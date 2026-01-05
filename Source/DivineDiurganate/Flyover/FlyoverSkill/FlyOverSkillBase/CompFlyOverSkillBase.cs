
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机技能基类
    /// </summary>
    public class CompFlyOverSkillBase : ThingComp
    {
        // 技能状态
        public int lastUseTick = -99999;
        public int useCount = 0;
        public bool isAvailable = true;

        // 选择状态
        protected SkillTargetType currentTargetType;
        protected IntVec3 firstTargetPoint;
        protected IntVec3 secondTargetPoint;

        /// <summary>
        /// 获取技能属性
        /// </summary>
        public CompProperties_FlyOverSkillBase SkillProps
        {
            get
            {
                return props as CompProperties_FlyOverSkillBase;
            }
        }

        /// <summary>
        /// 获取关联的战机数据
        /// </summary>
        public FlyoverData LinkedFlyoverData
        {
            get
            {
                var managedComp = parent.GetComp<CompFlyoverManaged>();
                return managedComp?.FlyoverData;
            }
        }

        /// <summary>
        /// 获取技能冷却百分比（0-1，0表示冷却完毕）
        /// </summary>
        public float CooldownPercent
        {
            get
            {
                if (SkillProps.cooldownTicks <= 0) return 0f;

                int ticksSinceUse = Find.TickManager.TicksGame - lastUseTick;
                if (ticksSinceUse >= SkillProps.cooldownTicks) return 0f;

                return 1f - (float)ticksSinceUse / SkillProps.cooldownTicks;
            }
        }

        /// <summary>
        /// 检查技能是否可用
        /// </summary>
        public virtual bool CanUseNow(out string reason)
        {
            reason = null;

            // 检查基本可用性
            if (!isAvailable)
            {
                reason = "Skill is unavailable";
                return false;
            }

            // 检查使用次数限制
            if (SkillProps.maxUses > 0 && useCount >= SkillProps.maxUses)
            {
                reason = $"Use limit reached ({useCount}/{SkillProps.maxUses})";
                return false;
            }

            // 检查冷却时间
            if (Find.TickManager.TicksGame < lastUseTick + SkillProps.cooldownTicks)
            {
                int remainingTicks = lastUseTick + SkillProps.cooldownTicks - Find.TickManager.TicksGame;
                reason = $"On cooldown for {remainingTicks.ToStringSecondsFromTicks()}";
                return false;
            }

            // 检查战机状态
            var flyoverData = LinkedFlyoverData;
            if (flyoverData == null)
            {
                reason = "No aircraft data found";
                return false;
            }

            if (flyoverData.status == FlyoverStatus.Destroyed && !SkillProps.canUseWhenDestroyed)
            {
                reason = "Aircraft is destroyed";
                return false;
            }

            if (flyoverData.status == FlyoverStatus.OnMap && !SkillProps.canUseWhenOnMap)
            {
                reason = "Cannot use while aircraft is on map";
                return false;
            }

            if (flyoverData.status == FlyoverStatus.Standby && !SkillProps.canUseWhenStandby)
            {
                reason = "Cannot use while aircraft is in standby";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 激活技能（开始目标选择）
        /// </summary>
        public virtual void Activate()
        {
            if (!CanUseNow(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return;
            }

            currentTargetType = SkillProps.targetType;

            // 根据目标类型开始选择
            switch (currentTargetType)
            {
                case SkillTargetType.SinglePoint:
                    StartSinglePointSelection();
                    break;

                case SkillTargetType.TwoPoints:
                    StartTwoPointsSelection();
                    break;
            }
        }

        /// <summary>
        /// 单点选择
        /// </summary>
        protected virtual void StartSinglePointSelection()
        {
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate (LocalTargetInfo target)
                {
                    OnSinglePointSelected(target);
                }
            );

            // 使用XML定义的消息，支持字符串格式化
            string message = GetFormattedMessage(SkillProps.singlePointSelectMessage, SkillProps.skillName);
            Messages.Message(message, MessageTypeDefOf.SilentInput);
        }

        /// <summary>
        /// 双点选择（第一步）
        /// </summary>
        protected virtual void StartTwoPointsSelection()
        {
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate (LocalTargetInfo target)
                {
                    firstTargetPoint = target.Cell;
                    OnSecondPointSelection();
                }
            );

            // 使用XML定义的消息，支持字符串格式化
            string message = GetFormattedMessage(SkillProps.twoPointsFirstPointMessage, SkillProps.skillName);
            Messages.Message(message, MessageTypeDefOf.SilentInput);
        }

        /// <summary>
        /// 双点选择（第二步）
        /// </summary>
        protected virtual void OnSecondPointSelection()
        {
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate (LocalTargetInfo target)
                {
                    secondTargetPoint = target.Cell;
                    OnTwoPointsSelected(firstTargetPoint, secondTargetPoint);
                }
            );

            // 使用XML定义的消息，支持字符串格式化
            string message = GetFormattedMessage(SkillProps.twoPointsSecondPointMessage, SkillProps.skillName);
            Messages.Message(message, MessageTypeDefOf.SilentInput);
        }

        /// <summary>
        /// 获取格式化消息（支持{0}占位符）
        /// </summary>
        protected virtual string GetFormattedMessage(string messageTemplate, params object[] args)
        {
            if (string.IsNullOrEmpty(messageTemplate))
            {
                // 如果XML中没有定义消息，返回默认消息
                if (SkillProps.targetType == SkillTargetType.SinglePoint)
                    return $"Select target for {SkillProps.skillName}";
                else if (SkillProps.targetType == SkillTargetType.TwoPoints)
                    return $"Select points for {SkillProps.skillName}";
                else
                    return $"Select for {SkillProps.skillName}";
            }

            try
            {
                return string.Format(messageTemplate, args);
            }
            catch (System.FormatException)
            {
                // 如果格式化失败，直接返回消息模板
                Log.Warning($"Invalid message format for skill {SkillProps.skillName}: {messageTemplate}");
                return messageTemplate;
            }
        }

        /// <summary>
        /// 单点选择完成回调
        /// </summary>
        protected virtual void OnSinglePointSelected(LocalTargetInfo target)
        {
            // 由子类实现具体逻辑
        }

        /// <summary>
        /// 双点选择完成回调
        /// </summary>
        protected virtual void OnTwoPointsSelected(IntVec3 point1, IntVec3 point2)
        {
            // 由子类实现具体逻辑
        }

        /// <summary>
        /// 单位选择完成回调
        /// </summary>
        protected virtual void OnPawnSelected(LocalTargetInfo target)
        {
            // 由子类实现具体逻辑
        }

        /// <summary>
        /// 建筑选择完成回调
        /// </summary>
        protected virtual void OnBuildingSelected(LocalTargetInfo target)
        {
            // 由子类实现具体逻辑
        }

        /// <summary>
        /// 执行技能逻辑
        /// </summary>
        public virtual void Execute()
        {
            // 记录使用
            lastUseTick = Find.TickManager.TicksGame;
            useCount++;
        }

        /// <summary>
        /// 获取技能图标
        /// </summary>
        public virtual Texture2D GetSkillIcon()
        {
            if (!string.IsNullOrEmpty(SkillProps.iconPath))
            {
                var icon = ContentFinder<Texture2D>.Get(SkillProps.iconPath, false);
                if (icon != null) return icon;
            }

            // 默认图标
            return ContentFinder<Texture2D>.Get("UI/Icons/DefaultSkill", false) ?? BaseContent.BadTex;
        }

        /// <summary>
        /// 获取技能冷却时间描述
        /// </summary>
        public virtual string GetCooldownDescription()
        {
            if (SkillProps.cooldownTicks <= 0) return "No cooldown";

            int remainingTicks = lastUseTick + SkillProps.cooldownTicks - Find.TickManager.TicksGame;
            if (remainingTicks <= 0) return "Ready";

            return $"Cooldown: {remainingTicks.ToStringSecondsFromTicks()}";
        }

        /// <summary>
        /// 获取技能状态描述
        /// </summary>
        public virtual string GetStatusDescription()
        {
            if (!CanUseNow(out string reason))
            {
                return reason;
            }
            return "Ready";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastUseTick, "lastUseTick", -99999);
            Scribe_Values.Look(ref useCount, "useCount", 0);
            Scribe_Values.Look(ref isAvailable, "isAvailable", true);
        }
    }
}
