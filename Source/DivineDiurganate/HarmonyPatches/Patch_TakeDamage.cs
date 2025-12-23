// File: HarmonyPatches/ArmorSystemPatch.cs
using HarmonyLib;
using RimWorld;
using Verse;

namespace DivineDiurganate.HarmonyPatches
{
    /// <summary>
    /// Harmony补丁：在TakeDamage前检查装甲系统
    /// </summary>
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("TakeDamage")]
    public static class Thing_TakeDamage_Patch
    {
        /// <summary>
        /// 前置补丁：在TakeDamage执行前检查装甲
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            // 检查是否有基础装甲组件
            var basicArmorComp = __instance.TryGetComp<CompMechArmor>();
            if (basicArmorComp != null)
            {
                // 复制dinfo以便修改
                DamageInfo dinfoCopy = dinfo;
                
                // 尝试阻挡伤害
                if (basicArmorComp.TryBlockDamage(ref dinfoCopy))
                {
                    // 伤害被完全抵消，返回空结果
                    __result = new DamageWorker.DamageResult();
                    return false; // 跳过原方法
                }
                
                // 更新dinfo（如果有部分阻挡）
                dinfo = dinfoCopy;
                return true; // 继续执行原方法
            }
            
            // 没有装甲组件，正常执行
            return true;
        }
    }
    
    /// <summary>
    /// 可选的：在PreApplyDamage中也添加检查
    /// </summary>
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("PreApplyDamage")]
    public static class Thing_PreApplyDamage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            // 如果已经被其他系统吸收了，跳过
            if (absorbed)
                return;
            
            // 检查是否有装甲组件
            var armorComp = __instance.TryGetComp<CompMechArmor>();
            
            if (armorComp != null)
            {
                // 这里可以进行额外的检查
                // 例如：检查装甲是否在冷却中、是否有特殊状态等
            }
        }
    }
}
