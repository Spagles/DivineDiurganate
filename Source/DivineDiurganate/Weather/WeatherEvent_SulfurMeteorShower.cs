using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 超简化版硫磺陨石雨天气事件 - 只召唤陨石，无其他效果
    /// </summary>
    public class WeatherEvent_SulfurMeteorShower : WeatherEvent
    {
        protected int duration;
        protected int age;
        protected int nextMeteorTick;
        protected int meteorInterval = 60;
        protected int spawnedCount = 0;
        protected int maxMeteors = 8;
        
        // 注意：DD_SulfurMeteor 是一个 Skyfaller，不是普通物品
        private const string METEOR_SKYFALLER_DEF_NAME = "DD_SulfurMeteor";
        
        public override bool Expired => age > duration;
        
        public override float SkyTargetLerpFactor => 0f;
        
        public WeatherEvent_SulfurMeteorShower(Map map) : base(map)
        {
            duration = 900;
            maxMeteors = Rand.RangeInclusive(5, 12);
            nextMeteorTick = age + Rand.Range(30, 90);
        }
        
        public WeatherEvent_SulfurMeteorShower(Map map, int maxMeteors = 8, int duration = 900) : base(map)
        {
            this.duration = duration;
            this.maxMeteors = maxMeteors;
            nextMeteorTick = age + Rand.Range(30, 90);
        }
        
        public override void FireEvent()
        {
        }
        
        public override void WeatherEventTick()
        {
            age++;
            
            if (age >= nextMeteorTick && spawnedCount < maxMeteors)
            {
                SpawnMeteor();
                nextMeteorTick = age + meteorInterval + Rand.Range(-10, 10);
            }
        }
        
        private void SpawnMeteor()
        {
            // 寻找合适的召唤位置（避开庇护区域）
            IntVec3 targetCell = FindNonShelteredMeteorCell();
            
            if (!targetCell.IsValid || !targetCell.InBounds(map))
                return;
            
            // 获取陨石 Skyfaller Def
            ThingDef meteorSkyfallerDef = DefDatabase<ThingDef>.GetNamedSilentFail(METEOR_SKYFALLER_DEF_NAME);
            
            if (meteorSkyfallerDef == null)
            {
                Log.Error($"无法找到陨石Skyfaller Def: {METEOR_SKYFALLER_DEF_NAME}");
                return;
            }
            
            try
            {
                // 方法1：直接生成 DD_SulfurMeteor Skyfaller（因为 DD_SulfurMeteor 本身就是一个 Skyfaller）
                // 使用 SkyfallerMaker.SpawnSkyfaller 的另一个重载，只传入 Skyfaller Def
                SkyfallerMaker.SpawnSkyfaller(meteorSkyfallerDef, targetCell, map);
                
                // 方法2：或者直接生成 Thing
                // Thing meteor = ThingMaker.MakeThing(meteorSkyfallerDef);
                // GenSpawn.Spawn(meteor, targetCell, map);
                
                spawnedCount++;
            }
            catch (System.Exception ex)
            {
                Log.Error($"召唤陨石时出错: {ex}");
            }
        }
        
        /// <summary>
        /// 寻找不在庇护区域内的陨石落点
        /// </summary>
        private IntVec3 FindNonShelteredMeteorCell()
        {
            // 尝试多次寻找不在庇护区域内的单元格
            for (int attempt = 0; attempt < 20; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomCell(map);
                
                if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                {
                    return candidate;
                }
            }
            
            // 如果找不到非庇护单元格，尝试地图边缘
            for (int attempt = 0; attempt < 10; attempt++)
            {
                IntVec3 candidate = GetRandomEdgeCell(map);
                
                if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                {
                    return candidate;
                }
            }
            
            // 如果所有尝试都失败，返回随机单元格（即使可能在庇护区域内）
            Log.Warning("[DivineDiurganate] 无法找到不在庇护区域内的陨石落点");
            return CellFinder.RandomCell(map);
        }
        
        /// <summary>
        /// 获取随机边缘单元格
        /// </summary>
        private IntVec3 GetRandomEdgeCell(Map map)
        {
            int edgeDistance = 10;
            int x = Rand.Range(edgeDistance, map.Size.x - edgeDistance);
            int z = Rand.Range(edgeDistance, map.Size.z - edgeDistance);
            
            // 随机选择边缘
            if (Rand.Value > 0.5f)
            {
                // 左边或右边
                if (Rand.Value > 0.5f)
                    x = edgeDistance; // 左边
                else
                    x = map.Size.x - edgeDistance - 1; // 右边
            }
            else
            {
                // 上边或下边
                if (Rand.Value > 0.5f)
                    z = edgeDistance; // 下边
                else
                    z = map.Size.z - edgeDistance - 1; // 上边
            }
            
            return new IntVec3(x, 0, z);
        }
        
        public static void TriggerMeteorShower(Map map, int maxMeteors = 8, int duration = 900)
        {
            WeatherEvent_SulfurMeteorShower meteorEvent = 
                new WeatherEvent_SulfurMeteorShower(map, maxMeteors, duration);
            
            map.weatherManager.eventHandler.AddEvent(meteorEvent);
        }
        
        public static void SpawnSingleMeteor(Map map, IntVec3 targetCell)
        {
            if (!targetCell.InBounds(map))
                return;
                
            // 检查目标单元格是否在庇护区域内
            if (WeatherShelterManager.IsCellSheltered(map, targetCell))
            {
                Log.Warning($"[DivineDiurganate] 尝试在庇护区域内召唤陨石，已阻止: {targetCell}");
                
                // 显示庇护保护效果
                MoteMaker.ThrowText(targetCell.ToVector3Shifted(), map, "庇护保护", Color.cyan, 2.0f);
                return;
            }
                
            ThingDef meteorSkyfallerDef = DefDatabase<ThingDef>.GetNamedSilentFail(METEOR_SKYFALLER_DEF_NAME);
            
            if (meteorSkyfallerDef == null)
            {
                Log.Error($"无法找到陨石Skyfaller Def: {METEOR_SKYFALLER_DEF_NAME}");
                return;
            }
            
            try
            {
                // 直接生成 DD_SulfurMeteor Skyfaller
                SkyfallerMaker.SpawnSkyfaller(meteorSkyfallerDef, targetCell, map);
            }
            catch (System.Exception ex)
            {
                Log.Error($"召唤单个陨石时出错: {ex}");
            }
        }
        
        // 不需要绘制特殊效果
        public override void WeatherEventDraw() { }
    }
}
