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

        public override string Label => "选择传送到达点";
        public override string Desc => "在地图上选择一个位置作为传送到达点";

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
            if (!loc.InBounds(targetMap)) return "位置超出地图边界";
            
            // 检查biome是否允许建立基地
            if (teleporter.Props.checkCanBuildBase)
            {
                Tile tile = Find.WorldGrid[targetMap.Tile];
                if (tile.PrimaryBiome == null || !tile.PrimaryBiome.canBuildBase)
                {
                    return $"此地图类型 '{tile.PrimaryBiome?.label ?? "未知"}' 不允许建立基地";
                }
            }
            
            // Check all cells in the group shape
            foreach (IntVec3 offset in relativeCells)
            {
                IntVec3 cell = loc + offset;
                
                if (!cell.InBounds(targetMap)) return "部分区域超出地图边界";
                
                // Check map edge
                if (cell.InNoBuildEdgeArea(targetMap))
                {
                    return "位置过于靠近地图边缘";
                }
                
                // Check fog
                if (cell.Fogged(targetMap))
                {
                    return "位置被战争迷雾覆盖";
                }
                
                // Check for indestructible buildings
                List<Thing> things = targetMap.thingGrid.ThingsListAt(cell);
                foreach (Thing t in things)
                {
                    if (t.def.category == ThingCategory.Building)
                    {
                        if (!t.def.destroyable)
                        {
                            return $"位置被不可摧毁的建筑 '{t.Label}' 阻挡";
                        }
                    }
                }
                
                // Check terrain passability
                TerrainDef terrain = cell.GetTerrain(targetMap);
                if (terrain.passability == Traversability.Impassable && !terrain.IsWater)
                {
                     return "地形不可通行";
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
                
                IntVec3 relativePos = t.Position - sourceCenter;
                IntVec3 drawPos = mouseCell + relativePos;
                
                if (drawPos.InBounds(targetMap))
                {
                    try
                    {
                        GhostUtility.GhostGraphicFor(t.Graphic, t.def, Color.white).DrawFromDef(drawPos.ToVector3ShiftedWithAltitude(AltitudeLayer.Blueprint), t.Rotation, t.def);
                    }
                    catch
                    {
                        // Ignore drawing errors to prevent UI crash
                    }
                }
            }
        }
    }
}
