// JobDriver_EnterMech.cs
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    public class JobDriver_EnterMech : JobDriver
    {
        private const TargetIndex MechIndex = TargetIndex.A;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Pawn pawn = this.pawn;
            LocalTargetInfo target = this.job.GetTarget(MechIndex);
            
            // 尝试保留机甲
            if (!pawn.Reserve(target, job, 1, -1, null, errorOnFailed))
                return false;
                
            return true;
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 1. 走到机甲旁边
            this.FailOnDespawnedNullOrForbidden(MechIndex);
            this.FailOn(() => 
            {
                var mech = TargetThingA as DDmechunit;
                if (mech == null) 
                    return true;
                
                var comp = mech.GetComp<CompMechPilotHolder>();
                if (comp == null || comp.IsFull || !comp.CanAddPilot(pawn))
                {
                    // 如果机甲已满或无法添加驾驶员，取消工作
                    return true;
                }
                
                return false;
            });
            
            yield return Toils_Goto.GotoThing(MechIndex, PathEndMode.Touch);
            
            // 2. 进入机甲
            Toil enterToil = new Toil();
            enterToil.initAction = () =>
            {
                var mech = TargetThingA as DDmechunit;
                if (mech == null) 
                    return;
                
                var comp = mech.GetComp<CompMechPilotHolder>();
                if (comp != null && comp.CanAddPilot(pawn))
                {
                    comp.AddPilot(pawn);
                }
            };
            enterToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enterToil;
        }
    }
}
