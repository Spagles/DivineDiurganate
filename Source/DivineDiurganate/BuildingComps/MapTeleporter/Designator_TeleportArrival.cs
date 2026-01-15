using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace DivineDiurganate
{
    public class Designator_TeleportArrival : Designator
    {
        private CompMapTeleporter teleporter;
        private Map targetMap;
        private TeleportLandingMarker marker;
        private List<Thing> thingsToTeleport = new List<Thing>();
        private IntVec3 sourceCenter;
        private List<IntVec3> relativeCells;

        public override string Label => "Designator_TeleportLabel".Translate(); 
        public override string Desc => "Designator_TeleportDesc".Translate();

        public Designator_TeleportArrival(CompMapTeleporter teleporter, Map targetMap, TeleportLandingMarker marker = null)
        {
            this.teleporter = teleporter;
            this.targetMap = targetMap;
            this.marker = marker;
            this.useMouseIcon = true;
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            this.soundSucceeded = SoundDefOf.Designate_PlaceBuilding;
            
            // Cache relative cells from the group
            this.relativeCells = teleporter.GetRelativeGroupCells();
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(targetMap)) return "Designator_TeleportOutofRange".Translate();
            
            // 检查biome是否允许建立基地
            if (teleporter.Props.checkCanBuildBase)
            {
                Tile tile = Find.WorldGrid[targetMap.Tile];
                if (tile.PrimaryBiome == null || !tile.PrimaryBiome.canBuildBase)
                {
                    return $"DD_HellTeleporter_NotAllowBase".Translate();
                }
            }
            
            // Check all cells in the group shape
            foreach (IntVec3 offset in relativeCells)
            {
                IntVec3 cell = loc + offset;
                
                if (!cell.InBounds(targetMap)) return "Designator_TeleportOutofRange".Translate();
                
                // Check map edge
                if (cell.InNoBuildEdgeArea(targetMap))
                {
                    return "Designator_TeleportCloseToRange".Translate();
                }
                
                // Check fog
                if (cell.Fogged(targetMap))
                {
                    return "Designator_TeleportFogCoverd".Translate();
                }
                
                // Check for indestructible buildings
                List<Thing> things = targetMap.thingGrid.ThingsListAt(cell);
                foreach (Thing t in things)
                {
                    if (t.def.category == ThingCategory.Building)
                    {
                        if (!t.def.destroyable)
                        {
                            return $"Designator_TeleportBlockByBuilding".Translate(t.Label);
                        }
                    }
                }
                
                // Check terrain passability
                TerrainDef terrain = cell.GetTerrain(targetMap);
                if (terrain.passability == Traversability.Impassable && !terrain.IsWater)
                {
                     return "Designator_TeleportImpassable".Translate();
                }
            }

            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (marker != null)
            {
                marker.Position = c;
                Find.DesignatorManager.Deselect();
                Find.Selector.ClearSelection();
                Find.Selector.Select(marker);
            }
            else
            {
                teleporter.ConfirmArrival(c, targetMap);
                Find.DesignatorManager.Deselect();
            }
        }

        public override void Selected()
        {
            CacheThings();
            DrawRect();
        }

        public override void SelectedUpdate()
        {
            DrawRect();
            DrawGhosts();
        }
        
        public override void DrawMouseAttachments()
        {
            base.DrawMouseAttachments();
            DrawRect();
        }

        private void DrawRect()
        {
            IntVec3 center = UI.MouseCell();
            List<IntVec3> drawCells = new List<IntVec3>();
            foreach (var offset in relativeCells)
            {
                drawCells.Add(center + offset);
            }
            GenDraw.DrawFieldEdges(drawCells);
        }

        private void CacheThings()
        {
            thingsToTeleport.Clear();
            if (teleporter.parent == null || teleporter.parent.Map == null) return;

            sourceCenter = teleporter.parent.Position;
            Map sourceMap = teleporter.parent.Map;
            
            // Use the group cells directly from the teleporter
            List<IntVec3> groupCells = teleporter.GroupCells;
            
            foreach (IntVec3 cell in groupCells)
            {
                if (!cell.InBounds(sourceMap)) continue;
                foreach (Thing t in sourceMap.thingGrid.ThingsListAt(cell))
                {
                    if (t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item || t.def.category == ThingCategory.Pawn)
                    {
                        if (t != teleporter.parent) thingsToTeleport.Add(t);
                    }
                }
            }
            // Add self (leader)
            thingsToTeleport.Add(teleporter.parent);
        }

        private void DrawGhosts()
        {
            IntVec3 mouseCell = UI.MouseCell();
            if (!mouseCell.InBounds(targetMap)) return;

            foreach (Thing t in thingsToTeleport)
            {
                if (t == null || t.Destroyed) continue;
                if (t.Graphic == null) continue;
                
                // 检查图形类型，如果是Graphic_RandomRotated则跳过
                var graphicType = t.Graphic.GetType();
                if (graphicType.Name.Contains("Graphic_RandomRotated") || graphicType.Name.Contains("Graphic_Cluster"))
                {
                    continue; // 跳过可能导致问题的图形类型
                }
                
                IntVec3 relativePos = t.Position - sourceCenter;
                IntVec3 drawPos = mouseCell + relativePos;
                
                if (drawPos.InBounds(targetMap))
                {
                    try
                    {
                        // 安全地尝试获取幽灵图形
                        var ghostGraphic = GetSafeGhostGraphic(t.Graphic, t.def, Color.white);
                        if (ghostGraphic != null)
                        {
                            ghostGraphic.DrawFromDef(drawPos.ToVector3ShiftedWithAltitude(AltitudeLayer.Blueprint), t.Rotation, t.def);
                        }
                        else
                        {
                            // 如果无法获取幽灵图形，使用简单的方框代替
                            GenDraw.DrawFieldEdges(new List<IntVec3> { drawPos }, Color.white);
                        }
                    }
                    catch
                    {
                        // 忽略绘图错误，显示简单的占位符
                        GenDraw.DrawFieldEdges(new List<IntVec3> { drawPos }, Color.yellow);
                    }
                }
            }
        }
        
        /// <summary>
        /// 安全地获取幽灵图形，避免Graphic_RandomRotated等类型的错误
        /// </summary>
        private Graphic GetSafeGhostGraphic(Graphic graphic, ThingDef thingDef, Color color)
        {
            try
            {
                // 首先尝试正常的获取方法
                return GhostUtility.GhostGraphicFor(graphic, thingDef, color);
            }
            catch (System.ArgumentException ex)
            {
                // 如果出现参数错误，尝试使用简化的图形
                Log.Warning($"[DivineDiurganate] 无法为物体 {thingDef.defName} 创建幽灵图形: {ex.Message}");
                
                // 尝试使用蓝图图形作为替代
                if (thingDef.graphicData != null)
                {
                    try
                    {
                        // 使用基础的Graphic_Single作为替代
                        var simpleGraphic = GraphicDatabase.Get<Graphic_Single>(
                            thingDef.graphicData.texPath, 
                            ShaderDatabase.Transparent, 
                            thingDef.graphicData.drawSize, 
                            color);
                        return simpleGraphic;
                    }
                    catch
                    {
                        // 如果这也失败了，返回null
                        return null;
                    }
                }
                return null;
            }
            catch
            {
                // 其他异常，返回null
                return null;
            }
        }
    }
}
