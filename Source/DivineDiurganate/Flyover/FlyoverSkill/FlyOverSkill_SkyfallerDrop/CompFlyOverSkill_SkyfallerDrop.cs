using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// Skyfaller投掷技能 - 在Flyover位置呼叫Skyfaller
    /// </summary>
    public class CompFlyOverSkill_SkyfallerDrop : CompFlyOverSkillBase
    {
        // 技能状态
        private int warmupTicksRemaining = 0;
        private IntVec3 targetPosition = IntVec3.Invalid;
        private bool isWarmingUp = false;
        
        /// <summary>
        /// 获取技能属性
        /// </summary>
        public CompProperties_FlyOverSkill_SkyfallerDrop SkyfallerProps
        {
            get
            {
                return props as CompProperties_FlyOverSkill_SkyfallerDrop;
            }
        }
        
        /// <summary>
        /// 立即释放回调
        /// </summary>
        protected override void OnInstantCast(IntVec3 targetPosition)
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null)
                {
                    Messages.Message("DD_FlyoverSkillNoMap".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }
                
                // 验证目标位置
                if (!targetPosition.InBounds(map))
                {
                    Messages.Message("DD_FlyoverSkilPointNotInBounds".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }
                
                // 如果有前摇时间，开始准备
                if (SkyfallerProps.warmupTicks > 0)
                {
                    StartWarmup(targetPosition, map);
                }
                else
                {
                    // 直接生成Skyfaller
                    SpawnSkyfaller(targetPosition, map);
                    
                    // 记录技能使用
                    Execute();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in skyfaller drop skill: {ex}");
                Messages.Message("DD_FlyoverSkillFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 开始前摇
        /// </summary>
        private void StartWarmup(IntVec3 position, Map map)
        {
            warmupTicksRemaining = SkyfallerProps.warmupTicks;
            targetPosition = position;
            isWarmingUp = true;

            
            // 显示消息
            Messages.Message("DD_FlyoverSkillSkyfallerWarmup".Translate(SkyfallerProps.skillName), 
                MessageTypeDefOf.SilentInput);
        }
        
        /// <summary>
        /// 生成Skyfaller
        /// </summary>
        private void SpawnSkyfaller(IntVec3 position, Map map)
        {
            try
            {
                // 计算最终位置（考虑随机偏移）
                IntVec3 finalPosition = CalculateDropPosition(position, map);
                
                if (SkyfallerProps.skyfallerDef != null)
                {
                    // 创建Skyfaller
                    Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(SkyfallerProps.skyfallerDef);
                    
                    // 如果有内容物，添加到Skyfaller
                    if (SkyfallerProps.contentThingDef != null && SkyfallerProps.contentCount > 0)
                    {
                        AddContentsToSkyfaller(skyfaller, SkyfallerProps.contentThingDef, SkyfallerProps.contentCount);
                    }
                    // 生成Skyfaller
                    GenSpawn.Spawn(skyfaller, finalPosition, map);
                    
                    // 显示成功消息
                    Messages.Message("DD_FlyoverSkillSkyfallerDropped".Translate(SkyfallerProps.skillName),
                        MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Log.Warning($"No skyfaller or projectile defined for skill: {SkyfallerProps.skillName}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error spawning skyfaller: {ex}");
            }
        }
        
        /// <summary>
        /// 计算坠落位置
        /// </summary>
        private IntVec3 CalculateDropPosition(IntVec3 basePosition, Map map)
        {
            IntVec3 dropPosition = basePosition;
            
            // 如果有随机半径，在半径内随机选择位置
            if (SkyfallerProps.dropRadius > 0)
            {
                // 在半径内随机选择一个有效的单元格
                var candidates = GenRadial.RadialCellsAround(basePosition, SkyfallerProps.dropRadius, true);
                var validCandidates = new List<IntVec3>();
                
                foreach (var cell in candidates)
                {
                    if (cell.InBounds(map) && cell.Walkable(map))
                    {
                        validCandidates.Add(cell);
                    }
                }
                
                if (validCandidates.Count > 0)
                {
                    dropPosition = validCandidates.RandomElement();
                }
            }
            // 如果有随机偏移，进行随机偏移
            else if (SkyfallerProps.randomizeDropOffset)
            {
                int offsetX = Rand.RangeInclusive(-SkyfallerProps.maxDistanceFromFlyover, SkyfallerProps.maxDistanceFromFlyover);
                int offsetZ = Rand.RangeInclusive(-SkyfallerProps.maxDistanceFromFlyover, SkyfallerProps.maxDistanceFromFlyover);
                
                // 确保最小距离
                if (Mathf.Abs(offsetX) < SkyfallerProps.minDistanceFromFlyover)
                {
                    offsetX = offsetX >= 0 ? SkyfallerProps.minDistanceFromFlyover : -SkyfallerProps.minDistanceFromFlyover;
                }
                if (Mathf.Abs(offsetZ) < SkyfallerProps.minDistanceFromFlyover)
                {
                    offsetZ = offsetZ >= 0 ? SkyfallerProps.minDistanceFromFlyover : -SkyfallerProps.minDistanceFromFlyover;
                }
                
                dropPosition = new IntVec3(basePosition.x + offsetX, basePosition.y, basePosition.z + offsetZ);
                
                // 确保位置在地图范围内
                if (!dropPosition.InBounds(map))
                {
                    dropPosition = basePosition;
                }
            }
            
            return dropPosition;
        }
        
        /// <summary>
        /// 添加内容物到Skyfaller
        /// </summary>
        private void AddContentsToSkyfaller(Skyfaller skyfaller, ThingDef contentDef, int count)
        {
            try
            {
                // 获取skyfaller的内部容器
                var compTransporter = skyfaller.GetComp<CompTransporter>();
                if (compTransporter != null && compTransporter.innerContainer != null)
                {
                    // 创建物品并添加到容器
                    Thing thing = ThingMaker.MakeThing(contentDef);
                    thing.stackCount = Mathf.Min(count, contentDef.stackLimit);
                    compTransporter.innerContainer.TryAdd(thing);
                }
                else
                {
                    // 尝试通过其他方式设置内容
                    Log.Warning($"Could not add contents to skyfaller: No transporter component found");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"Error adding contents to skyfaller: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 组件每帧更新
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            
            // 更新前摇状态
            if (isWarmingUp && warmupTicksRemaining > 0)
            {
                warmupTicksRemaining--;
                
                // 前摇结束
                if (warmupTicksRemaining <= 0)
                {
                    Map map = Find.CurrentMap;
                    if (map != null && targetPosition.IsValid && targetPosition.InBounds(map))
                    {
                        // 生成Skyfaller
                        SpawnSkyfaller(targetPosition, map);
                        
                        // 记录技能使用
                        Execute();
                    }
                    
                    // 重置状态
                    isWarmingUp = false;
                    targetPosition = IntVec3.Invalid;
                }
            }
        }
        
        /// <summary>
        /// 获取技能状态描述
        /// </summary>
        public override string GetStatusDescription()
        {
            if (isWarmingUp)
            {
                return "DD_FlyoverSkillWarmingUp".Translate(warmupTicksRemaining.ToStringSecondsFromTicks());
            }
            
            var baseDesc = base.GetStatusDescription();
            
            if (baseDesc != "DD_FlyoverSkillReady".Translate())
                return baseDesc;
            
            // 添加技能特定信息
            if (SkyfallerProps.skyfallerDef != null)
            {
                return "DD_FlyoverSkillSkyfallerDesc".Translate(SkyfallerProps.skyfallerDef.label);
            }
            
            return "DD_FlyoverSkillSkyfallerGeneric".Translate();
        }
        
        /// <summary>
        /// 获取技能冷却描述
        /// </summary>
        public override string GetCooldownDescription()
        {
            string desc = base.GetCooldownDescription();
            
            // 添加技能特定信息
            if (SkyfallerProps.skyfallerDef != null)
            {
                desc += $"\nDrops: {SkyfallerProps.skyfallerDef.LabelCap}";
            }
            
            if (SkyfallerProps.warmupTicks > 0)
            {
                desc += $"\nWarmup: {SkyfallerProps.warmupTicks.ToStringSecondsFromTicks()}";
            }
            
            return desc;
        }
        
        /// <summary>
        /// 检查技能是否可用
        /// </summary>
        public override bool CanUseNow(out string reason)
        {
            // 首先调用基类检查
            if (!base.CanUseNow(out reason))
            {
                return false;
            }
            
            // 检查是否正在准备中
            if (isWarmingUp)
            {
                reason = "DD_FlyoverSkillWarmingUp".Translate(warmupTicksRemaining.ToStringSecondsFromTicks());
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 序列化数据
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref warmupTicksRemaining, "warmupTicksRemaining", 0);
            Scribe_Values.Look(ref targetPosition, "targetPosition", IntVec3.Invalid);
            Scribe_Values.Look(ref isWarmingUp, "isWarmingUp", false);
        }
    }
}
