using Verse;
using Verse.AI;

namespace RimWorld;

public class WorkGiver_Warden_SuppressSlave : WorkGiver_Warden
{
	public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
	{
		if (!ModLister.CheckIdeology("Slave suppression"))
		{
			return null;
		}
		Pawn pawn2 = t as Pawn;
		if (!ShouldTakeCareOfSlave(pawn, pawn2))
		{
			return null;
		}
		if (pawn2.guest.slaveInteractionMode != SlaveInteractionModeDefOf.Suppress || pawn2.Downed || !pawn2.Awake() || !pawn.CanReserve(t))
		{
			return null;
		}
		pawn2.needs.TryGetNeed(out Need_Suppression need);
		if (need == null || !need.CanBeSuppressedNow || !pawn2.guest.ScheduledForSlaveSuppression)
		{
			return null;
		}
		return JobMaker.MakeJob(JobDefOf.SlaveSuppress, t);
	}
}
