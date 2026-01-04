using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 飞跃物体生成器的Comp属性
    /// </summary>
    public class CompProperties_FlyOverGenerator : CompProperties
    {
        // 生成的飞跃物体Def
        public ThingDef flyOverDef;
        
        // 默认飞行速度
        public float defaultSpeed = 1f;
        
        // 默认飞行高度
        public float defaultAltitude = 10f;
        
        // 是否包含内容物
        public bool spawnContents = false;
        
        // 内容物（如果需要）
        public ThingDef contentThingDef;
        public int contentCount = 1;
        
        // 能量消耗（如果需要）
        public float energyCost = 0f;
        
        // 冷却时间（ticks）
        public int cooldownTicks = 600;
        
        // 使用次数限制（-1为无限）
        public int useLimit = -1;
        
        public CompProperties_FlyOverGenerator()
        {
            compClass = typeof(CompFlyOverGenerator);
        }
    }
}
