using RimWorld;
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
            return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 0. 初始检查
            AddFailCondition(() =>
            {
                var mech = TargetThingA as DDmechunit;
                if (mech == null || mech.Destroyed)
                {
                    Messages.Message("DD_MechDestroyed".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }

                var comp = mech.GetComp<CompMechPilotHolder>();
                if (comp == null || comp.IsFull || !comp.CanAddPilot(pawn))
                {
                    Messages.Message("DD_MechCannotEnter".Translate(), MessageTypeDefOf.RejectInput, false);
                    return true;
                }

                if (pawn.Downed || pawn.Dead)
                    return true;

                return false;
            });

            // 1. 走到机甲旁边
            yield return Toils_Goto.GotoThing(MechIndex, PathEndMode.Touch);

            // 2. 检查是否仍然可以进入
            yield return Toils_General.Wait(10).WithProgressBarToilDelay(MechIndex);

            // 3. 进入机甲
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
                    Messages.Message("DD_PilotEnteredMech".Translate(pawn.LabelShort, mech.LabelShort),
                        MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("DD_MechCannotEnter".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            };
            enterToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enterToil;
        }
    }
}