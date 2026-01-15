using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;

namespace DivineDiurganate
{
    public class PlaceWorker_TeleporterRange : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            if (Find.CurrentMap == null || WorldRendererUtility.WorldRendered)
                return;

            // 获取传送器属性
            var compProps = def.GetCompProperties<CompProperties_MapTeleporter>();
            if (compProps == null)
                return;

            // 计算基础矩形
            CellRect baseRect = CellRect.CenteredOn(center, compProps.areaSize.x, compProps.areaSize.z);
            
            // 如果是已经存在的建筑，获取它的组件
            CompMapTeleporter existingTeleporter = null;
            if (thing != null)
            {
                existingTeleporter = thing.TryGetComp<CompMapTeleporter>();
            }

            // 准备绘制颜色
            Color drawColor = new Color(0f, 1f, 1f, 0.5f); // 青色，半透明
            Color edgeColor = new Color(0f, 0.5f, 1f, 0.8f); // 蓝色，用于边缘
            
            // 情况1：如果有现有的传送器组件，显示整个组
            if (existingTeleporter != null)
            {
                DrawTeleporterGroup(existingTeleporter, drawColor, edgeColor);
            }
            // 情况2：如果是放置模式，检查附近是否有其他传送器可以连接
            else
            {
                DrawPotentialTeleporterGroup(Find.CurrentMap, def, center, compProps, drawColor, edgeColor);
            }
        }
        
        /// <summary>
        /// 绘制已存在的传送器组
        /// </summary>
        private void DrawTeleporterGroup(CompMapTeleporter teleporter, Color fillColor, Color edgeColor)
        {
            try
            {
                // 获取组的所有单元格
                var groupCells = teleporter.GroupCells;
                if (groupCells == null || groupCells.Count == 0)
                    return;

                // 绘制填充区域
                GenDraw.DrawFieldEdges(groupCells, fillColor);
                
                // 绘制每个成员的位置
                var groupMembers = teleporter.GroupMembers;
                if (groupMembers != null)
                {
                    foreach (var member in groupMembers)
                    {
                        if (member.parent != null && member.parent.Spawned)
                        {
                            // 标记建筑位置
                            Vector3 centerPos = member.parent.TrueCenter();
                            GenDraw.DrawCircleOutline(centerPos, 0.5f, SimpleColor.Yellow);
                            
                            // 如果是领导者，特殊标记
                            var leader = member.Leader;
                            if (leader != null && member == leader)
                            {
                                GenDraw.DrawLineBetween(centerPos + new Vector3(-0.5f, 0, -0.5f), 
                                                       centerPos + new Vector3(0.5f, 0, 0.5f), 
                                                       SimpleColor.Yellow);
                                GenDraw.DrawLineBetween(centerPos + new Vector3(-0.5f, 0, 0.5f), 
                                                       centerPos + new Vector3(0.5f, 0, -0.5f), 
                                                       SimpleColor.Yellow);
                            }
                        }
                    }
                    
                    // 绘制连接线
                    if (groupMembers.Count > 1)
                    {
                        for (int i = 0; i < groupMembers.Count; i++)
                        {
                            for (int j = i + 1; j < groupMembers.Count; j++)
                            {
                                var comp1 = groupMembers[i];
                                var comp2 = groupMembers[j];
                                
                                if (comp1.parent != null && comp2.parent != null && 
                                    comp1.TeleportRect.ExpandedBy(1).Overlaps(comp2.TeleportRect))
                                {
                                    GenDraw.DrawLineBetween(comp1.parent.TrueCenter(), 
                                                          comp2.parent.TrueCenter(), 
                                                          SimpleColor.Blue);
                                }
                            }
                        }
                    }
                }
                
                // 显示区域信息
                ShowAreaInfo(teleporter, groupCells);
            }
            catch
            {
                // 忽略绘制错误
            }
        }
        
        /// <summary>
        /// 绘制潜在的传送器组（放置模式）
        /// </summary>
        private void DrawPotentialTeleporterGroup(Map map, ThingDef def, IntVec3 center, 
                                                 CompProperties_MapTeleporter compProps, 
                                                 Color fillColor, Color edgeColor)
        {
            try
            {
                // 获取地图上所有同类型的传送器
                var existingTeleporters = map.listerThings.ThingsOfDef(def);
                
                // 查找可能与新建筑连接的传送器
                HashSet<IntVec3> potentialCells = new HashSet<IntVec3>();
                List<Thing> potentialNeighbors = new List<Thing>();
                
                // 新建筑的矩形
                CellRect newRect = CellRect.CenteredOn(center, compProps.areaSize.x, compProps.areaSize.z);
                
                // 添加新建筑的单元格
                foreach (var cell in newRect)
                {
                    if (cell.InBounds(map))
                    {
                        potentialCells.Add(cell);
                    }
                }
                
                // 检查现有传送器是否与新建筑相邻
                foreach (var existingThing in existingTeleporters)
                {
                    if (existingThing == null || !existingThing.Spawned)
                        continue;
                        
                    var existingComp = existingThing.TryGetComp<CompMapTeleporter>();
                    if (existingComp == null)
                        continue;
                        
                    if (newRect.ExpandedBy(1).Overlaps(existingComp.TeleportRect))
                    {
                        potentialNeighbors.Add(existingThing);
                        // 添加现有传送器的单元格
                        foreach (var cell in existingComp.TeleportRect)
                        {
                            if (cell.InBounds(map))
                            {
                                potentialCells.Add(cell);
                            }
                        }
                    }
                }
                
                // 绘制单元格
                if (potentialCells.Count > 0)
                {
                    GenDraw.DrawFieldEdges(potentialCells.ToList(), fillColor);
                }
                
                // 绘制连接线
                foreach (var neighbor in potentialNeighbors)
                {
                    if (neighbor != null && neighbor.Spawned)
                    {
                        GenDraw.DrawLineBetween(center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays), 
                                              neighbor.TrueCenter(), 
                                              SimpleColor.Blue);
                        
                        // 标记现有传送器
                        GenDraw.DrawCircleOutline(neighbor.TrueCenter(), 0.5f, SimpleColor.Yellow);
                    }
                }
                
                // 标记新建筑位置
                GenDraw.DrawCircleOutline(center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays), 
                                        0.7f, SimpleColor.Yellow);
                
                // 显示基础信息
                ShowBaseAreaInfo(center, compProps, potentialNeighbors.Count);
            }
            catch
            {
                // 忽略绘制错误
            }
        }
        
        /// <summary>
        /// 显示传送器组的区域信息
        /// </summary>
        private void ShowAreaInfo(CompMapTeleporter teleporter, List<IntVec3> cells)
        {
            if (teleporter == null || teleporter.parent == null || cells == null)
                return;
                
            // 计算区域统计
            int areaSize = cells.Count;
            var groupMembers = teleporter.GroupMembers;
            int memberCount = groupMembers?.Count ?? 0;
            
            // 创建信息字符串
            string infoText = $"传送区域: {areaSize} 格";
            if (memberCount > 1)
            {
                infoText += $"\n连接建筑: {memberCount} 个";
                
                // 显示领导者信息
                var leader = teleporter.Leader;
                if (leader != null && leader.parent != null && leader != teleporter)
                {
                    infoText += "\n（当前不是领导者）";
                }
                else if (leader == teleporter)
                {
                    infoText += "\n（领导者）";
                }
            }
            
            // 在建筑位置显示信息
            Vector3 screenPos = Find.Camera.WorldToScreenPoint(teleporter.parent.TrueCenter());
            screenPos.y = Screen.height - screenPos.y;
            
            // 使用Widgets.Label绘制文本
            Rect textRect = new Rect(screenPos.x + 20, screenPos.y, 200, 60);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            Widgets.Label(textRect, infoText);
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 显示基础区域信息（放置模式）
        /// </summary>
        private void ShowBaseAreaInfo(IntVec3 center, CompProperties_MapTeleporter compProps, int neighborCount)
        {
            // 计算区域大小
            int baseArea = compProps.areaSize.x * compProps.areaSize.z;
            
            // 创建信息字符串
            string infoText = $"基础传送区域: {compProps.areaSize.x}x{compProps.areaSize.z} ({baseArea}格)";
            
            if (neighborCount > 0)
            {
                infoText += $"\n可连接建筑: {neighborCount} 个";
                int totalArea = baseArea * (neighborCount + 1);
                infoText += $"\n预计总区域: {totalArea} 格";
            }
            
            // 在鼠标位置显示信息
            Vector3 screenPos = Find.Camera.WorldToScreenPoint(center.ToVector3Shifted());
            screenPos.y = Screen.height - screenPos.y;
            
            Rect textRect = new Rect(screenPos.x + 20, screenPos.y, 220, 80);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            Widgets.Label(textRect, infoText);
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// 验证放置位置
        /// </summary>
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, 
                                                     Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            // 基础验证
            if (!loc.InBounds(map))
                return false;
                
            // 检查checkingDef是否是ThingDef
            if (!(checkingDef is ThingDef thingDef))
                return true; // 如果不是ThingDef，使用默认验证
            
            // 获取传送器属性
            var compProps = thingDef.GetCompProperties<CompProperties_MapTeleporter>();
            if (compProps == null)
                return true; // 如果没有组件，使用默认验证
            
            // 计算矩形区域
            CellRect teleportRect = CellRect.CenteredOn(loc, compProps.areaSize.x, compProps.areaSize.z);
            
            // 检查矩形是否完全在地图内
            foreach (var cell in teleportRect)
            {
                if (!cell.InBounds(map))
                {
                    return "部分区域超出地图边界";
                }
            }
            
            // 检查矩形内是否有不可摧毁的建筑
            foreach (var cell in teleportRect)
            {
                if (!cell.InBounds(map))
                    continue;
                    
                var things = map.thingGrid.ThingsListAt(cell);
                foreach (var t in things)
                {
                    if (t == thingToIgnore || t == thing)
                        continue;
                        
                    if (t.def.category == ThingCategory.Building)
                    {
                        if (!t.def.destroyable)
                        {
                            return $"区域内有不可摧毁的建筑: {t.Label}";
                        }
                    }
                }
            }
            
            return true;
        }
    }
}
