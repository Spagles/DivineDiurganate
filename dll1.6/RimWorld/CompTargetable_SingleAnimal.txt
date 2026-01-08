using System.Collections.Generic;
using Verse;

namespace RimWorld;

public class CompTargetable_SingleAnimal : CompTargetable
{
	protected override bool PlayerChoosesTarget => true;

	protected override TargetingParameters GetTargetingParameters()
	{
		return new TargetingParameters
		{
			canTargetPawns = true,
			canTargetBuildings = false,
			canTargetAnimals = true,
			canTargetHumans = false,
			canTargetMechs = false
		};
	}

	public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
	{
		yield return targetChosenByPlayer;
	}
}
