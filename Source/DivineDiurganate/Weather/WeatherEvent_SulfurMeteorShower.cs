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
            // 寻找合适的召唤位置
            IntVec3 targetCell = CellFinder.RandomCell(map);
            
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
