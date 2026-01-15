using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace DivineDiurganate
{
    public class CompMapTeleporter : ThingComp
    {
        public CompProperties_MapTeleporter Props => (CompProperties_MapTeleporter)props;

        private bool isWarmingUp = false;
        private int warmupTicksLeft = 0;
        private int totalWarmupTicks = 0;
        private GlobalTargetInfo target;
        private TeleportLandingMarker activeMarker;
        
        // 保存传送前的地图引用
        private Map sourceMapToClose = null;
        
        // 保存源地图的TileID，用于后续转换生物群系
        private int sourceMapTileId = -1;

        // 游戏条件组件
        private CompGameConditionTeleporter gameConditionComp;
        private bool gameConditionStarted = false;

        // Group caching
        private List<CompMapTeleporter> cachedGroupMembers;
        private int lastGroupCheckTick = -1;
        
        // Cells caching
        private List<IntVec3> cachedGroupCells;
        private int lastGroupCellsCheckTick = -1;

        public CellRect TeleportRect => CellRect.CenteredOn(parent.Position, Props.areaSize.x, Props.areaSize.z);
        
        public bool IsWarmingUp => isWarmingUp;
        
        public List<CompMapTeleporter> GroupMembers
        {
            get
            {
                if (lastGroupCheckTick == Find.TickManager.TicksGame && cachedGroupMembers != null)
                {
                    return cachedGroupMembers;
                }

                lastGroupCheckTick = Find.TickManager.TicksGame;
                cachedGroupMembers = new List<CompMapTeleporter>();
                var openSet = new Queue<CompMapTeleporter>();
                var closedSet = new HashSet<CompMapTeleporter>();

                openSet.Enqueue(this);
                closedSet.Add(this);

                while (openSet.Count > 0)
                {
                    var currentComp = openSet.Dequeue();
                    cachedGroupMembers.Add(currentComp);

                    var potentialNeighbors = parent.Map.listerThings.ThingsOfDef(parent.def);
                    foreach (var potentialNeighbor in potentialNeighbors)
                    {
                        var neighborComp = potentialNeighbor.TryGetComp<CompMapTeleporter>();
                        if (neighborComp == null || closedSet.Contains(neighborComp)) continue;

                        if (currentComp.TeleportRect.ExpandedBy(1).Overlaps(neighborComp.TeleportRect))
                        {
                            closedSet.Add(neighborComp);
                            openSet.Enqueue(neighborComp);
                        }
                    }
                }
                // Sort by ID to ensure consistent leader
                cachedGroupMembers.SortBy(c => c.parent.thingIDNumber);
                return cachedGroupMembers;
            }
        }

        public List<IntVec3> GroupCells
        {
            get
            {
                if (lastGroupCellsCheckTick == Find.TickManager.TicksGame && cachedGroupCells != null)
                {
                    return cachedGroupCells;
                }

                lastGroupCellsCheckTick = Find.TickManager.TicksGame;
                HashSet<IntVec3> cells = new HashSet<IntVec3>();
                foreach (var member in GroupMembers)
                {
                    foreach (var cell in member.TeleportRect)
                    {
                        if (cell.InBounds(parent.Map))
                        {
                            cells.Add(cell);
                        }
                    }
                }
                cachedGroupCells = cells.ToList();
                return cachedGroupCells;
            }
        }

        public List<IntVec3> GetRelativeGroupCells()
        {
            var cells = GroupCells;
            var center = parent.Position;
            return cells.Select(c => c - center).ToList();
        }

        public CompMapTeleporter Leader
        {
            get
            {
                var members = GroupMembers;
                if (members.Count == 0) return this;
                return members[0];
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isWarmingUp, "isWarmingUp", false);
            Scribe_Values.Look(ref warmupTicksLeft, "warmupTicksLeft", 0);
            Scribe_Values.Look(ref totalWarmupTicks, "totalWarmupTicks", 0);
            Scribe_TargetInfo.Look(ref target, "target");
            Scribe_References.Look(ref activeMarker, "activeMarker");
            Scribe_Values.Look(ref gameConditionStarted, "gameConditionStarted", false);
            Scribe_Values.Look(ref sourceMapTileId, "sourceMapTileId", -1);
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            gameConditionComp = parent.TryGetComp<CompGameConditionTeleporter>();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            
            // Draw the combined field edges
            GenDraw.DrawFieldEdges(GroupCells, Color.cyan);

            var leader = Leader;
            if (leader != null)
            {
                // Mark the leader clearly
                Vector3 center = leader.parent.TrueCenter();
                GenDraw.DrawLineBetween(center + new Vector3(-1f, 0, -1f), center + new Vector3(1f, 0, 1f), SimpleColor.Yellow);
                GenDraw.DrawLineBetween(center + new Vector3(-1f, 0, 1f), center + new Vector3(1f, 0, -1f), SimpleColor.Yellow);
                GenDraw.DrawCircleOutline(center, 1.5f, SimpleColor.Yellow);

                // Draw lines from members to leader
                foreach (var member in GroupMembers)
                {
                    if (member != leader)
                    {
                        GenDraw.DrawLineBetween(leader.parent.TrueCenter(), member.parent.TrueCenter(), SimpleColor.Cyan);
                    }
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Leader == this && isWarmingUp)
            {
                warmupTicksLeft--;
                
                // 每10%进度显示一次特效
                int percentInterval = totalWarmupTicks / 10;
                if (percentInterval > 0 && warmupTicksLeft % percentInterval == 0)
                {
                    foreach (var member in GroupMembers)
                    {
                        Props.warmupEffecter?.Spawn(member.parent, member.parent.Map).Cleanup();
                    }
                    
                    // 显示进度
                    float progress = 1f - (float)warmupTicksLeft / totalWarmupTicks;
                    if (progress > 0.9f)
                    {
                        Messages.Message($"DD_HellTeleporter_Willfinish".Translate(Mathf.RoundToInt(progress * 100)), 
                            parent, MessageTypeDefOf.SilentInput);
                    }
                }

                if (warmupTicksLeft <= 0)
                {
                    TryTeleport();
                    isWarmingUp = false;
                    StopGameCondition();
                }
            }
        }

        // 在建筑上显示剩余时间
        public override string CompInspectStringExtra()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            if (isWarmingUp)
            {
                float daysLeft = (float)warmupTicksLeft / 60000f; // 60000 ticks = 1天
                sb.AppendLine($"DD_HellTeleporter_Daysleft".Translate(daysLeft.ToString("F2")));
            }
            
            string baseStr = base.CompInspectStringExtra();
            if (!string.IsNullOrEmpty(baseStr))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(baseStr);
            }
            
            return sb.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            // Only the leader provides the gizmos
            if (Leader != this)
                yield break;

            if (isWarmingUp)
            {
                float progress = 1f - (float)warmupTicksLeft / totalWarmupTicks;
                float daysLeft = (float)warmupTicksLeft / 60000f; // 60000 ticks = 1天
                
                string desc = $"DD_HellTeleporter_TeleCancelDesc".Translate(
                    Mathf.RoundToInt(progress * 100), 
                    daysLeft.ToString("F2"));

                if (gameConditionStarted && Props.warmupGameConditionDef != null)
                {
                    desc += $"DD_HellTeleporter_TeleCancelStatue".Translate(Props.warmupGameConditionDef.label);
                }
                
                Command_Action cancelCmd = new Command_Action
                {
                    defaultLabel = "DD_HellTeleporter_TeleCancel".Translate(),
                    defaultDesc = desc,
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = CancelTeleport
                };
                yield return cancelCmd;
            }
            else if (activeMarker != null && !activeMarker.Destroyed)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_HellTeleporter_TeleCancel".Translate(),
                    defaultDesc = "DD_HellTeleporter_TeleCancelRemoveMark".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                    action = CancelTeleport
                };
            }
            else
            {
                string reason = GetDisabledReason();
                Command_Action teleportCmd = new Command_Action
                {
                    defaultLabel = "DD_HellTeleporter_TeleStart".Translate(),
                    defaultDesc = GetDescription(),
                    icon = ContentFinder<Texture2D>.Get("DivineDiurganate/UI/Commands/DD_HellTeleporter_TeleStart"),
                    action = StartTargeting,
                    disabledReason = reason
                };
                
                if (!string.IsNullOrEmpty(reason))
                {
                    teleportCmd.Disable(reason);
                }
                
                yield return teleportCmd;
            }
        }

        private string GetDisabledReason()
        {
            if (Props.requiredResearch != null && !Props.requiredResearch.IsFinished)
            {
                return $"DD_HellTeleporter_TeleStartNeedResearch".Translate(Props.requiredResearch.label);
            }
            
            return null;
        }

        private string GetDescription()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("DD_HellTeleporter_TeleStartBaseDesc".Translate());
            sb.AppendLine($"DD_HellTeleporter_TeleStartArea".Translate(Props.areaSize.x, Props.areaSize.z));
            sb.AppendLine($"DD_HellTeleporter_TeleStartTime".Translate(Props.daysPerDistance.ToString("F2")));
            
            if (Props.requiredResearch != null)
            {
                sb.AppendLine().AppendLine($"DD_HellTeleporter_TeleStartNeedResearch".Translate(Props.requiredResearch.label));
            }
            
            if (Props.checkCanBuildBase)
            {
                sb.AppendLine().AppendLine("DD_HellTeleporter_TeleStartOnlyCanBuildBase".Translate());
            }
            
            return sb.ToString();
        }

        private void StartTargeting()
        {
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(parent));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(ChoseWorldTarget, true, null, true, null, null);
        }
        
        private bool ChoseWorldTarget(GlobalTargetInfo targetInfo)
        {
            if (!targetInfo.IsValid)
            {
                Messages.Message("DD_HellTeleporter_NotVaildTile".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // 检查目标地图是否可以建立基地
            if (Props.checkCanBuildBase)
            {
                Tile tile = Find.WorldGrid[targetInfo.Tile];
                if (tile.PrimaryBiome == null || !tile.PrimaryBiome.canBuildBase)
                {
                    Messages.Message($"DD_HellTeleporter_NotAllowBase".Translate(tile.PrimaryBiome?.label ?? "未知"),
                        MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            this.target = targetInfo;

            MapParent mapParent = Find.WorldObjects.MapParentAt(targetInfo.Tile);

            if (mapParent == null)
            {
                // 尝试建立新基地 - 修正：SettleUtility.AddNewHome返回的是Settlement，需要检查是否成功
                try
                {
                    // 使用SettleUtility的静态方法创建新基地
                    Settlement settlement = SettleUtility.AddNewHome(targetInfo.Tile, Faction.OfPlayer);
                    if (settlement != null)
                    {
                        mapParent = settlement;
                    }
                    else
                    {
                        Messages.Message("DD_HellTeleporter_NotAllowBase".Translate(), MessageTypeDefOf.RejectInput);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"建立新基地时出错: {ex}");
                    Messages.Message("DD_HellTeleporter_NotAllowBase".Translate(), MessageTypeDefOf.RejectInput);
                    return false;
                }
            }
            if (mapParent != null)
            {
                if (!mapParent.HasMap)
                {
                    IntVec3 mapSize = Find.World.info.initialMapSize;
                    GetOrGenerateMapUtility.GetOrGenerateMap(targetInfo.Tile, mapSize, null);
                }
                if (mapParent.HasMap)
                {
                    CameraJumper.TryJump(mapParent.Map.Center, mapParent.Map);

                    if (activeMarker != null && !activeMarker.Destroyed)
                    {
                        activeMarker.Destroy();
                    }
                    activeMarker = (TeleportLandingMarker)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("DD_TeleportLandingMarker"));
                    activeMarker.sourceThing = parent;
                    GenSpawn.Spawn(activeMarker, mapParent.Map.Center, mapParent.Map);

                    Find.Selector.ClearSelection();
                    Find.Selector.Select(activeMarker);
                    Find.DesignatorManager.Select(new Designator_TeleportArrival(this, mapParent.Map, activeMarker));

                    return true;
                }
            }
            return false;
        }

        public void ConfirmArrival(IntVec3 cell, Map map)
        {
            this.target = new GlobalTargetInfo(cell, map);
            StartWarmup();
        }

        private void StartWarmup()
        {
            // 计算距离并确定预热时间
            Map sourceMap = parent.Map;
            int sourceTile = sourceMap.Tile;
            int targetTile = target.Tile;
            
            // 保存源地图的TileID，用于后续转换生物群系
            sourceMapTileId = sourceTile;
            
            // 计算世界地图上的距离
            float distance = Find.WorldGrid.ApproxDistanceInTiles(sourceTile, targetTile);
            
            // 计算总预热时间 = 基础时间 + 距离 × 单位距离所需天数 × 每天tick数
            // 1天 = 60000 ticks
            totalWarmupTicks = Props.warmupTicks + Mathf.RoundToInt(distance * Props.daysPerDistance * 60000f);
            warmupTicksLeft = totalWarmupTicks;
            
            isWarmingUp = true;
            
            // 启动游戏条件（如果有配置）
            StartGameCondition();
            
            // 显示预热开始信息（以天为单位）
            float totalDays = (float)totalWarmupTicks / 60000f;
            string message = $"DD_HellTeleporter_WarmingMessage".Translate(
                distance.ToString("F1"), 
                totalDays.ToString("F2"));
            
            Messages.Message(message, parent, MessageTypeDefOf.NeutralEvent);
            
            Props.warmupSound?.PlayOneShot(parent);
        }

        // 启动游戏条件
        private void StartGameCondition()
        {
            if (Props.warmupGameConditionDef == null) return;
            
            // 方式1：使用游戏条件组件（如果存在）
            if (gameConditionComp != null)
            {
                // CompGameConditionTeleporter会自动处理，我们只需要标记已开始
                gameConditionStarted = true;
            }
            // 方式2：直接创建游戏条件（备用方式）
            else if (parent.Map != null)
            {
                // 创建游戏条件
                GameCondition gameCondition = GameConditionMaker.MakeCondition(Props.warmupGameConditionDef);
                gameCondition.Duration = totalWarmupTicks;
                gameCondition.conditionCauser = parent;
                gameCondition.hideSource = Props.hideSource;
                gameCondition.suppressEndMessage = true;
                
                parent.Map.gameConditionManager.RegisterCondition(gameCondition);
                gameConditionStarted = true;
            }
        }
        
        // 停止游戏条件
        private void StopGameCondition()
        {
            if (!gameConditionStarted) return;
            
            // 方式1：使用游戏条件组件
            if (gameConditionComp != null)
            {
                gameConditionComp.StopAllConditions();
            }
            // 方式2：直接移除游戏条件
            else if (parent.Map != null && Props.warmupGameConditionDef != null)
            {
                GameCondition condition = parent.Map.GameConditionManager.GetActiveCondition(Props.warmupGameConditionDef);
                if (condition != null)
                {
                    condition.End();
                }
            }
            
            gameConditionStarted = false;
        }

        private void CancelTeleport()
        {
            isWarmingUp = false;
            warmupTicksLeft = 0;
            totalWarmupTicks = 0;
            
            // 停止游戏条件
            StopGameCondition();
            
            if (activeMarker != null && !activeMarker.Destroyed)
            {
                activeMarker.Destroy();
                activeMarker = null;
            }
        }

        private void TryTeleport()
        {
            if (!target.IsValid)
            {
                Messages.Message("DD_HellTeleporter_NotVaildTarget".Translate(), parent, MessageTypeDefOf.RejectInput);
                CancelTeleport();
                return;
            }

            Map targetMap = target.Map;
            IntVec3 targetCell = target.Cell;

            if (targetMap == null)
            {
                targetMap = GetOrGenerateTargetMap(target.Tile);
                if (targetMap == null)
                {
                    Messages.Message("DD_HellTeleporter_SpawnMapFail".Translate(), parent, MessageTypeDefOf.RejectInput);
                    CancelTeleport();
                    return;
                }
                targetCell = targetMap.Center;
            }

            // 再次检查目标地图是否允许建立基地
            if (Props.checkCanBuildBase)
            {
                Tile tile = Find.WorldGrid[targetMap.Tile];
                if (tile.PrimaryBiome == null || !tile.PrimaryBiome.canBuildBase)
                {
                    Messages.Message($"DD_HellTeleporter_NotAllowBase".Translate(), 
                        parent, MessageTypeDefOf.RejectInput);
                    CancelTeleport();
                    return;
                }
            }

            // 保存传送前的地图引用
            sourceMapToClose = parent.Map;
            
            // 执行传送
            TeleportContents(targetMap, targetCell);
            
            // 传送完成后转换旧地图生物群系
            ConvertSourceMapBiomeToHell();
            
            // 强制关闭源地图
            ForceCloseSourceMap();
        }
        
        private Map GetOrGenerateTargetMap(int tile)
        {
            MapParent mapParent = Find.WorldObjects.MapParentAt(tile);
            if (mapParent == null)
            {
                try
                {
                    Settlement settlement = SettleUtility.AddNewHome(tile, Faction.OfPlayer);
                    if (settlement == null)
                    {
                        return null;
                    }
                    mapParent = settlement;
                }
                catch (Exception ex)
                {
                    Log.Error($"建立新基地时出错: {ex}");
                    return null;
                }
            }
            if (mapParent != null)
            {
                if (!mapParent.HasMap)
                {
                    IntVec3 mapSize = Find.World.info.initialMapSize;
                    return GetOrGenerateMapUtility.GetOrGenerateMap(tile, mapSize, null);
                }
                return mapParent.Map;
            }
            return null;
        }

        private struct ThingToTeleport
        {
            public Thing thing;
            public IntVec3 relativePos;
            public bool isColonistPawn; // 是否为玩家殖民者
        }

        private void TeleportContents(Map targetMap, IntVec3 targetCenter)
        {
            // 注意：此时sourceMapToClose已经保存了传送前的地图引用
            Map sourceMap = sourceMapToClose ?? parent.Map; // 使用保存的源地图
            List<IntVec3> cells = GroupCells;
            IntVec3 center = parent.Position;
            
            List<ThingToTeleport> allThingsToTeleport = new List<ThingToTeleport>();
            List<ThingToTeleport> colonistPawnsToTeleport = new List<ThingToTeleport>();
            List<ThingToTeleport> otherThingsToTeleport = new List<ThingToTeleport>();
            
            List<Pair<IntVec3, TerrainDef>> terrainToTeleport = new List<Pair<IntVec3, TerrainDef>>();

            // 1. 收集数据
            HashSet<Thing> collectedThings = new HashSet<Thing>();
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(sourceMap)) continue;

                terrainToTeleport.Add(new Pair<IntVec3, TerrainDef>(cell - center, cell.GetTerrain(sourceMap)));

                List<Thing> thingList = sourceMap.thingGrid.ThingsListAt(cell);
                for (int i = thingList.Count - 1; i >= 0; i--)
                {
                    Thing t = thingList[i];
                    if (!collectedThings.Contains(t) &&
                        (t.def.category == ThingCategory.Item || 
                         t.def.category == ThingCategory.Pawn || 
                         t.def.category == ThingCategory.Building))
                    {
                        if (!t.def.destroyable) continue;
                        
                        collectedThings.Add(t);
                        
                        // 检查是否为玩家殖民者
                        bool isColonist = false;
                        if (t is Pawn pawn && pawn.Faction == Faction.OfPlayer && pawn.IsColonist)
                        {
                            isColonist = true;
                        }
                        
                        var thingToTeleport = new ThingToTeleport { 
                            thing = t, 
                            relativePos = t.Position - center,
                            isColonistPawn = isColonist
                        };
                        
                        allThingsToTeleport.Add(thingToTeleport);
                        
                        if (isColonist)
                        {
                            colonistPawnsToTeleport.Add(thingToTeleport);
                        }
                        else
                        {
                            otherThingsToTeleport.Add(thingToTeleport);
                        }
                    }
                }
            }
            
            // 2. 准备传送 (PreSwapMap) - 所有物体
            foreach (var data in allThingsToTeleport) 
            {
                if (data.thing != null && !data.thing.Destroyed)
                    data.thing.PreSwapMap();
            }

            // 3. 从源地图移除非殖民者物体 (DeSpawn)
            foreach (var data in otherThingsToTeleport)
            {
                if (data.thing != null && data.thing.Spawned) 
                    data.thing.DeSpawn(DestroyMode.WillReplace);
            }

            // 4. 修改地形
            foreach (var pair in terrainToTeleport)
            {
                IntVec3 newPos = targetCenter + pair.First;
                newPos = newPos.ClampInsideMap(targetMap);

                List<Thing> targetThings = targetMap.thingGrid.ThingsListAt(newPos);
                for (int i = targetThings.Count - 1; i >= 0; i--)
                {
                    if (targetThings[i].def.destroyable) targetThings[i].Destroy();
                }

                if (pair.Second != null)
                {
                    targetMap.terrainGrid.SetTerrain(newPos, pair.Second);
                    sourceMap.terrainGrid.SetTerrain(center + pair.First, TerrainDefOf.Soil);
                }
            }

            // 5. 放置非殖民者物体到新地图 (Spawn)
            foreach (var data in otherThingsToTeleport)
            {
                if (data.thing == null || data.thing.Destroyed) continue;
                IntVec3 newPos = targetCenter + data.relativePos;
                newPos = newPos.ClampInsideMap(targetMap);
                GenSpawn.Spawn(data.thing, newPos, targetMap, data.thing.Rotation);
            }

            // 6. 传送后处理非殖民者物体 (PostSwapMap)
            foreach (var data in otherThingsToTeleport)
            {
                if (data.thing != null && !data.thing.Destroyed) 
                    data.thing.PostSwapMap();
            }

            // 7. 逐个传送殖民者（避免ideo职位问题）
            if (colonistPawnsToTeleport.Count > 0)
            {
                foreach (var data in colonistPawnsToTeleport)
                {
                    if (data.thing == null || data.thing.Destroyed) continue;
                    
                    Pawn colonist = data.thing as Pawn;
                    if (colonist == null) continue;
                    
                    try
                    {
                        // 记录殖民者信息
                        string colonistName = colonist.LabelShortCap;
                        
                        // 单独为每个殖民者执行传送流程
                        if (colonist.Spawned) 
                            colonist.DeSpawn(DestroyMode.WillReplace);
                        
                        IntVec3 newPos = targetCenter + data.relativePos;
                        newPos = newPos.ClampInsideMap(targetMap);
                        
                        GenSpawn.Spawn(colonist, newPos, targetMap, colonist.Rotation);
                        colonist.PostSwapMap();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"传送殖民者 {data.thing?.LabelShortCap ?? "未知"} 时出错: {ex}");
                    }
                }
            }

            // 8. 传送后处理殖民者
            foreach (var data in colonistPawnsToTeleport)
            {
                if (data.thing != null && !data.thing.Destroyed) 
                    data.thing.PostSwapMap();
            }

            // 9. 完成
            CameraJumper.TryJump(targetCenter, targetMap);
            Props.teleportSound?.PlayOneShot(new TargetInfo(targetCenter, targetMap, false));
        }

        /// <summary>
        /// 转换源地图的生物群系为地狱生物群系
        /// </summary>
        private void ConvertSourceMapBiomeToHell()
        {
            try
            {
                // 获取地狱生物群系定义
                BiomeDef hellBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("DD_Hell_Biome");
                if (hellBiome == null)
                {
                    Log.Error("[DivineDiurganate] 找不到地狱生物群系定义: DD_Hell_Biome");
                    return;
                }

                // 获取源地图的Tile
                if (sourceMapTileId < 0 && sourceMapToClose != null)
                {
                    sourceMapTileId = sourceMapToClose.Tile;
                }
                
                if (sourceMapTileId < 0)
                {
                    Log.Warning("[DivineDiurganate] 无法获取源地图的TileID");
                    return;
                }

                // 获取世界网格
                WorldGrid worldGrid = Find.WorldGrid;
                if (worldGrid == null)
                {
                    Log.Error("[DivineDiurganate] 无法获取世界网格");
                    return;
                }

                // 获取Tile
                if (sourceMapTileId >= worldGrid.TilesCount)
                {
                    Log.Error($"[DivineDiurganate] 无效的TileID: {sourceMapTileId}");
                    return;
                }

                Tile tile = worldGrid[sourceMapTileId];
                if (tile == null)
                {
                    Log.Error($"[DivineDiurganate] 无法获取Tile: {sourceMapTileId}");
                    return;
                }

                // 保存原始生物群系（可选）
                BiomeDef originalBiome = tile.PrimaryBiome;
                
                // 转换生物群系为地狱
                tile.PrimaryBiome = hellBiome;
                
                // 记录转换
                string originalName = originalBiome?.label ?? "未知";

                // 创建转换特效（可选）
                CreateBiomeConversionEffects(sourceMapTileId);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 转换生物群系时出错: {ex}");
            }
        }
        
        /// <summary>
        /// 创建生物群系转换特效
        /// </summary>
        private void CreateBiomeConversionEffects(int tileId)
        {
            try
            {
                // 在世界地图上显示转换特效
                Vector3 tilePos = Find.WorldGrid.GetTileCenter(tileId);

                // 播放声音
                SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera();
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建生物群系转换特效时出错: {ex}");
            }
        }

        /// <summary>
        /// 强制关闭传送前的地图，无论剩下什么
        /// </summary>
        private void ForceCloseSourceMap()
        {
            if (sourceMapToClose == null)
            {
                Log.Warning("[DivineDiurganate] 尝试关闭源地图，但sourceMapToClose为null");
                return;
            }
            
            // 获取源地图的MapParent
            MapParent sourceMapParent = Find.WorldObjects.MapParentAt(sourceMapToClose.Tile);
            
            if (sourceMapParent != null)
            {
                // 强制移除地图
                Find.WorldObjects.Remove(sourceMapParent);
                
                // 如果这是当前活跃的地图，切换到世界视图
                if (Current.Game.CurrentMap == sourceMapToClose)
                {
                    CameraJumper.TryHideWorld();
                }
                
                // 清理引用
                sourceMapToClose = null;
            }
        }
    }
}
