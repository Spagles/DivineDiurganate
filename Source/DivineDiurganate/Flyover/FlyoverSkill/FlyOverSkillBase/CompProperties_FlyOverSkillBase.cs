using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机技能属性基类
    /// </summary>
    public class CompProperties_FlyOverSkillBase : CompProperties
    {
        // 基础信息
        public string skillName = "Unknown Skill";
        public string description = "No description available";
        public string iconPath; // 技能图标路径
        
        // 使用限制
        public int cooldownTicks = 3000; // 冷却时间（60 ticks = 1秒）
        public int maxUses = -1; // 最大使用次数，-1表示无限
        
        // 允许使用状态
        public bool canUseWhenOnMap = true;      // 在场上时可用
        public bool canUseWhenStandby = true;    // 待命时可用
        public bool canUseWhenDestroyed = false; // 被摧毁时不可用
        
        // 目标选择类型
        public SkillTargetType targetType = SkillTargetType.SinglePoint;
        
        // 新增：立即释放选项
        public bool instantCast = false; // 是否立即释放（不选择目标）
        public bool useFlyoverPosition = true; // 是否使用Flyover当前位置作为目标
        
        // 新增：目标选择提示消息（可在XML中定义）
        public string singlePointSelectMessage = "Select target for {0}";
        public string twoPointsFirstPointMessage = "Select first point for {0}";
        public string twoPointsSecondPointMessage = "Select second point for {0}";
        public string instantCastMessage = "Using {0} at aircraft position"; // 立即释放消息
        
        // 技能颜色（用于UI显示）
        public Color skillColor = Color.white;
        
        // 技能槽位（0-3）
        public int slotIndex = 0;
        
        public CompProperties_FlyOverSkillBase()
        {
            compClass = typeof(CompFlyOverSkillBase);
        }
    }
    
    /// <summary>
    /// 技能目标类型
    /// </summary>
    public enum SkillTargetType
    {
        SinglePoint,    // 单点选择
        TwoPoints,      // 双点选择
        Instant         // 立即释放（不需要选择目标）
    }
}
