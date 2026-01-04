using RimWorld;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    /// <summary>
    /// 简化的治疗能力效果 - 只负责启动治疗工作
    /// </summary>
    public class CompAbilityEffect_StartHealingJob : CompAbilityEffect
    {
        public new CompProperties_AbilityStartHealingJob Props => 
            (CompProperties_AbilityStartHealingJob)props;
        
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            // 验证目标
            if (!target.IsValid || target.Pawn == null)
                return;
                
            Pawn targetPawn = target.Pawn;
            
            // 简单检查：目标需要治疗
            if (!NeedsHealing(targetPawn))
                return;
            
            // 启动治疗工作
            StartHealingJob(targetPawn);
            
            // 播放特效（如果有）
            if (Props.fleckDef != null)
            {
                FleckMaker.Static(targetPawn.Position, targetPawn.Map, Props.fleckDef);
            }
        }
        
        /// <summary>
        /// 启动治疗工作
        /// </summary>
        private void StartHealingJob(Pawn target)
        {
            // 创建治疗工作
            Job job = JobMaker.MakeJob(DD_JobDefOf.DD_Holy_TendTarget, target);
            
            // 分配工作给施法者
            parent.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, true);
        }
        
        /// <summary>
        /// 检查目标是否需要治疗
        /// </summary>
        public bool NeedsHealing(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.Dead)
                return false;

            // 使用更稳定的检查方式
            return pawn.health.HasHediffsNeedingTend(false) || 
                   pawn.health.summaryHealth.SummaryHealthPercent < 1.0f;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
                
            Pawn targetPawn = target.Pawn;
                
            // 简单验证：目标需要治疗
            if (!NeedsHealing(targetPawn))
            {
                if (throwMessages)
                    Messages.Message("DD_TargetDoesNotNeedHealing".Translate(targetPawn.LabelShortCap), 
                                    MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }
        
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 简化版本，只检查最基本条件
            return target.Pawn != null && NeedsHealing(target.Pawn);
        }
    }
    
    /// <summary>
    /// 简化的属性 - 只需要启动工作
    /// </summary>
    public class CompProperties_AbilityStartHealingJob : CompProperties_AbilityEffect
    {
        // 可选：一个简单的特效
        public FleckDef fleckDef;
        
        // 可选：是否允许治疗敌对单位
        public bool allowHealEnemy = false;
        
        // 可选：治疗范围
        public float range = 30f;
        
        public CompProperties_AbilityStartHealingJob()
        {
            compClass = typeof(CompAbilityEffect_StartHealingJob);
            this.range = 30f; // 默认范围
        }
    }
}
