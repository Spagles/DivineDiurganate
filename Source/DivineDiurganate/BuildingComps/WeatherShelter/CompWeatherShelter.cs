using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 天气庇护所建筑组件（简化版）
    /// 构建一个方形区域，区域内视为受到庇护
    /// 不会受到特定天气事件的影响（如WeatherEvent_HellfireStorm的点燃）
    /// </summary>
    public class CompWeatherShelter : ThingComp
    {
        public CompProperties_WeatherShelter Props => (CompProperties_WeatherShelter)props;
        
        // 是否启用庇护
        private bool shelterEnabled = true;
        
        // 庇护强度
        private float shelterStrength = 1.0f;
        
        // 电力组件（如果有）
        private CompPowerTrader powerComp;
        
        // 激活状态
        private bool isActive = true;
        
        // 庇护区域矩形
        public CellRect ShelterRect => CellRect.CenteredOn(parent.Position, Props.shelterSize.x, Props.shelterSize.z);
        
        // 庇护单元格缓存
        private HashSet<IntVec3> cachedShelterCells;
        private int lastCellsCacheTick = -1;
        
        // 地狱天气效果器
        private Effecter hellWeatherEffecter;
        
        // 地狱天气定义缓存
        private static WeatherDef cachedHellWeatherDef;
        
        // 是否已初始化
        private bool initialized = false;
        
        // 地狱天气检查间隔
        private int hellWeatherCheckInterval = 120; // 每2秒检查一次
        private int nextHellWeatherCheckTick = 0;

        /// <summary>
        /// 是否受庇护
        /// </summary>
        public bool IsSheltered => shelterEnabled && isActive;

        /// <summary>
        /// 是否在地狱天气下
        /// </summary>
        private bool IsHellWeather
        {
            get
            {
                if (parent.Map == null || !parent.Spawned)
                    return false;
                
                return parent.Map.weatherManager.curWeather == GetHellWeatherDef();
            }
        }

        /// <summary>
        /// 获取地狱天气定义
        /// </summary>
        public static WeatherDef GetHellWeatherDef()
        {
            if (cachedHellWeatherDef == null)
            {
                cachedHellWeatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail("DD_Hell_Weather");
            }
            return cachedHellWeatherDef;
        }

        /// <summary>
        /// 获取庇护单元格（缓存）
        /// </summary>
        public HashSet<IntVec3> ShelterCells
        {
            get
            {
                if (lastCellsCacheTick == Find.TickManager.TicksGame && cachedShelterCells != null)
                {
                    return cachedShelterCells;
                }

                lastCellsCacheTick = Find.TickManager.TicksGame;
                cachedShelterCells = new HashSet<IntVec3>();
                
                if (!IsSheltered)
                {
                    return cachedShelterCells;
                }

                foreach (var cell in ShelterRect)
                {
                    if (cell.InBounds(parent.Map))
                    {
                        cachedShelterCells.Add(cell);
                    }
                }
                
                return cachedShelterCells;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shelterEnabled, "shelterEnabled", true);
            Scribe_Values.Look(ref shelterStrength, "shelterStrength", 1.0f);
            Scribe_Values.Look(ref isActive, "isActive", true);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            powerComp = parent.TryGetComp<CompPowerTrader>();
            
            // 初始检查激活状态
            UpdateActiveState();
            
            // 设置地狱天气检查时间
            nextHellWeatherCheckTick = Find.TickManager.TicksGame + Rand.Range(0, hellWeatherCheckInterval);
            
            initialized = true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UpdateActiveState();
            
            // 注册到天气管理器
            RegisterWithWeatherManager();
            
            // 如果加载存档，初始化地狱天气效果器
            if (respawningAfterLoad && IsSheltered && IsHellWeather)
            {
                UpdateHellWeatherEffecter();
            }
        }

        public void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            
            // 清理地狱天气效果器
            CleanupHellWeatherEffecter();
            
            // 从天气管理器注销
            UnregisterFromWeatherManager();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            
            // 清理地狱天气效果器
            CleanupHellWeatherEffecter();
            
            // 从天气管理器注销
            UnregisterFromWeatherManager();
        }

        /// <summary>
        /// 注册到天气管理器
        /// </summary>
        private void RegisterWithWeatherManager()
        {
            if (parent.Map == null || !IsSheltered)
                return;
            
            WeatherShelterManager.RegisterShelter(parent.Map, this);
        }

        /// <summary>
        /// 从天气管理器注销
        /// </summary>
        private void UnregisterFromWeatherManager()
        {
            if (parent.Map == null)
                return;
            
            WeatherShelterManager.UnregisterShelter(parent.Map, this);
        }

        /// <summary>
        /// 更新激活状态
        /// </summary>
        private void UpdateActiveState()
        {
            bool wasActive = isActive;
            
            // 检查电力（如果有电力组件）
            bool hasPower = true;
            if (powerComp != null && Props.requiresPower)
            {
                hasPower = powerComp.PowerOn;
            }
            
            // 检查建筑是否损坏
            bool isIntact = !parent.Destroyed && parent.HitPoints > Props.minHitPointsForShelter;
            
            // 检查是否启用
            bool enabled = shelterEnabled;
            
            isActive = hasPower && isIntact && enabled;
            
            // 如果状态改变，更新缓存和注册
            if (isActive != wasActive)
            {
                InvalidateCache();
                
                if (isActive)
                {
                    RegisterWithWeatherManager();
                }
                else
                {
                    UnregisterFromWeatherManager();
                }
                
                // 更新地狱天气效果器
                UpdateHellWeatherEffecter();
            }
        }

        /// <summary>
        /// 更新地狱天气效果器
        /// </summary>
        private void UpdateHellWeatherEffecter()
        {
            // 清理现有效果器
            CleanupHellWeatherEffecter();
            
            // 检查是否需要创建效果器
            if (ShouldShowHellWeatherEffecter())
            {
                CreateHellWeatherEffecter();
            }
        }

        /// <summary>
        /// 检查是否应该显示地狱天气效果器
        /// </summary>
        private bool ShouldShowHellWeatherEffecter()
        {
            // 基本条件检查
            if (!IsSheltered || !IsHellWeather || parent.Map == null || !parent.Spawned)
                return false;
            
            // 检查是否在当前玩家地图（优化性能）
            return parent.MapHeld == Find.CurrentMap;
        }

        /// <summary>
        /// 创建地狱天气效果器
        /// </summary>
        private void CreateHellWeatherEffecter()
        {
            try
            {
                // 使用CompProperties_WeatherShelter中定义的效果器
                if (Props.hellWeatherEffecter != null)
                {
                    hellWeatherEffecter = Props.hellWeatherEffecter.SpawnAttached(parent, parent.MapHeld);
                }
                else
                {
                    // 如果没有定义效果器，使用默认效果
                    Log.Warning($"[DivineDiurganate] {parent.LabelCap} 没有定义地狱天气效果器");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 创建地狱天气效果器时出错: {ex}");
            }
        }

        /// <summary>
        /// 清理地狱天气效果器
        /// </summary>
        private void CleanupHellWeatherEffecter()
        {
            if (hellWeatherEffecter != null)
            {
                try
                {
                    hellWeatherEffecter.Cleanup();
                    hellWeatherEffecter = null;
                }
                catch (Exception ex)
                {
                    Log.Error($"[DivineDiurganate] 清理地狱天气效果器时出错: {ex}");
                }
            }
        }

        /// <summary>
        /// 使缓存失效
        /// </summary>
        public void InvalidateCache()
        {
            cachedShelterCells = null;
            lastCellsCacheTick = -1;
            WeatherShelterManager.InvalidateCache(parent.Map);
        }

        /// <summary>
        /// 检查单元格是否在庇护区域内
        /// </summary>
        public bool IsCellSheltered(IntVec3 cell)
        {
            if (!IsSheltered)
                return false;
            
            return ShelterCells.Contains(cell);
        }

        /// <summary>
        /// 绘制额外选择覆盖
        /// </summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            
            // 绘制庇护区域边缘
            GenDraw.DrawFieldEdges(ShelterCells.ToList(), Props.shelterColor);
            
            // 如果在地狱天气下，额外绘制警告边界
            if (IsHellWeather && IsSheltered)
            {
                DrawHellWeatherWarning();
            }
        }

        /// <summary>
        /// 绘制地狱天气警告
        /// </summary>
        private void DrawHellWeatherWarning()
        {
            try
            {
                // 绘制闪烁的红色边界
                float pulse = Mathf.Sin(Find.TickManager.TicksGame * 0.05f) * 0.5f + 0.5f;
                Color warningColor = Color.Lerp(Color.red, Color.yellow, pulse);
                
                // 绘制外部边界
                CellRect outerRect = ShelterRect.ExpandedBy(1);
                GenDraw.DrawFieldEdges(outerRect.Cells.ToList(), warningColor * 0.7f);
                
                // 绘制中心点闪烁
                if (Find.TickManager.TicksGame % 40 < 20)
                {
                    Vector3 centerPos = parent.TrueCenter();
                    centerPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    GenDraw.DrawCircleOutline(centerPos, 2.5f * pulse, SimpleColor.Red);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 绘制地狱天气警告时出错: {ex}");
            }
        }

        /// <summary>
        /// 组件每Tick更新
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            
            // 每60Tick检查一次激活状态
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                UpdateActiveState();
            }
            
            // 定期检查地狱天气状态
            if (Find.TickManager.TicksGame >= nextHellWeatherCheckTick)
            {
                CheckHellWeather();
                nextHellWeatherCheckTick = Find.TickManager.TicksGame + hellWeatherCheckInterval;
            }
            
            // 更新地狱天气效果器
            UpdateHellWeatherEffecterTick();
        }

        /// <summary>
        /// 检查地狱天气状态
        /// </summary>
        private void CheckHellWeather()
        {
            try
            {
                bool wasHellWeather = hellWeatherEffecter != null;
                bool shouldBeHellWeather = ShouldShowHellWeatherEffecter();
                
                // 如果状态改变，更新效果器
                if (wasHellWeather != shouldBeHellWeather)
                {
                    UpdateHellWeatherEffecter();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] 检查地狱天气状态时出错: {ex}");
            }
        }

        /// <summary>
        /// 更新地狱天气效果器每Tick
        /// </summary>
        private void UpdateHellWeatherEffecterTick()
        {
            if (hellWeatherEffecter != null)
            {
                try
                {
                    // 更新效果器
                    hellWeatherEffecter.EffectTick(parent, parent);
                    
                    // 如果庇护所不再有效，清理效果器
                    if (!ShouldShowHellWeatherEffecter())
                    {
                        CleanupHellWeatherEffecter();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[DivineDiurganate] 更新地狱天气效果器时出错: {ex}");
                    CleanupHellWeatherEffecter();
                }
            }
        }

        /// <summary>
        /// 额外Gizmos
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            // 显示庇护区域
            Command_Action showAreaCmd = new Command_Action
            {
                defaultLabel = "DD_ShowWeatherShelter",
                defaultDesc = "DD_ShowWeatherShelter_Desc",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/SelectAll"),
                action = () =>
                {
                    // 临时高亮显示庇护区域
                    foreach (var cell in ShelterCells)
                    {
                        if (cell.InBounds(parent.Map))
                        {
                            parent.Map.debugDrawer.FlashCell(cell, 1.5f, null, 250);
                        }
                    }
                }
            };
            yield return showAreaCmd;
        }

        /// <summary>
        /// 接收损害时更新
        /// </summary>
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            
            // 检查是否损害严重到影响庇护
            if (parent.HitPoints <= Props.minHitPointsForShelter)
            {
                UpdateActiveState();
                
                if (!isActive)
                {
                    // 清理地狱天气效果器
                    CleanupHellWeatherEffecter();
                }
            }
        }
    }

    /// <summary>
    /// 天气庇护所组件属性
    /// </summary>
    public class CompProperties_WeatherShelter : CompProperties
    {
        // 庇护区域大小
        public IntVec2 shelterSize = new IntVec2(13, 13);
        
        // 庇护颜色
        public Color shelterColor = new Color(0.2f, 0.8f, 1f, 0.3f);
        public Color borderColor = Color.white;
        
        // 电力要求
        public bool requiresPower = false;
        public float minPowerToShelter = 0f;
        
        // 建筑状态要求
        public int minHitPointsForShelter = 10;
        
        // 普通效果器
        public EffecterDef activeEffecter;
        
        // 地狱天气效果器
        public EffecterDef hellWeatherEffecter;
        
        // 音效
        public SoundDef shelterActiveSound;
        public SoundDef shelterDeactivateSound;
        
        // 地狱天气庇护强度乘数
        public float hellWeatherStrengthMultiplier = 1.5f;
        
        // 地狱天气效果半径
        public float hellWeatherEffectRadius = 15f;

        public CompProperties_WeatherShelter()
        {
            compClass = typeof(CompWeatherShelter);
        }
    }

    /// <summary>
    /// 天气庇护所管理器（静态类，管理所有庇护所）
    /// </summary>
    public static class WeatherShelterManager
    {
        // 按地图存储庇护所
        private static Dictionary<Map, List<CompWeatherShelter>> sheltersByMap = new Dictionary<Map, List<CompWeatherShelter>>();
        
        // 按地图存储庇护单元格缓存
        private static Dictionary<Map, HashSet<IntVec3>> shelteredCellsByMap = new Dictionary<Map, HashSet<IntVec3>>();
        private static int lastCacheUpdateTick = -1;
        
        /// <summary>
        /// 获取地狱天气定义
        /// </summary>
        private static WeatherDef GetHellWeatherDef()
        {
            return CompWeatherShelter.GetHellWeatherDef();
        }
        
        /// <summary>
        /// 检查地图是否处于地狱天气
        /// </summary>
        public static bool IsMapInHellWeather(Map map)
        {
            if (map == null)
                return false;
            
            var hellWeatherDef = GetHellWeatherDef();
            if (hellWeatherDef == null)
                return false;
            
            return map.weatherManager.curWeather == hellWeatherDef;
        }
        
        /// <summary>
        /// 注册庇护所
        /// </summary>
        public static void RegisterShelter(Map map, CompWeatherShelter shelter)
        {
            if (map == null || shelter == null)
                return;
            
            if (!sheltersByMap.ContainsKey(map))
            {
                sheltersByMap[map] = new List<CompWeatherShelter>();
            }
            
            if (!sheltersByMap[map].Contains(shelter))
            {
                sheltersByMap[map].Add(shelter);
                InvalidateCache(map);
            }
        }
        
        /// <summary>
        /// 注销庇护所
        /// </summary>
        public static void UnregisterShelter(Map map, CompWeatherShelter shelter)
        {
            if (map == null || !sheltersByMap.ContainsKey(map))
                return;
            
            sheltersByMap[map].Remove(shelter);
            InvalidateCache(map);
            
            // 如果没有庇护所了，移除地图条目
            if (sheltersByMap[map].Count == 0)
            {
                sheltersByMap.Remove(map);
                shelteredCellsByMap.Remove(map);
            }
        }
        
        /// <summary>
        /// 使缓存失效
        /// </summary>
        public static void InvalidateCache(Map map)
        {
            if (map != null && shelteredCellsByMap.ContainsKey(map))
            {
                shelteredCellsByMap.Remove(map);
            }
        }
        
        /// <summary>
        /// 获取地图上所有庇护单元格
        /// </summary>
        public static HashSet<IntVec3> GetShelteredCells(Map map)
        {
            if (map == null)
                return new HashSet<IntVec3>();
            
            // 使用缓存
            if (shelteredCellsByMap.ContainsKey(map) && lastCacheUpdateTick == Find.TickManager.TicksGame)
            {
                return shelteredCellsByMap[map];
            }
            
            lastCacheUpdateTick = Find.TickManager.TicksGame;
            HashSet<IntVec3> cells = new HashSet<IntVec3>();
            
            if (sheltersByMap.ContainsKey(map))
            {
                foreach (var shelter in sheltersByMap[map])
                {
                    if (shelter.IsSheltered)
                    {
                        foreach (var cell in shelter.ShelterCells)
                        {
                            cells.Add(cell);
                        }
                    }
                }
            }
            
            shelteredCellsByMap[map] = cells;
            return cells;
        }
        
        /// <summary>
        /// 检查单元格是否受庇护
        /// </summary>
        public static bool IsCellSheltered(Map map, IntVec3 cell, Type weatherEventType = null)
        {
            if (map == null || !cell.InBounds(map))
                return false;
            
            // 快速检查缓存
            var shelteredCells = GetShelteredCells(map);
            if (!shelteredCells.Contains(cell))
                return false;
            
            // 如果需要检查特定天气事件，找到庇护该单元格的庇护所
            if (sheltersByMap.ContainsKey(map))
            {
                foreach (var shelter in sheltersByMap[map])
                {
                    if (shelter.IsSheltered && shelter.ShelterCells.Contains(cell))
                    {
                        return true;
                    }
                }
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 获取地图上所有活跃庇护所
        /// </summary>
        public static List<CompWeatherShelter> GetActiveShelters(Map map)
        {
            if (map == null || !sheltersByMap.ContainsKey(map))
                return new List<CompWeatherShelter>();
            
            return sheltersByMap[map].Where(s => s.IsSheltered).ToList();
        }
        
        /// <summary>
        /// 获取地狱天气下的活跃庇护所
        /// </summary>
        public static List<CompWeatherShelter> GetHellWeatherShelters(Map map)
        {
            var activeShelters = GetActiveShelters(map);
            if (!IsMapInHellWeather(map))
            {
                return new List<CompWeatherShelter>();
            }
            
            return activeShelters;
        }
        
        /// <summary>
        /// 检查Thing是否被庇护（静态方法）
        /// </summary>
        public static bool IsThingSheltered(Thing thing, Type weatherEventType = null)
        {
            if (thing == null || thing.Map == null || !thing.Spawned)
                return false;
            
            return IsCellSheltered(thing.Map, thing.Position, weatherEventType);
        }
    }
}
