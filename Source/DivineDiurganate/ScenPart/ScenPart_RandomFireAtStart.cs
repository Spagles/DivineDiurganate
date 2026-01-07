using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 场景部件：随机点燃玩家初始位置附近的可燃物
    /// </summary>
    public class ScenPart_RandomFireAtStart : ScenPart
    {
        private IntRange fireCountRange = new IntRange(1, 3);
        private float radius = 15f;
        private string radiusBuf;
        private bool hasTriggered = false;
        
        // 临时缓存用于编辑界面
        private string fireCountMinBuf;
        private string fireCountMaxBuf;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fireCountRange, "fireCountRange", new IntRange(1, 3));
            Scribe_Values.Look(ref radius, "radius", 15f);
            Scribe_Values.Look(ref hasTriggered, "hasTriggered", false);
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight * 4f + 10f);
            
            // 点火数量范围设置
            Rect countRect = scenPartRect.TopPartPixels(ScenPart.RowHeight * 2f);
            DoFireCountEditInterface(countRect);
            
            // 半径设置
            Rect radiusRect = new Rect(scenPartRect.x, countRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight);
            DoRadiusEditInterface(radiusRect);
            
            // 描述文本
            Rect descRect = new Rect(scenPartRect.x, radiusRect.yMax + 5f, scenPartRect.width, ScenPart.RowHeight);
            DoDescriptionInterface(descRect);
        }

        private void DoFireCountEditInterface(Rect rect)
        {
            Rect labelRect = rect.TopPartPixels(ScenPart.RowHeight);
            Rect fieldRect = rect.BottomPartPixels(ScenPart.RowHeight);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, "FireCountRange".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 最小值和最大值输入框
            Rect minRect = fieldRect.LeftHalf().LeftPart(0.45f);
            Rect separatorRect = new Rect(minRect.xMax, fieldRect.y, fieldRect.width * 0.1f, fieldRect.height);
            Rect maxRect = fieldRect.RightHalf().RightPart(0.45f);
            
            // 最小值
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(minRect.LeftPart(0.3f), "Min".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(minRect.RightPart(0.7f), ref fireCountRange.min, ref fireCountMinBuf, 0, 100);
            
            // 分隔符
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(separatorRect, "-");
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 最大值
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(maxRect.LeftPart(0.3f), "Max".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.TextFieldNumeric(maxRect.RightPart(0.7f), ref fireCountRange.max, ref fireCountMaxBuf, 1, 100);
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

        private void DoDescriptionInterface(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            string desc = "ScenPart_RandomFireAtStart_Description".Translate(
                fireCountRange.min,
                fireCountRange.max,
                radius.ToString("F1")
            );
            Widgets.Label(rect, desc);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override string Summary(Scenario scen)
        {
            return "ScenPart_RandomFireAtStart_Summary".Translate(
                fireCountRange.min,
                fireCountRange.max,
                radius.ToString("F1")
            ).CapitalizeFirst();
        }

        public override void Randomize()
        {
            base.Randomize();
            fireCountRange = new IntRange(Rand.RangeInclusive(1, 5), Rand.RangeInclusive(3, 10));
            if (fireCountRange.min > fireCountRange.max)
            {
                int temp = fireCountRange.min;
                fireCountRange.min = fireCountRange.max;
                fireCountRange.max = temp;
            }
            radius = Rand.Range(8f, 25f);
        }

        /// <summary>
        /// 游戏开始后立即执行
        /// </summary>
        public override void PostGameStart()
        {
            base.PostGameStart();
            
            // 确保只执行一次
            if (hasTriggered)
            {
                return;
            }
            
            // 对每个玩家地图执行点火
            foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                TriggerFiresAtPlayerStart(map);
            }
            
            hasTriggered = true;
        }

        /// <summary>
        /// 在指定地图的玩家起始位置附近触发火灾
        /// </summary>
        private void TriggerFiresAtPlayerStart(Map map)
        {
            if (map == null)
            {
                Log.Warning("ScenPart_RandomFireAtStart: 地图为null");
                return;
            }

            // 获取玩家起始位置 - 使用地图生成时的起始点
            IntVec3 startPosition = GetPlayerStartPosition(map);
            if (!startPosition.IsValid)
            {
                Log.Warning($"ScenPart_RandomFireAtStart: 地图 {map.Index} 中找不到有效的玩家起始位置");
                return;
            }

            // 获取起始位置附近的可燃物
            List<Thing> flammableThings = FindFlammableThingsNear(map, startPosition);
            
            if (flammableThings.Count == 0)
            {
                return;
            }


            // 确定要点燃的数量
            int fireCount = fireCountRange.RandomInRange;
            fireCount = Mathf.Clamp(fireCount, 1, flammableThings.Count);
            
            // 随机选择并点燃
            int firesStarted = 0;
            List<Thing> shuffledList = flammableThings.InRandomOrder().ToList();
            
            foreach (Thing thing in shuffledList.Take(fireCount))
            {
                if (TryStartFire(thing, map))
                {
                    firesStarted++;
                }
            }
        }

        /// <summary>
        /// 获取玩家起始位置
        /// 参考 ScenPart_ScatterThings 的实现
        /// </summary>
        private IntVec3 GetPlayerStartPosition(Map map)
        {
            // 方法1：使用地图生成时的起始点（如果可用）
            if (MapGenerator.PlayerStartSpot.IsValid)
            {
                return MapGenerator.PlayerStartSpot;
            }
            
            // 方法2：尝试获取地图上的第一个殖民者位置
            Pawn firstColonist = map.mapPawns.FreeColonists.FirstOrDefault();
            if (firstColonist != null && firstColonist.Position.IsValid)
            {
                return firstColonist.Position;
            }
            
            // 方法3：使用地图中心作为后备
            if (map.Center.IsValid)
            {
                return map.Center;
            }

            // 最后尝试使用默认位置
            return new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
        }

        /// <summary>
        /// 查找起始位置附近的可燃物
        /// </summary>
        private List<Thing> FindFlammableThingsNear(Map map, IntVec3 center)
        {
            List<Thing> flammableThings = new List<Thing>();
            int radiusInt = Mathf.CeilToInt(radius);

            // 搜索指定半径内的所有单元格
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                // 检查单元格上的所有物品
                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                foreach (Thing thing in things)
                {
                    if (IsFlammable(thing))
                    {
                        flammableThings.Add(thing);
                    }
                }
                
                // 检查建筑物的可燃性
                Building building = map.edificeGrid.InnerArray[map.cellIndices.CellToIndex(cell)];
                if (building != null && IsFlammable(building))
                {
                    flammableThings.Add(building);
                }
            }

            return flammableThings;
        }

        /// <summary>
        /// 判断物体是否可燃
        /// </summary>
        private bool IsFlammable(Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            // 检查物品的可燃性
            if (thing.GetStatValue(StatDefOf.Flammability) > 0.01f)
            {
                return true;
            }

            // 检查建筑
            if (thing is Building building)
            {
                return building.FlammableNow;
            }

            return false;
        }

        /// <summary>
        /// 尝试点燃物体
        /// </summary>
        private bool TryStartFire(Thing thing, Map map)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            try
            {
                // 如果是地形代理，直接在地面上点火
                if (thing is FlammableTerrainProxy terrainProxy)
                {
                    return FireUtility.TryStartFireIn(terrainProxy.Cell, map, Rand.Range(0.1f, 0.3f), terrainProxy);
                }

                // 如果是普通物品，尝试点燃它
                if (thing.FlammableNow)
                {
                    float fireSize = Rand.Range(0.1f, 0.5f);
                    return FireUtility.TryStartFireIn(thing.Position, map, fireSize, thing);
                }

                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error($"ScenPart_RandomFireAtStart: 点燃物体时出错 ({thing?.def?.defName}): {ex}");
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
            if (!hasTriggered && map.IsPlayerHome && Find.TickManager.TicksGame < 1000)
            {
                TriggerFiresAtPlayerStart(map);
                hasTriggered = true;
            }
        }

        public override bool HasNullDefs()
        {
            return base.HasNullDefs();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (fireCountRange.min < 0)
            {
                yield return "Fire count minimum cannot be negative";
            }
            
            if (fireCountRange.max < fireCountRange.min)
            {
                yield return "Fire count maximum cannot be less than minimum";
            }
            
            if (radius <= 0)
            {
                yield return "Radius must be greater than 0";
            }
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ fireCountRange.GetHashCode();
            hashCode = (hashCode * 397) ^ radius.GetHashCode();
            return hashCode;
        }

        /// <summary>
        /// 用于表示可然地形的代理类
        /// </summary>
        private class FlammableTerrainProxy : Thing
        {
            public IntVec3 Cell { get; private set; }
            
            public FlammableTerrainProxy(IntVec3 cell, TerrainDef terrain)
            {
                Cell = cell;
                def = ThingDefOf.Fire; // 使用火的def作为代理
                Position = cell;
            }
            
            public override string ToString()
            {
                return $"FlammableTerrainProxy at {Cell}";
            }
        }
    }
}
