// File: Patches/ColonistBarMechPatch_Minimal.cs
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class Patch_ColonistBarMech_Minimal
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<ColonistBar.Entry> ___cachedEntries)
        {
            // 安全检查：只在玩家派系存在时运行
            if (Faction.OfPlayer == null)
                return;
            
            try
            {
                // 只处理玩家殖民者条目
                var playerEntries = ___cachedEntries
                    .Where(e => e.pawn != null && 
                           (e.pawn.Faction == Faction.OfPlayer || e.pawn.HostFaction == Faction.OfPlayer))
                    .ToList();
                
                if (playerEntries.Count == 0)
                    return;
                
                // 收集需要隐藏的驾驶员
                var pilotsToHide = new HashSet<Pawn>();
                
                foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                {
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        if (pawn is DDmechunit mech)
                        {
                            var pilotComp = mech.TryGetComp<CompMechPilotHolder>();
                            if (pilotComp != null && pilotComp.HasPilots)
                            {
                                foreach (var pilot in pilotComp.GetPilots())
                                {
                                    if (pilot.Faction == Faction.OfPlayer || pilot.HostFaction == Faction.OfPlayer)
                                    {
                                        pilotsToHide.Add(pilot);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 过滤掉需要隐藏的驾驶员
                ___cachedEntries = ___cachedEntries
                    .Where(e => e.pawn == null || !pilotsToHide.Contains(e.pawn))
                    .ToList();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] Error in minimal ColonistBar patch: {ex}");
                // 出错时不改变原列表
            }
        }
    }
}
