using System.Text;
using Verse;

namespace RimWorld;

public class StatWorker_Wildness : StatWorker
{
	public override bool ShouldShowFor(StatRequest req)
	{
		Pawn pawn = req.Pawn ?? (req.Thing as Pawn);
		if (pawn != null && (pawn.IsWildMan() || pawn.IsAnimal))
		{
			return true;
		}
		if (req.Def is ThingDef { race: { Animal: not false } })
		{
			return true;
		}
		return false;
	}

	public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(base.GetExplanationFinalizePart(req, numberSense, finalVal));
		stringBuilder.AppendLine();
		ThingDef thingDef = req.Def as ThingDef;
		if (thingDef.race != null && !thingDef.race.Humanlike)
		{
			stringBuilder.AppendLine(string.Format("{0}: {1}", "TrainingDecayInterval".Translate(), TrainableUtility.DegradationPeriodTicks(thingDef).ToStringTicksToDays()));
			stringBuilder.AppendLine();
		}
		if (!TrainableUtility.TamenessCanDecay(thingDef))
		{
			string key = (thingDef.race.FenceBlocked ? "TamenessWillNotDecayFenceBlocked" : "TamenessWillNotDecay");
			stringBuilder.AppendLine(key.Translate());
		}
		return stringBuilder.ToString();
	}
}
