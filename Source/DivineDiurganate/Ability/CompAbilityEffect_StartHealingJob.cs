using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace DivineDiurganate
{
    /// <summary>
    /// 简化的治疗能力效果 - 只负责启动治疗工作
    /// </summary>
    public class CompAbilityEffect_StartHealingJob : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            // 验证目标
            if (target == null || !target.IsValid)
                return;
                
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
                return;
            
            // 简单检查：目标需要治疗
            if (!NeedsHealing(targetPawn))
                return;
            
            // 启动治疗工作
            StartHealingJob(targetPawn);
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

            return pawn.health.summaryHealth.SummaryHealthPercent < 1;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
                
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null)
                return false;
                
            // 简单验证：目标需要治疗
            return NeedsHealing(targetPawn);
        }
    }
    
    /// <summary>
    /// 简化的属性 - 只需要启动工作
    /// </summary>
    public class CompProperties_AbilityStartHealingJob : CompProperties_AbilityEffect
    {
        // 可选：一个简单的特效
        public FleckDef fleckDef;
        
        public CompProperties_AbilityStartHealingJob()
        {
            compClass = typeof(CompAbilityEffect_StartHealingJob);
        }
    }
}
