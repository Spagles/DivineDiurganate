using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompProperties_MapTeleporter : CompProperties
    {
        public IntVec2 areaSize = new IntVec2(13, 13);
        public int warmupTicks = 120;
        public float daysPerDistance = 0.25f; // 每单位距离需要的天数
        
        // 新增：最大传送天数限制
        public float maxTeleportDays = 0f; // 0表示无限制
        public bool useMaxTeleportDays = false; // 是否启用最大时间限制
        
        public EffecterDef warmupEffecter;
        public SoundDef warmupSound;
        public SoundDef teleportSound;
        public ResearchProjectDef requiredResearch;
        
        // 是否检查目标地图的canBuildBase
        public bool checkCanBuildBase = true;
        
        // 传送期间的游戏条件
        public GameConditionDef warmupGameConditionDef;
        public int worldRange = 0; // 影响范围（0表示只影响本地图）
        public bool hideSource = true; // 隐藏来源
        public bool preventConditionStacking = true; // 防止条件堆叠
        
        // 是否在传送后转换源地图生物群系为地狱
        public bool convertSourceBiomeToHell = true;

        public CompProperties_MapTeleporter()
        {
            compClass = typeof(CompMapTeleporter);
        }
    }
}
