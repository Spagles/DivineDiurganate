using RimWorld;
using Verse.AI;

namespace Verse;

public class TimedFlyoff : Thing
{
	public CellRect rect;

	public int triggerTick;

	protected override void Tick()
	{
		if (GenTicks.TicksGame < triggerTick)
		{
			return;
		}
		foreach (IntVec3 cell in rect.Cells)
		{
			foreach (Thing thing in cell.GetThingList(base.Map))
			{
				if (thing is Pawn { flight: not null } pawn)
				{
					Job job = JobMaker.MakeJob(JobDefOf.ExitMapFlying);
					pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				}
			}
		}
		Destroy();
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref rect, "rect");
		Scribe_Values.Look(ref triggerTick, "triggerTick", 0);
	}
}
