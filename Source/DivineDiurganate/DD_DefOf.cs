using RimWorld;
using Verse;

namespace DivineDiurganate
{
    [DefOf]
    public static class DD_JobDefOf
    {
        public static JobDef DD_EnterMech;
        public static JobDef DD_RefuelMech;
        public static JobDef DD_RepairMech;
        public static JobDef DD_ForceEjectPilot;
        static DD_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DD_JobDefOf));
        }
    }

    [DefOf]
    public static class DD_MentalStateDefOf
    {
        public static MentalStateDef DD_MechNoPilot;

        static DD_MentalStateDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DD_MentalStateDefOf));
        }
    }
}