namespace Verse;

public class TimeMentalBreak : Thing
{
	public Pawn pawn;

	public MentalBreakDef breakDef;

	public int triggerTick;

	protected override void Tick()
	{
		if (GenTicks.TicksGame >= triggerTick)
		{
			if (pawn != null && pawn.Spawned)
			{
				breakDef.Worker.TryStart(pawn, null, causedByMood: false);
			}
			Destroy();
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_References.Look(ref pawn, "pawn");
		Scribe_Defs.Look(ref breakDef, "breakDef");
		Scribe_Values.Look(ref triggerTick, "triggerTick", 0);
	}
}
