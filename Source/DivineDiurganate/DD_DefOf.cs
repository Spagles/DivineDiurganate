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
        static DD_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DD_JobDefOf));
        }
    }
}
