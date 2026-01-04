using HarmonyLib;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    [HarmonyPatch(typeof(PawnGenerator))]
    [HarmonyPatch("GeneratePawn", typeof(PawnGenerationRequest))]
    public static class Patch_GeneratePawn_HeadType
    {
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            Pawn pawn = __result;
            if (pawn == null) return;
            
            // 只处理人类
            if (!pawn.RaceProps.Humanlike) return;

            // 检查 FixedIdentityExtension
            HeadTypeExtension fixedIdentityExt = request.KindDef?.GetModExtension<HeadTypeExtension>();
            if (fixedIdentityExt == null) return;
            
            // 只保留头部类型修改的部分
            if (pawn.story != null && !fixedIdentityExt.forcedHeadTypeDef.NullOrEmpty())
            {
                HeadTypeDef headTypeDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(fixedIdentityExt.forcedHeadTypeDef);
                if (headTypeDef != null && 
                    (headTypeDef.gender == Gender.None || headTypeDef.gender == pawn.gender) && 
                    pawn.story.headType != headTypeDef)
                {
                    pawn.story.headType = headTypeDef;
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                }
            }
        }
    }
}
