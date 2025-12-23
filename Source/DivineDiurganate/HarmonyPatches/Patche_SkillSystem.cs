// File: HarmonyPatches/SkillSystemPatches.cs
using HarmonyLib;
using RimWorld;
using Verse;

namespace DivineDiurganate.HarmonyPatches
{
    /// <summary>
    /// 更简单的修复：完全跳过机甲技能的Tick系统
    /// </summary>
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("TickInterval")]
    public static class Pawn_TickInterval_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance)
        {
            // 检查是否是机甲并且有技能继承组件
            var skillComp = __instance.TryGetComp<CompMechSkillInheritance>();
            if (skillComp != null)
            {
                // 完全跳过原版技能系统的Tick
                // 我们会在组件的CompTick中处理技能更新
                return false;
            }

            return true;
        }
    }

}
