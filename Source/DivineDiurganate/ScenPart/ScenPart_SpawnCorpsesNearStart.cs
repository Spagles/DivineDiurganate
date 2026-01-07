using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 场景部件：在玩家起始位置附近生成尸体
    /// </summary>
    public class ScenPart_SpawnCorpsesNearStart : ScenPart
    {
        private PawnKindDef pawnKind;
        private IntRange corpseCountRange = new IntRange(1, 3);
        private float radius = 15f;
        private bool freshCorpses = true;
        private bool hasTriggered = false;
        
        // 临时缓存用于编辑界面
        private string corpseCountMinBuf;
        private string corpseCountMaxBuf;
        private string radiusBuf;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref pawnKind, "pawnKind");
            Scribe_Values.Look(ref corpseCountRange, "corpseCountRange", new IntRange(1, 3));
            Scribe_Values.Look(ref radius, "radius", 15f);
            Scribe_Values.Look(ref freshCorpses, "freshCorpses", true);
            Scribe_Values.Look(ref hasTriggered, "hasTriggered", false);
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight * 5f + 10f);
            
            // PawnKind选择按钮
            Rect pawnKindRect = scenPartRect.TopPartPixels(ScenPart.RowHeight);
            DoPawnKindEditInterface(pawnKindRect);
            
            // 尸体数量范围设置
            Rect countRect = new Rect(scenPartRect.x, pawnKindRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight * 2f);
            DoCorpseCountEditInterface(countRect);
            
            // 半径设置
            Rect radiusRect = new Rect(scenPartRect.x, countRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight);
            DoRadiusEditInterface(radiusRect);
            
            // 新鲜尸体复选框
            Rect freshRect = new Rect(scenPartRect.x, radiusRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight);
            DoFreshCorpsesEditInterface(freshRect);
            
            // 描述文本
            Rect descRect = new Rect(scenPartRect.x, freshRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight);
            DoDescriptionInterface(descRect);
        }

        private void DoPawnKindEditInterface(Rect rect)
        {
            // 添加标签
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.3f, rect.height);
            Rect buttonRect = new Rect(labelRect.xMax + 5f, rect.y, rect.width - labelRect.width - 5f, rect.height);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, "Creature".Translate() + ":");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 显示选择的PawnKind或默认文本
            string buttonText = pawnKind?.LabelCap ?? "SelectCreature".Translate();
            if (Widgets.ButtonText(buttonRect, buttonText))
            {
                OpenPawnKindSelectionMenu();
            }
        }

        private void OpenPawnKindSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 获取所有PawnKindDef，排除一些玩家不可见的类型
            foreach (PawnKindDef pkd in DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(pk => pk.race != null && pk.race.race != null))
            {
                string label = pkd.LabelCap;
                
                options.Add(new FloatMenuOption(label, () => {
                    pawnKind = pkd;
                }));
            }
            
            if (options.Any())
            {
                // 按字母顺序排序
                options.SortBy(o => o.Label);
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void DoCorpseCountEditInterface(Rect rect)
        {
            Rect labelRect = rect.TopPartPixels(ScenPart.RowHeight);
            Rect fieldRect = rect.BottomPartPixels(ScenPart.RowHeight);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, "CorpseCountRange".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 最小值和最大值输入框
            Rect minRect = fieldRect.LeftHalf().LeftPart(0.45f);
            Rect separatorRect = new Rect(minRect.xMax, fieldRect.y, fieldRect.width * 0.1f, fieldRect.height);
            Rect maxRect = fieldRect.RightHalf().RightPart(0.45f);
            
            // 最小值
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(minRect.LeftPart(0.3f), "Min".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(minRect.RightPart(0.7f), ref corpseCountRange.min, ref corpseCountMinBuf, 0, 50);
            
            // 分隔符
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(separatorRect, "-");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 最大值
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(maxRect.LeftPart(0.3f), "Max".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(maxRect.RightPart(0.7f), ref corpseCountRange.max, ref corpseCountMaxBuf, 1, 50);
        }

        private void DoRadiusEditInterface(Rect rect)
        {
            Rect labelRect = rect.LeftPart(0.4f);
            Rect fieldRect = rect.RightPart(0.6f);
            
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(labelRect, "Radius".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            Widgets.TextFieldNumeric(fieldRect, ref radius, ref radiusBuf, 1f, 50f);
        }

        private void DoFreshCorpsesEditInterface(Rect rect)
        {
            Rect checkboxRect = rect.LeftPart(0.7f);
            Widgets.CheckboxLabeled(checkboxRect, "FreshCorpses".Translate(), ref freshCorpses);
        }

        private void DoDescriptionInterface(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            string creatureName = pawnKind?.LabelCap ?? "UnknownCreature".Translate();
            string desc = "ScenPart_SpawnCorpsesNearStart_Description".Translate(
                corpseCountRange.min,
                corpseCountRange.max,
                creatureName,
                radius.ToString("F1"),
                freshCorpses ? "Yes" : "No"
            );
            Widgets.Label(rect, desc);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override string Summary(Scenario scen)
        {
            string creatureName = pawnKind?.LabelCap ?? "UnknownCreature".Translate();
            return "ScenPart_SpawnCorpsesNearStart_Summary".Translate(
                corpseCountRange.min,
                corpseCountRange.max,
                creatureName,
                radius.ToString("F1")
            ).CapitalizeFirst();
        }

        public override void Randomize()
        {
            base.Randomize();
            
            // 随机选择PawnKind
            var possiblePawnKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                .Where(pk => pk.race != null && pk.race.race != null)
                .ToList();
            
            if (possiblePawnKinds.Any())
            {
                pawnKind = possiblePawnKinds.RandomElement();
            }
            
            corpseCountRange = new IntRange(Rand.RangeInclusive(1, 5), Rand.RangeInclusive(3, 10));
            if (corpseCountRange.min > corpseCountRange.max)
            {
                int temp = corpseCountRange.min;
                corpseCountRange.min = corpseCountRange.max;
                corpseCountRange.max = temp;
            }
            
            radius = Rand.Range(8f, 25f);
            freshCorpses = Rand.Chance(0.7f); // 70%概率生成新鲜尸体
        }

        /// <summary>
        /// 游戏开始后立即执行
        /// </summary>
        public override void PostGameStart()
        {
            base.PostGameStart();
            
            // 确保只执行一次
            if (hasTriggered || pawnKind == null)
            {
                return;
            }
            
            // 对每个玩家地图执行生成
            foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                SpawnCorpsesAtPlayerStart(map);
            }
            
            hasTriggered = true;
        }

        /// <summary>
        /// 在指定地图的玩家起始位置附近生成尸体
        /// </summary>
        private void SpawnCorpsesAtPlayerStart(Map map)
        {
            if (map == null || pawnKind == null)
            {
                Log.Warning("ScenPart_SpawnCorpsesNearStart: 地图或PawnKind为null");
                return;
            }

            // 获取玩家起始位置
            IntVec3 startPosition = GetPlayerStartPosition(map);
            if (!startPosition.IsValid)
            {
                Log.Warning($"ScenPart_SpawnCorpsesNearStart: 地图 {map.Index} 中找不到有效的玩家起始位置");
                return;
            }

            // 确定要生成的尸体数量
            int corpseCount = corpseCountRange.RandomInRange;

            int corpsesSpawned = 0;
            
            // 为每个尸体寻找一个位置
            for (int i = 0; i < corpseCount; i++)
            {
                IntVec3 spawnCell = FindValidSpawnCell(startPosition, map, radius);
                if (spawnCell.IsValid)
                {
                    if (TrySpawnCorpse(spawnCell, map))
                    {
                        corpsesSpawned++;
                    }
                }
                else
                {
                    Log.Warning($"ScenPart_SpawnCorpsesNearStart: 无法为第{i+1}个尸体找到有效位置");
                }
            }
        }

        /// <summary>
        /// 获取玩家起始位置
        /// </summary>
        private IntVec3 GetPlayerStartPosition(Map map)
        {
            // 使用地图生成时的起始点
            if (MapGenerator.PlayerStartSpot.IsValid)
            {
                return MapGenerator.PlayerStartSpot;
            }
            
            // 尝试获取地图上的第一个殖民者位置
            Pawn firstColonist = map.mapPawns.FreeColonists.FirstOrDefault();
            if (firstColonist != null && firstColonist.Position.IsValid)
            {
                return firstColonist.Position;
            }
            
            // 使用地图中心作为后备
            if (map.Center.IsValid)
            {
                return map.Center;
            }

            // 最后尝试使用默认位置
            return new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
        }

        /// <summary>
        /// 查找有效的生成位置
        /// </summary>
        private IntVec3 FindValidSpawnCell(IntVec3 center, Map map, float searchRadius)
        {
            // 尝试在指定半径内随机寻找有效位置
            for (int i = 0; i < 30; i++) // 最多尝试30次
            {
                IntVec3 randomCell = center + IntVec3Utility.RandomHorizontalOffset(searchRadius);
                if (randomCell.InBounds(map) && randomCell.Walkable(map) && !randomCell.Fogged(map))
                {
                    // 确保没有其他尸体或重要建筑在同一个位置
                    if (map.thingGrid.ThingsAt(randomCell).Any(t => t is Corpse || t is Building))
                    {
                        continue;
                    }
                    
                    return randomCell;
                }
            }
            
            // 如果随机查找失败，尝试在半径内系统地查找
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, searchRadius, true).InRandomOrder())
            {
                if (cell.InBounds(map) && cell.Walkable(map) && !cell.Fogged(map))
                {
                    if (!map.thingGrid.ThingsAt(cell).Any(t => t is Corpse || t is Building))
                    {
                        return cell;
                    }
                }
            }
            
            return IntVec3.Invalid;
        }

        /// <summary>
        /// 尝试在指定位置生成尸体
        /// </summary>
        private bool TrySpawnCorpse(IntVec3 cell, Map map)
        {
            try
            {
                // 创建Pawn
                PawnGenerationRequest request = new PawnGenerationRequest(
                    pawnKind,
                    faction: null, // 无派系，野生动物尸体
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: false
                );
                
                Pawn pawn = PawnGenerator.GeneratePawn(request);
                
                // 杀死Pawn来创建尸体
                if (freshCorpses)
                {
                    // 新鲜尸体：直接杀死，没有伤口
                    HealthUtility.DamageUntilDead(pawn);
                }
                else
                {
                    // 非新鲜尸体：随机添加一些伤口，并设置腐烂程度
                    HealthUtility.DamageUntilDead(pawn);
                    
                    // 随机添加一些额外的伤口
                    int woundCount = Rand.Range(1, 5);
                    for (int i = 0; i < woundCount; i++)
                    {
                        BodyPartRecord part = pawn.RaceProps.body.AllParts.RandomElement();
                        if (part != null)
                        {
                            pawn.TakeDamage(new DamageInfo(
                                DamageDefOf.Cut,
                                Rand.Range(5f, 15f),
                                armorPenetration: 0f,
                                instigator: null,
                                hitPart: part
                            ));
                        }
                    }
                }
                
                // 创建尸体
                Corpse corpse = pawn.Corpse;
                if (corpse != null)
                {
                    // 设置尸体位置并生成
                    GenSpawn.Spawn(corpse, cell, map);
                    
                    // 如果不是新鲜尸体，设置腐烂程度
                    if (!freshCorpses)
                    {
                        CompRottable rottable = corpse.TryGetComp<CompRottable>();
                        if (rottable != null)
                        {
                            // 随机设置腐烂进度（25%到75%之间）
                            rottable.RotProgress = rottable.PropsRot.TicksToRotStart + 
                                                  (int)(rottable.PropsRot.TicksToDessicated * Rand.Range(0.25f, 0.75f));
                        }
                    }
                    
                    return true;
                }
                
                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error($"ScenPart_SpawnCorpsesNearStart: 生成尸体时出错: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 地图加载后检查（用于加载存档）
        /// </summary>
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            
            // 如果是加载存档，并且还没触发过，检查是否应该触发
            if (!hasTriggered && pawnKind != null && map.IsPlayerHome && Find.TickManager.TicksGame < 1000)
            {
                SpawnCorpsesAtPlayerStart(map);
                hasTriggered = true;
            }
        }

        public override bool HasNullDefs()
        {
            return base.HasNullDefs() || pawnKind == null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (pawnKind == null)
            {
                yield return "PawnKind is not selected";
            }
            
            if (corpseCountRange.min < 0)
            {
                yield return "Corpse count minimum cannot be negative";
            }
            
            if (corpseCountRange.max < corpseCountRange.min)
            {
                yield return "Corpse count maximum cannot be less than minimum";
            }
            
            if (radius <= 0)
            {
                yield return "Radius must be greater than 0";
            }
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            if (pawnKind != null)
            {
                hashCode = (hashCode * 397) ^ pawnKind.GetHashCode();
            }
            hashCode = (hashCode * 397) ^ corpseCountRange.GetHashCode();
            hashCode = (hashCode * 397) ^ radius.GetHashCode();
            hashCode = (hashCode * 397) ^ freshCorpses.GetHashCode();
            return hashCode;
        }
    }
}
