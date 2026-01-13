using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 切换自适应护盾的技能效果组件
    /// 如果目标没有护盾Hediff则添加，如果有则移除
    /// </summary>
    public class CompAbilityEffect_ToggleAdaptiveShield : CompAbilityEffect
    {
        public new CompProperties_AbilityToggleAdaptiveShield Props => 
            (CompProperties_AbilityToggleAdaptiveShield)props;
        
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            Pawn targetPawn = target.Pawn ?? parent.pawn;
            
            if (targetPawn == null)
                return;
                
            // 检查是否已有护盾Hediff
            Hediff existingHediff = targetPawn.health.hediffSet.GetFirstHediffOfDef(Props.shieldHediffDef);
            
            if (existingHediff != null)
            {
                // 关闭护盾
                targetPawn.health.RemoveHediff(existingHediff);
                
                if (Props.deactivateMessage != null && targetPawn.Spawned)
                {
                    Messages.Message(Props.deactivateMessage.Translate(targetPawn.LabelShortCap), 
                        targetPawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            else
            {
                // 开启护盾
                Hediff hediff = HediffMaker.MakeHediff(Props.shieldHediffDef, targetPawn);
                targetPawn.health.AddHediff(hediff);
                
                if (Props.activateMessage != null && targetPawn.Spawned)
                {
                    Messages.Message(Props.activateMessage.Translate(targetPawn.LabelShortCap), 
                        targetPawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            
            // 播放切换特效
            if (Props.toggleFleck != null && targetPawn.Spawned && targetPawn.Map != null)
            {
                FleckMaker.Static(targetPawn.Position, targetPawn.Map, Props.toggleFleck);
            }
        }
        
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
                
            if (Props.shieldHediffDef == null)
            {
                if (throwMessages)
                    Messages.Message("DD_Shield_NoHediffConfigured".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取当前护盾状态用于图标显示
        /// </summary>
        public bool IsShieldActive(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(Props.shieldHediffDef) != null;
        }
        
        public override string ExtraTooltipPart()
        {
            Pawn pawn = parent.pawn;
            if (pawn == null)
                return null;
                
            bool isActive = IsShieldActive(pawn);
            return "DD_Shield_CurrentState".Translate(isActive ? "DD_Shield_On".Translate() : "DD_Shield_Off".Translate());
        }
    }
    
    /// <summary>
    /// 切换护盾技能的属性配置
    /// </summary>
    public class CompProperties_AbilityToggleAdaptiveShield : CompProperties_AbilityEffect
    {
        /// <summary>
        /// 护盾Hediff定义
        /// </summary>
        public HediffDef shieldHediffDef;
        
        /// <summary>
        /// 激活消息翻译键
        /// </summary>
        public string activateMessage = "DD_Shield_ActivatedMessage";
        
        /// <summary>
        /// 关闭消息翻译键
        /// </summary>
        public string deactivateMessage = "DD_Shield_DeactivatedMessage";
        
        /// <summary>
        /// 切换时的特效
        /// </summary>
        public FleckDef toggleFleck;
        
        public CompProperties_AbilityToggleAdaptiveShield()
        {
            compClass = typeof(CompAbilityEffect_ToggleAdaptiveShield);
        }
    }
}
