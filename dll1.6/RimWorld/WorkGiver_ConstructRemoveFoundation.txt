using Verse;
using Verse.AI;

namespace RimWorld;

public class WorkGiver_ConstructRemoveFoundation : WorkGiver_ConstructAffectFloor
{
	protected override DesignationDef DesDef => DesignationDefOf.RemoveFoundation;

	public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
	{
		Job job = RemoveFloorJob(pawn, c);
		if (job != null)
		{
			return job;
		}
		Job job2 = RemoveFoundationJob(pawn, c);
		if (job2 != null)
		{
			return job2;
		}
		return JobMaker.MakeJob(JobDefOf.RemoveFoundation, c);
	}

	public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
	{
		if (!base.HasJobOnCell(pawn, c, forced))
		{
			return false;
		}
		if (!pawn.Map.terrainGrid.CanRemoveFoundationAt(c))
		{
			return false;
		}
		if (AnyBuildingBlockingFoundationRemoval(c, pawn.Map))
		{
			return false;
		}
		return true;
	}

	public static bool AnyBuildingBlockingFoundationRemoval(IntVec3 c, Map map)
	{
		if (!map.terrainGrid.CanRemoveFoundationAt(c))
		{
			return false;
		}
		Building firstBuilding = c.GetFirstBuilding(map);
		if (firstBuilding?.def.terrainAffordanceNeeded != null)
		{
			if (map.terrainGrid.UnderTerrainAt(c) == null)
			{
				return !map.terrainGrid.TopTerrainAt(c).affordances.Contains(firstBuilding.def.terrainAffordanceNeeded);
			}
			return !map.terrainGrid.UnderTerrainAt(c).affordances.Contains(firstBuilding.def.terrainAffordanceNeeded);
		}
		return false;
	}

	private Job RemoveFloorJob(Pawn pawn, IntVec3 c)
	{
		if (pawn.Map.terrainGrid.UnderTerrainAt(c) == null || !pawn.Map.terrainGrid.CanRemoveTopLayerAt(c))
		{
			return null;
		}
		if (!pawn.CanReserve(c, 1, -1, ReservationLayerDefOf.Floor))
		{
			return null;
		}
		if (pawn.WorkTypeIsDisabled(WorkGiverDefOf.ConstructRemoveFloors.workType))
		{
			return null;
		}
		Job job = JobMaker.MakeJob(JobDefOf.RemoveFloor, c);
		job.ignoreDesignations = true;
		return job;
	}

	private Job RemoveFoundationJob(Pawn pawn, IntVec3 c)
	{
		if (!pawn.Map.terrainGrid.CanRemoveFoundationAt(c))
		{
			return null;
		}
		if (!pawn.CanReserve(c, 1, -1, ReservationLayerDefOf.Floor))
		{
			return null;
		}
		if (pawn.WorkTypeIsDisabled(WorkGiverDefOf.ConstructRemoveFloors.workType))
		{
			return null;
		}
		Job job = JobMaker.MakeJob(JobDefOf.RemoveFoundation, c);
		job.ignoreDesignations = true;
		return job;
	}
}
