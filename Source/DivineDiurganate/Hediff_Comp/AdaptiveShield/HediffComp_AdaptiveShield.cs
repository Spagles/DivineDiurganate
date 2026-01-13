using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 自适应护盾组件 - 格挡正面攻击，具有伤害阈值和自适应穿透机制
    /// </summary>
    public class HediffComp_AdaptiveShield : HediffComp
    {
        // 当前伤害阈值（会随穿透次数增加）
        private float currentDamageThreshold;
        
        // 穿透次数统计
        private int penetrationCount = 0;
        
        // 格挡次数统计
        private int blockCount = 0;
        
        // 上次穿透时间（用于衰减计时）
        private int lastPenetrationTick = -99999;
        
        public HediffCompProperties_AdaptiveShield Props => (HediffCompProperties_AdaptiveShield)props;
        
        /// <summary>
        /// 当前有效的伤害阈值
        /// </summary>
        public float CurrentThreshold => currentDamageThreshold;
        
        /// <summary>
        /// 格挡次数
        /// </summary>
        public int BlockCount => blockCount;
        
        /// <summary>
        /// 穿透次数
        /// </summary>
        public int PenetrationCount => penetrationCount;
        
        public override void CompPostMake()
        {
            base.CompPostMake();
            // 初始化伤害阈值
            currentDamageThreshold = Props.baseDamageThreshold;
        }
        
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref currentDamageThreshold, "currentDamageThreshold", Props.baseDamageThreshold);
            Scribe_Values.Look(ref penetrationCount, "penetrationCount", 0);
            Scribe_Values.Look(ref blockCount, "blockCount", 0);
            Scribe_Values.Look(ref lastPenetrationTick, "lastPenetrationTick", -99999);
        }
        
        /// <summary>
        /// 每Tick更新，处理阈值衰减
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            // 如果阈值高于基础值，检查是否需要衰减
            if (currentDamageThreshold > Props.baseDamageThreshold && Props.thresholdDecayDelay > 0)
            {
                int ticksSincePenetration = Find.TickManager.TicksGame - lastPenetrationTick;
                
                // 超过延迟时间后开始衰减
                if (ticksSincePenetration > Props.thresholdDecayDelay)
                {
                    // 每秒衰减一次（每60tick）
                    if (Find.TickManager.TicksGame % 60 == 0)
                    {
                        float decayAmount = Props.thresholdDecayPerSecond;
                        currentDamageThreshold = Mathf.Max(Props.baseDamageThreshold, currentDamageThreshold - decayAmount);
                    }
                }
            }
        }
        
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            
            // 激活时显示消息
            if (Props.showActivationMessage && Pawn != null && Pawn.Spawned)
            {
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "DD_Shield_Activated".Translate(), 1.9f);
            }
        }
        
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            
            // 关闭时重置阈值
            if (Props.resetThresholdOnDeactivate)
            {
                currentDamageThreshold = Props.baseDamageThreshold;
                penetrationCount = 0;
            }
        }
        


        /// <summary>
        /// 尝试格挡伤害 - 核心方法
        /// </summary>
        /// <param name="dinfo">伤害信息</param>
        /// <returns>true = 伤害被完全吸收, false = 伤害穿透</returns>
        public bool TryBlockDamage(ref DamageInfo dinfo)
        {
            if (Pawn == null || Pawn.Dead || Pawn.Downed)
                return false;
                
            // 检查攻击是否来自正面
            if (!IsAttackFromFront(dinfo))
                return false;
                
            float damageAmount = dinfo.Amount;
            
            // 检查是否超出伤害阈值
            if (damageAmount <= currentDamageThreshold)
            {
                // 伤害被完全吸收
                OnDamageBlocked(dinfo);
                return true;
            }
            else
            {
                // 伤害穿透护盾
                OnDamagePenetrated(dinfo);
                return false;
            }
        }
        
        /// <summary>
        /// 检查攻击是否来自正面（±blockAngle度范围）
        /// </summary>
        private bool IsAttackFromFront(DamageInfo dinfo)
        {
            // 计算攻击者相对于防御者的角度
            float attackerAngle = dinfo.Angle + 180f;
            float defenderAngle = Pawn.Rotation.AsAngle;
            
            // 标准化角度
            if (attackerAngle >= 360f)
                attackerAngle -= 360f;
            if (attackerAngle < 0f)
                attackerAngle += 360f;
                
            // 计算角度差
            float angleDiff = Mathf.Abs(defenderAngle - attackerAngle);
            if (angleDiff > 180f)
                angleDiff = 360f - angleDiff;
                
            return angleDiff <= Props.blockAngle;
        }
        
        /// <summary>
        /// 伤害被格挡时的处理
        /// </summary>
        private void OnDamageBlocked(DamageInfo dinfo)
        {
            blockCount++;
            
            // 播放格挡特效
            if (Pawn.Spawned && Pawn.Map != null)
            {
                // 显示格挡文字
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, 
                    "DD_Shield_Blocked".Translate(dinfo.Amount.ToString("F0")), 1.5f);
                
                // 播放格挡音效
                if (Props.blockSound != null)
                {
                    Props.blockSound.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                }
                
                // 格挡特效
                if (Props.blockEffecter != null)
                {
                    Props.blockEffecter.Spawn().Trigger(Pawn, dinfo.Instigator ?? Pawn, -1);
                }
                else
                {
                    // 使用默认的金属偏转特效
                    EffecterDefOf.Deflect_Metal.Spawn().Trigger(Pawn, dinfo.Instigator ?? Pawn, -1);
                }
            }
        }
        
        /// <summary>
        /// 伤害穿透护盾时的处理
        /// </summary>
        private void OnDamagePenetrated(DamageInfo dinfo)
        {
            penetrationCount++;
            
            // 记录穿透时间（用于衰减计时）
            lastPenetrationTick = Find.TickManager.TicksGame;
            
            // 提升伤害阈值
            float oldThreshold = currentDamageThreshold;
            currentDamageThreshold += Props.thresholdIncreaseOnPenetration;
            
            // 限制最大阈值
            if (Props.maxDamageThreshold > 0)
            {
                currentDamageThreshold = Mathf.Min(currentDamageThreshold, Props.maxDamageThreshold);
            }
            
            // 播放穿透提示
            if (Pawn.Spawned && Pawn.Map != null)
            {
                string message = "DD_Shield_Penetrated".Translate(
                    dinfo.Amount.ToString("F0"), 
                    oldThreshold.ToString("F0"),
                    currentDamageThreshold.ToString("F0"));
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, message, 2f);
                
                // 播放穿透音效
                if (Props.penetrateSound != null)
                {
                    Props.penetrateSound.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                }
            }
        }
        

        
        public override string CompTipStringExtra
        {
            get
            {
                return "DD_Shield_Status".Translate(
                    currentDamageThreshold.ToString("F0"),
                    blockCount.ToString(),
                    penetrationCount.ToString());
            }
        }
    }
    
    /// <summary>
    /// 自适应护盾属性配置
    /// </summary>
    public class HediffCompProperties_AdaptiveShield : HediffCompProperties
    {
        /// <summary>
        /// 基础伤害阈值（低于此值的伤害完全吸收）
        /// </summary>
        public float baseDamageThreshold = 20f;
        
        /// <summary>
        /// 最大伤害阈值（0表示无上限）
        /// </summary>
        public float maxDamageThreshold = 0f;
        
        /// <summary>
        /// 每次穿透后阈值增加量
        /// </summary>
        public float thresholdIncreaseOnPenetration = 2f;
        
        /// <summary>
        /// 格挡角度（正面±此角度范围内的攻击可被格挡）
        /// </summary>
        public float blockAngle = 70f;
        
        /// <summary>
        /// 关闭时是否重置阈值
        /// </summary>
        public bool resetThresholdOnDeactivate = true;
        

        
        /// <summary>
        /// 格挡音效
        /// </summary>
        public SoundDef blockSound;
        
        /// <summary>
        /// 穿透音效
        /// </summary>
        public SoundDef penetrateSound;
        
        /// <summary>
        /// 格挡特效
        /// </summary>
        public EffecterDef blockEffecter;
        
        /// <summary>
        /// 是否显示激活消息
        /// </summary>
        public bool showActivationMessage = true;
        
        /// <summary>
        /// 阈值衰减延迟（穿透后多少tick开始衰减，0表示不衰减）
        /// 默认2500tick ≈ 约42秒（游戏内约1小时）
        /// </summary>
        public int thresholdDecayDelay = 2500;
        
        /// <summary>
        /// 每秒阈值衰减量
        /// </summary>
        public float thresholdDecayPerSecond = 1f;
        
        public HediffCompProperties_AdaptiveShield()
        {
            compClass = typeof(HediffComp_AdaptiveShield);
        }
    }
}
