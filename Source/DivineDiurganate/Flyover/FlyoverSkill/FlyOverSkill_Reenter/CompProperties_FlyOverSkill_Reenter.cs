using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 再次入场技能属性
    /// </summary>
    public class CompProperties_FlyOverSkill_Reenter : CompProperties_FlyOverSkillBase
    {
        // 飞行相关配置
        public float defaultSpeed = 1f;
        public float defaultAltitude = 15f;
        
        // 视觉效果配置
        public bool spawnContentsOnImpact = false;
        public ThingDef contentThingDef;
        public int contentCount = 0;
        
        public CompProperties_FlyOverSkill_Reenter()
        {
            compClass = typeof(CompFlyOverSkill_Reenter);
            targetType = SkillTargetType.TwoPoints;
            skillName = "Re-enter";
            description = "Summon the aircraft to fly over a new path on the map.";
            cooldownTicks = 6000; // 100秒冷却
            skillColor = new Color(0.2f, 0.6f, 0.9f, 1f);
        }
    }
}
