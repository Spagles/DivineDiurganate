using RimWorld;
using Verse;
using System.Collections.Generic;

namespace DivineDiurganate
{
    /// <summary>
    /// 初始文化Meme触发器属性
    /// </summary>
    public class StorytellerCompProperties_InitialMemeTrigger : StorytellerCompProperties
    {
        // 要检查的Meme
        public MemeDef memeToCheck;
        
        // 触发条件配置
        public bool checkPlayerFaction = true;           // 检查玩家派系
        public bool checkPrimaryIdeoOnly = true;         // 只检查主要文化
        
        // 事件配置
        public IncidentDef incidentIfHasMeme;            // 如果拥有该meme，触发此事件
        public IncidentDef incidentIfNoMeme;             // 如果没有该meme，触发此事件
        
        // 时间配置
        public float fireAfterDaysPassed = 0f;           // 游戏开始后多少天开始检测
        public float checkIntervalDays = 1f;             // 检查间隔（天）
        public bool repeatable = false;                  // 是否可重复触发
        public float repeatIntervalDays = 30f;           // 重复触发间隔（天）
        
        // 过滤条件
        public bool debugLogging = false;                // 启用调试日志
        public bool onlyOncePerGame = true;              // 整个游戏只触发一次
        
        public StorytellerCompProperties_InitialMemeTrigger()
        {
            compClass = typeof(StorytellerComp_InitialMemeTrigger);
        }
    }
}
