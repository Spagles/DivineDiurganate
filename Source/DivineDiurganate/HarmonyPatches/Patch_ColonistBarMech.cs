// File: Patches/ColonistBarMechPatch_Combined.cs
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;
using System.Text;

namespace DivineDiurganate
{
    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    public static class Patch_ColonistBarMech_Combined
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<ColonistBar.Entry> ___cachedEntries)
        {
            try
            {
                // 安全检查：只在玩家派系存在时运行
                if (Faction.OfPlayer == null || ___cachedEntries == null)
                    return;

                // 1. 找出所有玩家殖民地的机甲和驾驶员映射关系
                var mechToPilots = new Dictionary<Pawn, List<Pawn>>();
                var pilotToMech = new Dictionary<Pawn, Pawn>();
                
                // 只检查玩家殖民地地图
                foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                {
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        // 只处理玩家派系或玩家托管的单位
                        bool isPlayerControlled = pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer;
                        if (!isPlayerControlled)
                            continue;

                        // 检查是否是机甲
                        if (pawn is DDmechunit mech)
                        {
                            var pilotComp = mech.TryGetComp<CompMechPilotHolder>();
                            if (pilotComp != null && pilotComp.HasPilots)
                            {
                                var pilots = pilotComp.GetPilots()
                                    .Where(p => p.Faction == Faction.OfPlayer || p.HostFaction == Faction.OfPlayer)
                                    .ToList();
                                
                                if (pilots.Count > 0)
                                {
                                    mechToPilots[mech] = pilots;
                                    foreach (var pilot in pilots)
                                    {
                                        pilotToMech[pilot] = mech;
                                    }
                                }
                            }
                        }
                    }
                }

                // 如果没有需要处理的驾驶员，直接返回
                if (pilotToMech.Count == 0 && mechToPilots.Count == 0)
                    return;

                // 2. 创建新的条目列表
                var newEntries = new List<ColonistBar.Entry>();
                var addedMechs = new HashSet<Pawn>();

                // 3. 处理原始条目
                foreach (var entry in ___cachedEntries)
                {
                    var pawn = entry.pawn;
                    
                    // 处理空条目（用于分组分隔）
                    if (pawn == null)
                    {
                        newEntries.Add(entry);
                        continue;
                    }
                    
                    // 只处理玩家派系或玩家托管的单位
                    bool isPlayerControlled = pawn.Faction == Faction.OfPlayer || pawn.HostFaction == Faction.OfPlayer;
                    if (!isPlayerControlled)
                    {
                        // 非玩家单位保留原样
                        newEntries.Add(entry);
                        continue;
                    }

                    // 检查是否是驾驶员
                    if (pilotToMech.ContainsKey(pawn))
                    {
                        // 跳过驾驶员，不添加到列表中
                        // 但我们会在后面添加他们的机甲
                        continue;
                    }

                    // 检查是否是机甲
                    if (mechToPilots.ContainsKey(pawn))
                    {
                        // 机甲且有驾驶员：添加到新列表
                        newEntries.Add(entry);
                        addedMechs.Add(pawn);
                    }
                    else
                    {
                        // 普通殖民者：添加到新列表
                        newEntries.Add(entry);
                    }
                }

                // 4. 确保所有有驾驶员的机甲都被添加（如果还没有）
                foreach (var mech in mechToPilots.Keys)
                {
                    if (!addedMechs.Contains(mech))
                    {
                        // 找到正确的地图和组信息
                        ColonistBar.Entry? existingEntry = ___cachedEntries.FirstOrDefault(e => e.pawn == mech);
                        
                        if (existingEntry.HasValue)
                        {
                            // 如果机甲原本就在列表中，直接添加
                            newEntries.Add(existingEntry.Value);
                        }
                        else
                        {
                            // 否则创建一个新条目
                            // 尝试找到相同地图和派系的参考条目
                            var referenceEntry = ___cachedEntries
                                .Where(e => e.pawn != null)
                                .FirstOrDefault(e => e.map == mech.MapHeld && 
                                                   (e.pawn.Faction == Faction.OfPlayer || e.pawn.HostFaction == Faction.OfPlayer));
                            
                            if (referenceEntry.pawn != null)
                            {
                                newEntries.Add(new ColonistBar.Entry(mech, referenceEntry.map, referenceEntry.group));
                            }
                            else
                            {
                                // 回退方案：使用机甲自己的地图和默认组
                                newEntries.Add(new ColonistBar.Entry(mech, mech.MapHeld, 0));
                            }
                        }
                        
                        addedMechs.Add(mech);
                    }
                }

                // 5. 保持分组顺序
                newEntries = ReorderEntriesByGroup(newEntries);

                // 6. 替换原列表
                ___cachedEntries = newEntries;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] Error in combined ColonistBar patch: {ex}");
                // 出错时不改变原列表，保持原样
            }
        }

        /// <summary>
        /// 重新排序条目以保持正确的分组顺序
        /// </summary>
        private static List<ColonistBar.Entry> ReorderEntriesByGroup(List<ColonistBar.Entry> entries)
        {
            // 按组排序，保持组内顺序
            return entries
                .OrderBy(e => e.group)
                .ThenBy(e => 
                {
                    // 尝试保持原始顺序
                    if (e.pawn != null)
                    {
                        // 对于玩家殖民者，按某种顺序排列
                        // 这里可以根据需要调整
                        return e.pawn.thingIDNumber;
                    }
                    return 0;
                })
                .ToList();
        }

        /// <summary>
        /// 调试方法：显示当前殖民者栏状态
        /// </summary>
        public static void DebugShowColonistBarState()
        {
            if (!DebugSettings.godMode)
                return;

            var bar = Find.ColonistBar;
            var entries = Traverse.Create(bar).Field("cachedEntries").GetValue<List<ColonistBar.Entry>>();
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== 殖民者栏状态 ===");
            sb.AppendLine($"总条目数: {entries.Count}");
            
            int group = -1;
            foreach (var entry in entries)
            {
                if (entry.group != group)
                {
                    sb.AppendLine($"--- 组 {entry.group} ---");
                    group = entry.group;
                }
                
                if (entry.pawn == null)
                {
                    sb.AppendLine("  [空条目]");
                }
                else
                {
                    string type = "未知";
                    if (entry.pawn is DDmechunit)
                        type = "机甲";
                    else if (entry.pawn.RaceProps.Humanlike)
                        type = "人类";
                    else
                        type = "其他";
                    
                    sb.AppendLine($"  {entry.pawn.LabelCap} ({type}) - 地图: {entry.map?.Index ?? -1}");
                }
            }
            
            Log.Message(sb.ToString());
        }
    }
    
    /// <summary>
    /// 额外的补丁，用于在绘制头像时处理机甲的特殊情况
    /// </summary>
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    public static class Patch_ColonistBarDrawer_Mech
    {
        [HarmonyPrefix]
        public static bool Prefix(Rect rect, Pawn colonist, Map pawnMap)
        {
            try
            {
                // 检查是否是机甲
                if (colonist is DDmechunit mech)
                {
                    var pilotComp = mech.TryGetComp<CompMechPilotHolder>();
                    if (pilotComp != null && pilotComp.HasPilots)
                    {
                        // 获取主要驾驶员
                        var primaryPilot = pilotComp.GetPrimaryPilot();
                        if (primaryPilot != null)
                        {
                            // 使用驾驶员的头像来绘制机甲
                            // 这里可以添加机甲的特殊边框或标识
                            DrawMechWithPilot(rect, mech, primaryPilot);
                            return false; // 跳过原版绘制
                        }
                    }
                }
                return true; // 继续原版绘制
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] Error in ColonistBarDrawer patch: {ex}");
                return true;
            }
        }
        
        private static void DrawMechWithPilot(Rect rect, DDmechunit mech, Pawn pilot)
        {
            // 绘制驾驶员的头像
            ColonistBarColonistDrawer.DrawColonist(rect, pilot, mech.MapHeld , false, false);
            
            // 添加机甲标识
            // 可以在角落添加一个小图标
            float iconSize = Mathf.Min(rect.width, rect.height) * 0.25f;
            Rect iconRect = new Rect(rect.x + rect.width - iconSize, rect.y, iconSize, iconSize);
            
            // 使用机甲的图标
            Texture2D mechIcon = ContentFinder<Texture2D>.Get("UI/Icons/MechIcon", false);
            if (mechIcon != null)
            {
                GUI.DrawTexture(iconRect, mechIcon);
            }
            
            // 添加边框颜色（例如蓝色边框表示有机甲）
            Widgets.DrawBox(rect, 2, null);
            
            // 添加驾驶员数量提示
            var pilotComp = mech.TryGetComp<CompMechPilotHolder>();
            if (pilotComp != null && pilotComp.CurrentPilotCount > 1)
            {
                string pilotCountText = $"x{pilotComp.CurrentPilotCount}";
                Vector2 textSize = Text.CalcSize(pilotCountText);
                Rect textRect = new Rect(rect.x, rect.y + rect.height - textSize.y, 
                                        textSize.x, textSize.y);
                GUI.color = Color.yellow;
                Text.Font = GameFont.Tiny;
                Widgets.Label(textRect, pilotCountText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
    }
}
