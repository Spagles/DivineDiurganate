using HarmonyLib;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// Harmony补丁 - 拦截伤害并让自适应护盾处理
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_AdaptiveShield_PreApplyDamage
    {
        /// <summary>
        /// 在伤害应用前检查是否被护盾格挡
        /// </summary>
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance == null || __instance.Dead)
                return true;
                
            // 检查是否有自适应护盾Hediff
            var hediffWithShield = GetAdaptiveShieldHediff(__instance);
            if (hediffWithShield == null)
                return true;
                
            // 尝试格挡伤害
            if (hediffWithShield.TryBlockDamage(ref dinfo))
            {
                absorbed = true;
                return false; // 伤害被完全吸收，跳过原方法
            }
            
            return true; // 伤害穿透，继续正常处理
        }
        
        /// <summary>
        /// 获取Pawn身上的自适应护盾组件
        /// </summary>
        public static HediffComp_AdaptiveShield GetAdaptiveShieldHediff(Pawn pawn)
        {
            if (pawn.health?.hediffSet?.hediffs == null)
                return null;
                
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                var shieldComp = hediff.TryGetComp<HediffComp_AdaptiveShield>();
                if (shieldComp != null)
                    return shieldComp;
            }
            
            return null;
        }
    }
    

}
