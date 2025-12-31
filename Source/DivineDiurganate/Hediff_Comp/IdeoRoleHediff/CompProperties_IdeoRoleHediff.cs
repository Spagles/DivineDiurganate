using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    public class HediffCompProperties_IdeoRoleHediff : HediffCompProperties
    {
        // 指定的意识形态职位类型
        public PreceptDef requiredRole;
        
        // 需要的 MemeDef（可选）
        public List<MemeDef> requiredMeme;
        
        // 是否只在有对应职位时才给予Hediff
        public bool requireRole = true;
        
        // 是否需要检查Meme
        public bool requireMeme = false;
        
        // 是否所有需要的Meme都必须存在（true=所有都需要，false=任意一个）
        public bool requireAllMemes = true;
        
        // Hediff的严重性级别（可选）
        public float severityLevel = 1.0f;
        
        // 检查间隔（游戏刻）
        public int checkIntervalTicks = 2500; // 默认1游戏天
        
        public HediffCompProperties_IdeoRoleHediff()
        {
            this.compClass = typeof(Comp_IdeoRoleHediff);
        }
    }
}
