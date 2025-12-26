using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    public class CompProperties_AbilityDestroyOwnBodyPart : CompProperties_AbilityEffect
    {
        // 要破坏的身体部位列表
        public List<BodyPartDef> bodyPartsToDestroy;

        public CompProperties_AbilityDestroyOwnBodyPart()
        {
            compClass = typeof(CompAbilityEffect_DestroyOwnBodyPart);
        }
    }
}
