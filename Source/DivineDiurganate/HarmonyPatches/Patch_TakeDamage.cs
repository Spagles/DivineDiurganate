using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate.HarmonyPatches
{
    /// <summary>
    /// 简化的机甲装甲系统补丁
    /// 直接检查DD_MechArmor stat，大于0则启动装甲系统
    /// </summary>
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("TakeDamage")]
    public static class Thing_TakeDamage_Patch
    {
        // 缓存装甲值StatDef
        private static readonly StatDef ArmorStatDef = StatDef.Named("DD_MechArmor");
        
        // 阻挡效果的MoteDef
        private static readonly ThingDef BlockMoteDef = DefDatabase<ThingDef>.GetNamedSilentFail("Mote_Spark");
        
        // 阻挡音效
        private static readonly SoundDef BlockSoundDef = DefDatabase<SoundDef>.GetNamedSilentFail("ArmorBlock");
        
        // 调试统计
        private static readonly Dictionary<Thing, ArmorStats> DebugStats = new Dictionary<Thing, ArmorStats>();
        
        private class ArmorStats
        {
            public int blockedHits = 0;
            public int totalHits = 0;
        }
        
        /// <summary>
        /// 前置补丁：在TakeDamage执行前检查装甲
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
        {
            float armorValue = __instance.GetStatValue(ArmorStatDef);
            
            // 如果装甲值 <= 0，不启动装甲系统
            if (armorValue <= 0)
                return true;
            
            // 更新调试统计
            if (!DebugStats.ContainsKey(__instance))
                DebugStats[__instance] = new ArmorStats();
            DebugStats[__instance].totalHits++;
            
            // 计算穿甲伤害
            float armorPenetration = dinfo.ArmorPenetrationInt;
            float piercingDamage = dinfo.Amount * armorPenetration;
            
            // 判断是否应该阻挡
            bool shouldBlock = piercingDamage < armorValue;
            
            if (shouldBlock)
            {
                // 阻挡成功
                DebugStats[__instance].blockedHits++;
                
                // 显示阻挡效果
                ShowBlockEffect(__instance, dinfo);
                
                // 播放阻挡音效
                PlayBlockSound(__instance);
                
                // 返回空结果，跳过原方法
                __result = new DamageWorker.DamageResult();
                
                // 可选：在开发模式下显示日志
                if (Prefs.DevMode)
                {
                    Log.Message($"[DD Armor] {__instance.LabelCap} blocked attack: " +
                        $"Damage={dinfo.Amount}, Penetration={armorPenetration:P0}, " +
                        $"PierceDamage={piercingDamage:F1}, Armor={armorValue:F1}");
                }
                
                return false; // 跳过原TakeDamage方法
            }
            
            // 阻挡失败，继续执行原方法
            if (Prefs.DevMode)
            {
                Log.Message($"[DD Armor] {__instance.LabelCap} failed to block: " +
                    $"Damage={dinfo.Amount}, Penetration={armorPenetration:P0}, " +
                    $"PierceDamage={piercingDamage:F1}, Armor={armorValue:F1}");
            }
            
            return true;
        }
        
        /// <summary>
        /// 显示阻挡效果
        /// </summary>
        private static void ShowBlockEffect(Thing target, DamageInfo dinfo)
        {
            if (!target.Spawned)
                return;
                
            // 显示文字效果
            Vector3 textPos = target.DrawPos + new Vector3(0, 0, 1f);
            MoteMaker.ThrowText(textPos, target.Map, "DD_BlockByMechArmor".Translate(), Color.yellow, 2.5f);
        }
        
        /// <summary>
        /// 播放阻挡音效
        /// </summary>
        private static void PlayBlockSound(Thing target)
        {
            if (!target.Spawned)
                return;
                
            if (BlockSoundDef != null)
            {
                BlockSoundDef.PlayOneShot(new TargetInfo(target.Position, target.Map));
            }
            else
            {
                // 备用音效
                SoundDefOf.MetalHitImportant.PlayOneShot(new TargetInfo(target.Position, target.Map));
            }
        }
    }
}
