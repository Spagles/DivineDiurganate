using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 轰炸技能属性
    /// </summary>
    public class CompProperties_FlyOverSkill_Bombardment : CompProperties_FlyOverSkillBase
    {
        // 轰炸区域配置
        public int bombardmentWidth = 5;           // 轰炸区域宽度
        public int bombardmentLength = 8;          // 轰炸区域长度
        
        // 目标选择配置
        public float targetSelectionChance = 0.6f; // 每个格子被选中的概率
        public int minTargetCells = 3;             // 最小目标格子数
        public int maxTargetCells = 15;            // 最大目标格子数
        
        // 时间配置
        public int warmupTicks = 120;              // 前摇时间
        public int rowDelayTicks = 30;             // 每排之间的延迟
        public int impactDelayTicks = 10;          // 单个轰炸的延迟（同一排内）
        
        // Skyfaller 配置
        public ThingDef skyfallerDef;              // 使用的 Skyfaller
        public ThingDef projectileDef;             // 备用的抛射体定义（如果 skyfaller 不可用）
        
        // 轰炸效果器
        public EffecterDef bombardmentEffecter;
        public float bombardmentEffectDuration = 2.0f;
        
        public CompProperties_FlyOverSkill_Bombardment()
        {
            compClass = typeof(CompFlyOverSkill_Bombardment);
            targetType = SkillTargetType.TwoPoints; // 使用双点选择
            skillName = "Bombardment";
            description = "Call in an artillery bombardment. Select two points to define the bombardment area and direction.";
            cooldownTicks = 9000; // 150秒冷却
            skillColor = new Color(0.8f, 0.2f, 0.2f, 1f); // 红色系
        }
    }
}
