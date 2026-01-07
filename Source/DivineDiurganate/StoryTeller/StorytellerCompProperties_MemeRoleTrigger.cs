using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// Meme 和 Role 触发器的 StorytellerComp 属性
    /// </summary>
    public class StorytellerCompProperties_MemeRoleTrigger : StorytellerCompProperties
    {
        // 必需配置
        public MemeDef requiredMeme;                    // 必须拥有的 Meme
        public PreceptDef requiredRolePrecept;          // 必须分配的职位（Precept_Role）
        public IncidentDef incident;                    // 要触发的事件

        // 时间配置
        public float fireAfterDaysPassed = 0f;          // 游戏开始后多少天开始检测
        public float checkIntervalDays = 5f;            // 检查间隔（天）
        public bool repeatable = false;                 // 是否可重复触发
        public float repeatIntervalDays = 30f;          // 重复触发间隔（天）

        // 过滤条件
        public int minColonistsWithRole = 1;            // 最少有多少殖民者拥有该职位
        public bool requireAllColonies = true;          // 是否要求所有殖民地都满足条件
        public bool requirePermanentRole = true;        // 是否要求永久职位（非临时）

        // 额外条件
        public List<FactionDef> allowedFactions;        // 允许的派系列表（可选）
        public bool debugLogging = false;               // 启用调试日志

        // 冷却和状态跟踪
        private int lastTriggeredTick = -1;             // 上次触发时间（ticks）

        public StorytellerCompProperties_MemeRoleTrigger()
        {
            compClass = typeof(StorytellerComp_MemeRoleTrigger);
        }
    }
}
