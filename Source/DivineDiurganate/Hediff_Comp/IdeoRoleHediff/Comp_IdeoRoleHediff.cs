// File: Comp_IdeoRoleHediff_Fixed.cs
using RimWorld;
using Verse;
using System.Linq;

namespace DivineDiurganate
{
    public class Comp_IdeoRoleHediff : HediffComp
    {
        private HediffCompProperties_IdeoRoleHediff Props => (HediffCompProperties_IdeoRoleHediff)this.props;
        
        private int lastCheckTick = -1;
        
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            
            // 如果是DDmechunit，跳过检查
            if (IsDDmechunit(this.Pawn))
            {
                return; // DDmechunit的Hediff由另一个组件负责，我们不处理
            }
            
            // 定期检查
            if (Find.TickManager.TicksGame >= lastCheckTick + Props.checkIntervalTicks)
            {
                lastCheckTick = Find.TickManager.TicksGame;
                CheckRoleRequirement();
            }
        }
        
        // 检查Pawn是否为DDmechunit
        private bool IsDDmechunit(Pawn pawn)
        {
            return pawn is DDmechunit;
        }
        
        private void CheckRoleRequirement()
        {
            if (this.Pawn == null || !this.Pawn.Spawned || this.Pawn.Dead)
                return;
                
            // 检查是否需要这个hediff
            if (!MeetsAllRequirements())
            {
                // 如果不需要，移除这个hediff
                this.Pawn.health.RemoveHediff(this.parent);
            }
        }
        
        private bool MeetsAllRequirements()
        {
            // 1. 检查角色要求
            if (Props.requireRole)
            {
                if (!MeetsRoleRequirement())
                    return false;
            }
            
            // 2. 检查Meme要求
            if (Props.requireMeme)
            {
                if (!MeetsMemeRequirement())
                    return false;
            }
            
            return true;
        }
        
        private bool MeetsRoleRequirement()
        {
            if (this.Pawn?.Ideo == null || Props.requiredRole == null)
                return false;
                
            // 获取pawn的意识形态职位
            var pawnRole = this.Pawn.Ideo.GetRole(this.Pawn);
            return pawnRole != null && pawnRole.def == Props.requiredRole;
        }
        
        private bool MeetsMemeRequirement()
        {
            if (this.Pawn?.Ideo == null || Props.requiredMeme == null || Props.requiredMeme.Count == 0)
                return false;
                
            // 检查pawn的意识形态是否包含所需的Meme
            if (Props.requireAllMemes)
            {
                // 必须包含所有指定的Meme
                foreach (var meme in Props.requiredMeme)
                {
                    if (!this.Pawn.Ideo.HasMeme(meme))
                        return false;
                }
                return true;
            }
            else
            {
                // 只需要包含任意一个指定的Meme
                foreach (var meme in Props.requiredMeme)
                {
                    if (this.Pawn.Ideo.HasMeme(meme))
                        return true;
                }
                return false;
            }
        }
        
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", -1);
        }
    }
}
