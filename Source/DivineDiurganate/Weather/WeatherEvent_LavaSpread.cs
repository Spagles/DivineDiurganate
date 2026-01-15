using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 岩浆蔓延天气事件
    /// 触发时，地图上已有的随机10格LavaShallow向周围蔓延一圈
    /// </summary>
    public class WeatherEvent_LavaSpread : WeatherEvent
    {
        // 岩浆地形定义
        private static readonly TerrainDef LavaShallowDef = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaShallow");
        
        // 事件是否已过期
        private bool expired = false;
        
        // 是否已执行蔓延
        private bool spreadExecuted = false;
        
        // 蔓延的源单元格列表
        private List<IntVec3> sourceCells = new List<IntVec3>();
        
        // 蔓延的目标单元格列表
        private List<IntVec3> targetCells = new List<IntVec3>();
        
        // 事件持续时间（ticks）
        private int eventDurationTicks = 300; // 5秒
        private int ticksActive = 0;
        
        // 蔓延的视觉效果延迟
        private int spreadEffectDelay = 30; // 0.5秒后开始显示蔓延效果
        private int spreadEffectTicks = 0;
        
        // 是否显示视觉效果
        private bool showVisualEffects = true;
        
        // 蔓延速度（每帧蔓延的单元格数量）
        private int cellsPerTick = 1;

        public override bool Expired => expired;

        public WeatherEvent_LavaSpread(Map map) : base(map)
        {
            // 初始化岩浆地形定义
            InitializeLavaDef();
            
            // 查找地图上现有的浅层岩浆单元格（避开庇护区域）
            FindExistingLavaCells(map);
        }
        
        public WeatherEvent_LavaSpread(Map map, bool showEffects) : base(map)
        {
            showVisualEffects = showEffects;
            InitializeLavaDef();
            FindExistingLavaCells(map);
        }

        /// <summary>
        /// 初始化岩浆地形定义
        /// </summary>
        private void InitializeLavaDef()
        {
            if (LavaShallowDef == null)
            {
                Log.Error("[DivineDiurganate] 找不到LavaShallow地形定义！");
                expired = true;
                return;
            }
        }

        /// <summary>
        /// 查找地图上现有的浅层岩浆单元格（避开庇护区域）
        /// </summary>
        private void FindExistingLavaCells(Map map)
        {
            try
            {
                sourceCells.Clear();
                
                // 遍历整个地图，查找所有LavaShallow地形（排除庇护区域内的）
                for (int x = 0; x < map.Size.x; x++)
                {
                    for (int z = 0; z < map.Size.z; z++)
                    {
                        IntVec3 cell = new IntVec3(x, 0, z);
                        TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
                        
                        if (terrain == LavaShallowDef)
                        {
                            // 检查是否在庇护区域内
                            if (!WeatherShelterManager.IsCellSheltered(map, cell))
                            {
                                sourceCells.Add(cell);
                            }
                        }
                    }
                }
                
                // 如果找不到足够的岩浆单元格，可以创建一些（避开庇护区域）
                if (sourceCells.Count == 0 && map != null)
                {
                    CreateInitialLavaCells(map);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 查找岩浆单元格时出错: {ex}");
            }
        }

        /// <summary>
        /// 创建初始的岩浆单元格（如果地图上没有，避开庇护区域）
        /// </summary>
        private void CreateInitialLavaCells(Map map)
        {
            try
            {
                // 在地图中心周围创建10个岩浆单元格，避开庇护区域
                IntVec3 center = map.Center;
                int createdCells = 0;
                int maxAttempts = 100;
                
                for (int attempt = 0; attempt < maxAttempts && createdCells < 10; attempt++)
                {
                    // 随机选择一个中心周围的单元格
                    IntVec3 cell = center + new IntVec3(
                        Rand.Range(-15, 15),
                        0,
                        Rand.Range(-15, 15)
                    );
                    
                    if (cell.InBounds(map))
                    {
                        // 检查是否在庇护区域内
                        if (WeatherShelterManager.IsCellSheltered(map, cell))
                        {
                            continue; // 跳过庇护区域内的单元格
                        }
                        
                        // 检查是否已经是岩浆
                        if (map.terrainGrid.TerrainAt(cell) == LavaShallowDef)
                        {
                            continue; // 跳过已经是岩浆的单元格
                        }
                        
                        // 设置地形为岩浆
                        map.terrainGrid.SetTerrain(cell, LavaShallowDef);
                        sourceCells.Add(cell);
                        createdCells++;
                        
                        // 显示效果
                        if (showVisualEffects)
                        {
                            CreateLavaEffect(cell, map, 1.5f);
                        }
                    }
                }
                
                if (createdCells < 10)
                {
                    Log.Warning($"[DivineDiurganate] 只创建了 {createdCells} 个岩浆单元格，而不是10个（可能因为庇护区域限制）");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建初始岩浆单元格时出错: {ex}");
            }
        }

        /// <summary>
        /// 触发天气事件
        /// </summary>
        public override void FireEvent()
        {
            if (spreadExecuted)
                return;
            
            // 选择10个随机的岩浆单元格作为蔓延源
            SelectRandomSourceCells();
            
            // 计算蔓延的目标单元格（避开庇护区域）
            CalculateSpreadCells();
            
            spreadExecuted = true;
        }

        /// <summary>
        /// 选择随机的岩浆单元格作为蔓延源
        /// </summary>
        private void SelectRandomSourceCells()
        {
            try
            {
                if (sourceCells.Count == 0)
                {
                    return;
                }
                
                // 随机打乱源单元格列表
                List<IntVec3> shuffledCells = new List<IntVec3>(sourceCells);
                shuffledCells.Shuffle();
                
                // 取前10个（如果不足10个则取全部）
                int count = Mathf.Min(10, shuffledCells.Count);
                List<IntVec3> selected = shuffledCells.GetRange(0, count);
                
                // 清除并重新设置源单元格
                sourceCells.Clear();
                sourceCells.AddRange(selected);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 选择随机源单元格时出错: {ex}");
            }
        }

        /// <summary>
        /// 计算蔓延的目标单元格（避开庇护区域）
        /// </summary>
        private void CalculateSpreadCells()
        {
            try
            {
                targetCells.Clear();
                
                foreach (IntVec3 sourceCell in sourceCells)
                {
                    // 获取周围一圈的单元格（8个方向）
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            // 跳过中心单元格本身
                            if (x == 0 && z == 0)
                                continue;
                            
                            IntVec3 neighborCell = sourceCell + new IntVec3(x, 0, z);
                            
                            // 检查单元格是否在地图范围内
                            if (!neighborCell.InBounds(map))
                                continue;
                            
                            // 检查单元格是否在庇护区域内
                            if (WeatherShelterManager.IsCellSheltered(map, neighborCell))
                            {
                                continue; // 跳过庇护区域内的单元格
                            }
                            
                            // 检查单元格是否已经是岩浆
                            TerrainDef currentTerrain = map.terrainGrid.TerrainAt(neighborCell);
                            if (currentTerrain == LavaShallowDef)
                                continue;
                            
                            // 检查单元格是否适合变为岩浆
                            if (CanConvertToLava(neighborCell))
                            {
                                // 添加到目标单元格列表（使用HashSet避免重复）
                                if (!targetCells.Contains(neighborCell))
                                {
                                    targetCells.Add(neighborCell);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 计算蔓延单元格时出错: {ex}");
            }
        }

        /// <summary>
        /// 检查单元格是否可以转换为岩浆
        /// </summary>
        private bool CanConvertToLava(IntVec3 cell)
        {
            try
            {
                if (!cell.InBounds(map))
                    return false;
                
                // 检查当前地形
                TerrainDef currentTerrain = map.terrainGrid.TerrainAt(cell);
                
                // 不允许在水上创建岩浆（避免冲突）
                if (currentTerrain != null && currentTerrain.IsWater)
                    return false;
                
                // 检查是否有不可摧毁的建筑
                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                foreach (Thing thing in things)
                {
                    if (thing.def.category == ThingCategory.Building)
                    {
                        // 检查建筑是否可摧毁
                        if (!thing.def.destroyable)
                            return false;
                    }
                    
                    // 检查是否有重要物品（避免销毁关键物品）
                    if (thing.def.category == ThingCategory.Item)
                    {
                        // 可以添加特定物品的检查
                        if (thing.def.IsApparel || thing.def.IsWeapon)
                        {
                            // 允许摧毁装备和武器
                            continue;
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 检查单元格是否可以转换为岩浆时出错: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 天气事件更新
        /// </summary>
        public override void WeatherEventTick()
        {
            ticksActive++;
            
            // 延迟执行蔓延效果
            if (!spreadExecuted && ticksActive >= spreadEffectDelay)
            {
                FireEvent();
            }
            
            // 执行蔓延过程（每帧蔓延一定数量的单元格）
            if (spreadExecuted && targetCells.Count > 0 && spreadEffectTicks < targetCells.Count)
            {
                ExecuteSpreadTick();
            }
            
            // 检查事件是否应该过期
            if (ticksActive >= eventDurationTicks)
            {
                expired = true;
            }
        }

        /// <summary>
        /// 执行单次蔓延（每帧调用）
        /// </summary>
        private void ExecuteSpreadTick()
        {
            try
            {
                // 本帧要处理的单元格数量
                int cellsThisTick = Mathf.Min(cellsPerTick, targetCells.Count - spreadEffectTicks);
                
                for (int i = 0; i < cellsThisTick; i++)
                {
                    int index = spreadEffectTicks + i;
                    if (index >= targetCells.Count)
                        break;
                    
                    IntVec3 cell = targetCells[index];
                    
                    // 转换为岩浆地形
                    ConvertCellToLava(cell);
                    
                    // 显示视觉效果
                    if (showVisualEffects)
                    {
                        CreateSpreadEffect(cell);
                    }
                    
                    spreadEffectTicks++;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 执行蔓延时出错: {ex}");
            }
        }

        /// <summary>
        /// 将单元格转换为岩浆地形
        /// </summary>
        private void ConvertCellToLava(IntVec3 cell)
        {
            try
            {
                if (!cell.InBounds(map))
                    return;
                
                // 检查是否在庇护区域内（再次确认）
                if (WeatherShelterManager.IsCellSheltered(map, cell))
                {
                    return;
                }
                
                // 摧毁单元格上的可摧毁物体
                DestroyThingsAtCell(cell);
                
                // 设置地形为浅层岩浆
                map.terrainGrid.SetTerrain(cell, LavaShallowDef);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 转换单元格为岩浆时出错: {ex}");
            }
        }

        /// <summary>
        /// 摧毁单元格上的物体
        /// </summary>
        private void DestroyThingsAtCell(IntVec3 cell)
        {
            try
            {
                if (!cell.InBounds(map))
                    return;
                
                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                
                // 反向遍历，避免修改集合时出现问题
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing thing = things[i];
                    
                    // 跳过不可摧毁的建筑和重要物品
                    if (thing.def.category == ThingCategory.Building && !thing.def.destroyable)
                        continue;
                    
                    // 摧毁物体
                    if (thing.def.destroyable)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                        
                        // 显示摧毁效果
                        if (showVisualEffects)
                        {
                            CreateDestructionEffect(cell, thing);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 摧毁单元格物体时出错: {ex}");
            }
        }

        /// <summary>
        /// 创建岩浆效果
        /// </summary>
        private void CreateLavaEffect(IntVec3 cell, Map map, float size = 1.0f)
        {
            try
            {
                // 创建烟雾效果
                FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, size * 0.8f);
                
                // 创建火花效果
                if (Rand.Chance(0.3f))
                {
                    FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), map);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建岩浆效果时出错: {ex}");
            }
        }

        /// <summary>
        /// 创建蔓延效果
        /// </summary>
        private void CreateSpreadEffect(IntVec3 cell)
        {
            try
            {
                // 创建岩浆蔓延效果
                CreateLavaEffect(cell, map, 1.2f);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建蔓延效果时出错: {ex}");
            }
        }

        /// <summary>
        /// 创建摧毁效果
        /// </summary>
        private void CreateDestructionEffect(IntVec3 cell, Thing thing)
        {
            try
            {
                // 创建爆炸效果
                FleckMaker.Static(cell, map, FleckDefOf.ExplosionFlash, 1.5f);
                
                // 创建烟雾效果
                FleckMaker.ThrowSmoke(cell.ToVector3Shifted(), map, 1.0f);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建摧毁效果时出错: {ex}");
            }
        }

        /// <summary>
        /// 天空目标（可选）
        /// </summary>
        public override SkyTarget SkyTarget => new SkyTarget(1f, new SkyColorSet(Color.red * 0.3f, Color.black, Color.red * 0.1f, 1f), 1f, 1f);

        /// <summary>
        /// 天空目标插值因子
        /// </summary>
        public override float SkyTargetLerpFactor
        {
            get
            {
                // 在事件期间逐渐增加红色调
                float progress = (float)ticksActive / eventDurationTicks;
                return Mathf.Clamp01(progress * 0.3f); // 最大30%的红色天空
            }
        }
    }
    
    /// <summary>
    /// 岩浆蔓延天气事件生成器
    /// </summary>
    public class WeatherEventMaker_LavaSpread : WeatherEventMaker
    {
        // 事件发生的基础概率（每帧）
        public float baseChancePerTick = 0.00002f;
        
        // 需要的最低地图岩浆单元格数量
        public int minLavaCells = 5;
        
        // 需要的最低温度（摄氏度）
        public float minTemperature = 40f;
        
        // 冷却时间（ticks）
        public int cooldownTicks = 180000; // 3天
        private int lastSpreadTick = -999999;
        
        // 只有在特定天气下才触发
        public List<WeatherDef> requiredWeathers;
        
        // 只有在特定生物群系下才触发
        public List<BiomeDef> requiredBiomes;
        
        public void WeatherEventTick(Map map, float strength)
        {
            // 检查冷却时间
            if (Find.TickManager.TicksGame - lastSpreadTick < cooldownTicks)
                return;
            
            // 检查温度条件
            if (map.mapTemperature.OutdoorTemp < minTemperature)
                return;
            
            // 检查天气条件
            if (requiredWeathers != null && requiredWeathers.Count > 0)
            {
                bool hasRequiredWeather = false;
                foreach (var weatherDef in requiredWeathers)
                {
                    if (map.weatherManager.curWeather == weatherDef)
                    {
                        hasRequiredWeather = true;
                        break;
                    }
                }
                
                if (!hasRequiredWeather)
                    return;
            }
            
            // 检查生物群系条件
            if (requiredBiomes != null && requiredBiomes.Count > 0)
            {
                BiomeDef currentBiome = map.Biome;
                if (!requiredBiomes.Contains(currentBiome))
                    return;
            }
            
            // 计算地图上现有的岩浆单元格数量（不包括庇护区域内的）
            int lavaCellCount = CountNonShelteredLavaCells(map);
            if (lavaCellCount < minLavaCells)
                return;
            
            // 计算最终概率（受岩浆数量影响）
            float lavaFactor = Mathf.InverseLerp(minLavaCells, minLavaCells + 20, lavaCellCount);
            float finalChance = baseChancePerTick * strength * lavaFactor;
            
            // 随机检查
            if (Rand.Value < finalChance)
            {
                TriggerLavaSpreadEvent(map);
                lastSpreadTick = Find.TickManager.TicksGame;
            }
        }
        
        /// <summary>
        /// 计算地图上的非庇护区域岩浆单元格数量
        /// </summary>
        private int CountNonShelteredLavaCells(Map map)
        {
            try
            {
                TerrainDef lavaDef = DefDatabase<TerrainDef>.GetNamedSilentFail("LavaShallow");
                if (lavaDef == null)
                    return 0;
                
                int count = 0;
                // 为了提高性能，只检查部分单元格
                for (int i = 0; i < 100; i++)
                {
                    IntVec3 randomCell = CellFinder.RandomCell(map);
                    if (map.terrainGrid.TerrainAt(randomCell) == lavaDef)
                    {
                        // 检查是否在庇护区域内
                        if (!WeatherShelterManager.IsCellSheltered(map, randomCell))
                        {
                            count++;
                        }
                    }
                }
                
                // 估算总数量
                float sampleRatio = 100f / (map.Size.x * map.Size.z);
                return Mathf.RoundToInt(count / sampleRatio);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 计算岩浆单元格数量时出错: {ex}");
                return 0;
            }
        }
        
        /// <summary>
        /// 触发岩浆蔓延事件
        /// </summary>
        private void TriggerLavaSpreadEvent(Map map)
        {
            try
            {
                // 创建天气事件
                WeatherEvent_LavaSpread weatherEvent = new WeatherEvent_LavaSpread(map);
                
                // 添加到地图的天气事件管理器
                map.weatherManager.eventHandler.AddEvent(weatherEvent);
                
                // 触发事件
                weatherEvent.FireEvent();
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 触发岩浆蔓延事件时出错: {ex}");
            }
        }
    }
}
