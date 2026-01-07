using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// Skyfaller投掷技能属性
    /// </summary>
    public class CompProperties_FlyOverSkill_SkyfallerDrop : CompProperties_FlyOverSkillBase
    {
        // Skyfaller配置
        public ThingDef skyfallerDef;              // 使用的Skyfaller定义
        
        // 坠落参数
        public float dropRadius = 0f;              // 坠落点随机半径（0表示精确位置）
        public bool randomizeDropOffset = false;   // 是否在周围随机偏移
        public int minDistanceFromFlyover = 0;     // 最小距离（避免砸到自己）
        public int maxDistanceFromFlyover = 3;     // 最大距离
        
        // 效果参数
        public int warmupTicks = 60;               // 前摇时间（默认为1秒）
        public bool showWarmupEffect = true;       // 是否显示准备效果
        
        // 投掷物内容
        public ThingDef contentThingDef;           // Skyfaller内部包含的物品
        public int contentCount = 1;               // 物品数量
        
        public CompProperties_FlyOverSkill_SkyfallerDrop()
        {
            compClass = typeof(CompFlyOverSkill_SkyfallerDrop);
            
            // 设置为立即释放
            instantCast = true;
            targetType = SkillTargetType.Instant;
            
            skillName = "Skyfaller Drop";
            description = "Drop a skyfaller at the aircraft's current position.";
            cooldownTicks = 4500; // 75秒冷却
            skillColor = new Color(0.7f, 0.5f, 0.1f, 1f); // 橙色系
            instantCastMessage = "Dropping skyfaller at {0} position";
        }
    }
}
