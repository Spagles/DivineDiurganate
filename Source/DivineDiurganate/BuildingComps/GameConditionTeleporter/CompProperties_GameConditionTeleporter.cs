using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompProperties_GameConditionTeleporter : CompProperties
    {
        public GameConditionDef conditionDef;
        public int worldRange = 0; // 影响范围（0表示只影响本地图）
        public bool hideSource = true; // 隐藏来源
        public bool preventConditionStacking = true; // 防止条件堆叠
        
        public CompProperties_GameConditionTeleporter()
        {
            compClass = typeof(CompGameConditionTeleporter);
        }
    }
}
