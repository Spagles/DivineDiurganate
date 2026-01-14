using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 地狱焚风天气事件 - 随机选择着火中心，点燃周围12x12区域的可燃物品
    /// </summary>
    public class WeatherEvent_HellfireStorm : WeatherEvent
    {
        // 事件持续时间（单位：ticks）
        protected int duration;
        
        // 事件当前年龄（单位：ticks）
        protected int age;
        
        // 着火中心点
        protected IntVec3 fireCenter;
        
        // 已经找到并标记的可燃物品
        protected List<Thing> flammableThingsInArea;
        
        // 需要点燃的物品数量
        protected int totalToIgnite;
        
        // 当前点燃的物品数量
        protected int ignitedCount = 0;
        
        // 燃烧特效颜色
        private static readonly SkyColorSet HellfireColors = new SkyColorSet(
            new Color(1f, 0.4f, 0.1f),  // 火光色
            new Color(1f, 0.3f, 0f),    // 火焰色
            new Color(1f, 0.5f, 0.2f),  // 发光色
            1.25f                       // 强度
        );
        
        // 地狱焚风音效
        private static readonly SoundDef HellfireWindSound = SoundDef.Named("HellfireWind");
        
        // 区域大小（12x12）
        private const int AREA_SIZE = 12;
        
        public override bool Expired => age > duration;
        
        public override SkyTarget SkyTarget => new SkyTarget(0.8f, HellfireColors, 1f, 1f);
        
        public override float SkyTargetLerpFactor => HellfireBrightness;
        
        protected float HellfireBrightness
        {
            get
            {
                // 事件开始和结束时亮度较低，中间较亮
                float progress = (float)age / duration;
                if (progress < 0.2f)
                    return progress * 5f; // 逐渐变亮
                else if (progress > 0.8f)
                    return (1f - progress) * 5f; // 逐渐变暗
                else
                    return 1f; // 保持最亮
            }
        }
        
        /// <summary>
        /// 构造函数 - RimWorld WeatherEventMaker需要这个构造函数
        /// </summary>
        /// <param name="map">地图</param>
        public WeatherEvent_HellfireStorm(Map map) : base(map)
        {
            // 事件持续时间（约5秒）
            duration = 300;
            
            // 随机选择着火中心点
            this.fireCenter = GetRandomFireCenter(map);
            
            // 默认点燃区域内所有可燃物
            totalToIgnite = 0;
            
            // 在事件开始时查找区域内的所有可燃物品
            flammableThingsInArea = FindFlammableThingsInArea(map, this.fireCenter, AREA_SIZE);
        }
        
        /// <summary>
        /// 扩展构造函数 - 用于手动触发时指定参数
        /// </summary>
        /// <param name="map">地图</param>
        /// <param name="fireCenter">着火中心点</param>
        /// <param name="igniteCount">需要点燃的物品数量（0表示点燃所有）</param>
        public WeatherEvent_HellfireStorm(Map map, IntVec3 fireCenter, int igniteCount = 0) : base(map)
        {
            duration = 300;
            this.fireCenter = fireCenter;
            this.totalToIgnite = igniteCount > 0 ? igniteCount : 0;
            flammableThingsInArea = FindFlammableThingsInArea(map, this.fireCenter, AREA_SIZE);
        }
        
        /// <summary>
        /// 获取随机的着火中心点
        /// </summary>
        private IntVec3 GetRandomFireCenter(Map map)
        {
            // 尝试找到合适的位置（避免在水上或重要区域）
            for (int attempt = 0; attempt < 20; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomCell(map);
                
                if (candidate.InBounds(map) && 
                    !candidate.GetTerrain(map).IsWater && 
                    candidate.Walkable(map) &&
                    candidate.GetRoom(map) != null)
                {
                    return candidate;
                }
            }
            
            // 如果找不到理想位置，返回地图中心
            return new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
        }
        
        /// <summary>
        /// 查找着火中心周围区域内的所有可燃物品
        /// </summary>
        private List<Thing> FindFlammableThingsInArea(Map map, IntVec3 center, int areaSize)
        {
            List<Thing> flammableThings = new List<Thing>();
            
            // 计算区域的边界
            int halfSize = areaSize / 2;
            int minX = Mathf.Max(center.x - halfSize, 0);
            int maxX = Mathf.Min(center.x + halfSize, map.Size.x - 1);
            int minZ = Mathf.Max(center.z - halfSize, 0);
            int maxZ = Mathf.Min(center.z + halfSize, map.Size.z - 1);
            
            // 遍历区域内的所有单元格
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    
                    if (!cell.InBounds(map))
                        continue;
                        
                    // 检查单元格上的所有物品
                    List<Thing> things = map.thingGrid.ThingsListAt(cell);
                    foreach (Thing thing in things)
                    {
                        // 检查物品是否可以被点燃
                        if (CanIgniteThing(thing))
                        {
                            flammableThings.Add(thing);
                        }
                    }
                    
                    // 检查建筑物
                    Building building = map.edificeGrid.InnerArray[map.cellIndices.CellToIndex(cell)];
                    if (building != null && CanIgniteThing(building))
                    {
                        flammableThings.Add(building);
                    }
                }
            }
            
            return flammableThings;
        }
        
        /// <summary>
        /// 事件开始时触发
        /// </summary>
        public override void FireEvent()
        {
            // 播放地狱焚风音效
            if (HellfireWindSound != null)
            {
                HellfireWindSound.PlayOneShotOnCamera(map);
            }
            
            // 创建着火中心特效
            CreateFireCenterEffect();
        }
        
        /// <summary>
        /// 创建着火中心特效
        /// </summary>
        private void CreateFireCenterEffect()
        {
            // 在着火中心创建大量火焰和烟雾特效
            for (int i = 0; i < 10; i++)
            {
                Vector3 pos = fireCenter.ToVector3Shifted() + 
                    new Vector3(
                        Rand.Range(-1f, 1f),
                        0f,
                        Rand.Range(-1f, 1f)
                    );
            }
        }
        
        /// <summary>
        /// 每Tick更新事件状态
        /// </summary>
        public override void WeatherEventTick()
        {
            age++;
            
            // 在事件开始的前几Tick点燃物品
            if (age < 100 && age % 5 == 0) // 每5Tick点燃一次，持续100Tick
            {
                TryIgniteThingsInArea();
            }
        }
        
        /// <summary>
        /// 尝试点燃区域内的物品
        /// </summary>
        private void TryIgniteThingsInArea()
        {
            if (flammableThingsInArea.Count == 0)
                return;
                
            // 计算本次Tick应该点燃的物品数量
            int toIgniteThisTick;
            if (totalToIgnite > 0)
            {
                // 如果指定了点燃数量，平均分配在事件期间
                int remaining = totalToIgnite - ignitedCount;
                toIgniteThisTick = Mathf.Min(Mathf.Max(remaining / 20, 1), remaining);
            }
            else
            {
                // 如果没有指定数量，每次点燃区域内的几个物品
                toIgniteThisTick = Mathf.Max(flammableThingsInArea.Count / 20, 1);
            }
            
            // 尝试点燃物品
            for (int i = 0; i < toIgniteThisTick && i < flammableThingsInArea.Count; i++)
            {
                // 从列表中移除已尝试的物品
                Thing thing = flammableThingsInArea[0];
                flammableThingsInArea.RemoveAt(0);
                
                if (TryIgniteThing(thing))
                {
                    ignitedCount++;
                    
                    // 如果达到了指定的点燃数量，停止点燃
                    if (totalToIgnite > 0 && ignitedCount >= totalToIgnite)
                    {
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// 尝试点燃单个物品
        /// </summary>
        private bool TryIgniteThing(Thing thing)
        {
            if (thing == null || thing.Destroyed)
                return false;
                
            // 检查物品是否已经着火
            if (thing.GetAttachment(ThingDefOf.Fire) != null)
                return false;
                
            // 尝试点燃物品
            try
            {
                if (thing.FlammableNow)
                {
                    // 创建火焰
                    Fire fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                    fire.fireSize = Rand.Range(0.2f, 0.5f);
                    GenSpawn.Spawn(fire, thing.Position, map, thing.Rotation);
                    
                    // 播放点燃特效
                    PlayIgniteEffect(thing);
                    
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"地狱焚风点燃物品时出错: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// 播放点燃特效
        /// </summary>
        private void PlayIgniteEffect(Thing thing)
        {
            // 创建视觉特效
            Vector3 pos = thing.DrawPos + new Vector3(
                Rand.Range(-0.3f, 0.3f),
                0f,
                Rand.Range(-0.3f, 0.3f)
            );
        }
        
        /// <summary>
        /// 检查物品是否可以被点燃
        /// </summary>
        private bool CanIgniteThing(Thing thing)
        {
            if (thing == null || thing.Destroyed)
                return false;
                
            // 检查是否为重要的结构或核心建筑
            if (thing.def.category == ThingCategory.Building)
            {
                // 避免点燃核心建筑
                if (thing.def.building != null && thing.def.building.isNaturalRock)
                    return false;
            }
            
            // 检查物品是否可以被点燃
            if (!thing.FlammableNow)
                return false;
                
            // 检查物品是否已经在燃烧
            if (thing.GetAttachment(ThingDefOf.Fire) != null)
                return false;
                
            // 检查物品是否有防火特性
            if (thing.FireBulwark)
                return false;
                
            // 检查是否为Pawn
            if (thing is Pawn pawn)
            {
                // 不点燃玩家控制的Pawn
                //if (pawn.Faction == Faction.OfPlayer)
                //    return false;
                    
                // 不点燃已倒地或死亡的Pawn
                if (pawn.Downed || pawn.Dead)
                    return false;
                    
                // 检查Pawn是否可燃（非机械）
                if (!pawn.RaceProps.IsFlesh)
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 手动触发地狱焚风事件
        /// </summary>
        public static void TriggerHellfireStorm(Map map, IntVec3? fireCenter = null, int igniteCount = 0)
        {
            // 创建适当的构造函数调用
            WeatherEvent_HellfireStorm hellfireEvent;
            
            if (fireCenter.HasValue)
            {
                // 使用指定中心点的构造函数
                hellfireEvent = new WeatherEvent_HellfireStorm(map, fireCenter.Value, igniteCount);
            }
            else
            {
                // 使用默认构造函数，会自动随机选择中心点
                hellfireEvent = new WeatherEvent_HellfireStorm(map);
            }
            
            map.weatherManager.eventHandler.AddEvent(hellfireEvent);
        }
    }
}
