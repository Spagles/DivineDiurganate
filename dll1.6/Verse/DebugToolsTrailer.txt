using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse.AI;

namespace Verse;

public static class DebugToolsTrailer
{
	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000, requiresOdyssey = true)]
	private static List<DebugActionNode> BirdFlyoff()
	{
		List<DebugActionNode> list = new List<DebugActionNode>();
		foreach (int delay in new List<int> { 60, 90, 120, 150, 180, 210, 240, 270, 300 })
		{
			list.Add(new DebugActionNode($"{delay.TicksToSeconds()}s", DebugActionType.Action, delegate
			{
				DebugToolsGeneral.GenericRectTool("Set area", delegate(CellRect rect)
				{
					TimedFlyoff obj = (TimedFlyoff)GenSpawn.Spawn(ThingDefOf.TimedFlyoff, IntVec3.Zero, Find.CurrentMap);
					obj.rect = rect;
					obj.triggerTick = GenTicks.TicksGame + delay;
				});
			}));
		}
		return list;
	}

	[DebugAction("Pawns", "Mental break delayed", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
	private static List<DebugActionNode> MentalBreakDelay()
	{
		List<DebugActionNode> list = new List<DebugActionNode>();
		List<int> delays = new List<int> { 60, 300, 360, 420, 600 };
		foreach (MentalBreakDef item in DefDatabase<MentalBreakDef>.AllDefs.OrderByDescending((MentalBreakDef x) => x.intensity))
		{
			MentalBreakDef locBrDef = item;
			list.Add(new DebugActionNode(locBrDef.defName)
			{
				action = delegate
				{
					List<FloatMenuOption> list2 = new List<FloatMenuOption>();
					foreach (int delay in delays)
					{
						list2.Add(new FloatMenuOption($"{delay.TicksToSeconds()}s", delegate
						{
							DebugTools.curTool = new DebugTool("Select pawn", delegate
							{
								foreach (Pawn item2 in UI.MouseCell().GetThingList(Find.CurrentMap).OfType<Pawn>()
									.ToList())
								{
									if (item2.RaceProps.Humanlike)
									{
										MoteMaker.ThrowText(item2.DrawPos, Find.CurrentMap, item2.Label + " queued mental break");
										TimeMentalBreak obj = (TimeMentalBreak)GenSpawn.Spawn(ThingDefOf.TimeMentalBreak, IntVec3.Zero, Find.CurrentMap);
										obj.pawn = item2;
										obj.breakDef = locBrDef;
										obj.triggerTick = GenTicks.TicksGame + delay;
										break;
									}
								}
							});
						}));
					}
					Find.WindowStack.Add(new FloatMenu(list2));
				},
				labelGetter = delegate
				{
					string text = locBrDef.defName;
					if (Find.CurrentMap != null && !Find.CurrentMap.mapPawns.FreeColonists.Any((Pawn x) => locBrDef.Worker.BreakCanOccur(x)))
					{
						text = "[NO] " + text;
					}
					return text;
				}
			});
		}
		return list;
	}

	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000, requiresOdyssey = true)]
	private static void SwimAt(Pawn pawn)
	{
		DebugTools.curTool = new DebugTool("Select destination...", delegate
		{
			if (SwimPathFinder.TryFindSwimPath(pawn, UI.MouseCell(), out var result))
			{
				pawn.needs.joy.CurLevelPercentage = 0.3f;
				Job job = JobMaker.MakeJob(JobDefOf.GoSwimming, result[0]);
				job.locomotionUrgency = LocomotionUrgency.Walk;
				job.targetQueueA = new List<LocalTargetInfo>();
				for (int i = 1; i < result.Count; i++)
				{
					job.targetQueueA.Add(result[i]);
				}
				pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				DebugTools.curTool = null;
			}
		});
	}

	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000, requiresOdyssey = true)]
	private static void StartLavaFlowAtEdge()
	{
		List<DebugMenuOption> list = new List<DebugMenuOption>();
		foreach (Rot4 rot in Rot4.AllRotations)
		{
			list.Add(new DebugMenuOption(rot.ToStringHuman(), DebugMenuOptionMode.Action, delegate
			{
				Map currentMap = Find.CurrentMap;
				GameCondition_LavaFlow gameCondition_LavaFlow = GameConditionMaker.MakeConditionPermanent(GameConditionDefOf.LavaFlow) as GameCondition_LavaFlow;
				gameCondition_LavaFlow.trailerForceStartEdge = rot;
				currentMap.GameConditionManager.RegisterCondition(gameCondition_LavaFlow);
			}));
		}
		Find.WindowStack.Add(new Dialog_DebugOptionListLister(list, "Select destination"));
	}

	[DebugAction("Pawns", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = 1000)]
	private static void MeleeAttackTarget(Pawn p)
	{
		DebugTools.curTool = new DebugTool("Select target...", delegate
		{
			Pawn pawn = PawnAt(UI.MouseCell());
			Job job = JobMaker.MakeJob(JobDefOf.AttackMelee);
			job.targetA = pawn;
			p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
		});
		static Pawn PawnAt(IntVec3 c)
		{
			foreach (Thing item in Find.CurrentMap.thingGrid.ThingsAt(c))
			{
				if (item is Pawn result)
				{
					return result;
				}
			}
			return null;
		}
	}

	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = -1000, requiresOdyssey = true)]
	private static void VolcanicDebris()
	{
		SkyfallerMaker.SpawnSkyfaller(ThingDefOf.LavaRockIncoming, UI.MouseCell(), Find.CurrentMap);
	}

	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = -1000, requiresOdyssey = true)]
	private static List<DebugActionNode> VolcanicDebrisDelayed()
	{
		List<DebugActionNode> list = new List<DebugActionNode>();
		for (int i = 0; i < 41; i++)
		{
			int delay = i * 30;
			list.Add(new DebugActionNode(((float)delay / 60f).ToString("F1") + " seconds", DebugActionType.ToolMap, delegate
			{
				SkyfallerMaker.SpawnSkyfaller(ThingDefOf.LavaRockIncoming, UI.MouseCell(), Find.CurrentMap).ticksToImpact += delay;
			}));
		}
		return list;
	}

	[DebugAction("General", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap, displayPriority = -1000, requiresOdyssey = true)]
	private static void NuzzlePawn(Pawn animal)
	{
		if (!animal.IsAnimal)
		{
			Messages.Message("Must select an animal", MessageTypeDefOf.RejectInput);
			return;
		}
		DebugTools.curTool = new DebugTool("Select target...", delegate
		{
			Pawn pawn = animal.Map.thingGrid.ThingsAt(UI.MouseCell()).OfType<Pawn>().FirstOrDefault();
			if (pawn != null && pawn.RaceProps.Humanlike)
			{
				pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wait, 3000), JobTag.Misc);
				Job job = JobMaker.MakeJob(JobDefOf.Nuzzle, pawn);
				job.locomotionUrgency = LocomotionUrgency.Walk;
				job.expiryInterval = 3000;
				animal.jobs.TryTakeOrderedJob(job, JobTag.Misc);
				DebugTools.curTool = null;
			}
		});
	}
}
