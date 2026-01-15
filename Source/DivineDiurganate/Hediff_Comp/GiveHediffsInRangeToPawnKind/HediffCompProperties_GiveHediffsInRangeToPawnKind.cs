using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    public class HediffCompProperties_GiveHediffsInRangeToPawnKind : HediffCompProperties
    {
        public float range;
        public TargetingParameters targetingParameters;
        public HediffDef hediff;
        public ThingDef mote;
        public bool hideMoteWhenNotDrafted;
        public float defaultSeverity = 1f; // 默认严重性
        public bool onlyPawnsInSameFaction = true;
        
        // PawnKindDef 到严重性的映射
        public List<PawnKindSeverity> pawnKindSeverities = new List<PawnKindSeverity>();
        
        // 是否对不在列表中的PawnKindDef使用默认严重性
        public bool useDefaultForOtherPawnKinds = true;
        
        // 缓存字典，用于快速查找
        private Dictionary<PawnKindDef, float> pawnKindSeverityDict = null;

        public HediffCompProperties_GiveHediffsInRangeToPawnKind()
        {
            compClass = typeof(HediffComp_GiveHediffsInRangeToPawnKind);
        }
        
        /// <summary>
        /// 获取PawnKindDef对应的严重性
        /// </summary>
        public float GetSeverityForPawnKind(PawnKindDef pawnKindDef)
        {
            if (pawnKindSeverityDict == null && pawnKindSeverities != null)
            {
                pawnKindSeverityDict = new Dictionary<PawnKindDef, float>();
                foreach (var pawnKindSeverity in pawnKindSeverities)
                {
                    if (pawnKindSeverity.pawnKindDef != null)
                    {
                        pawnKindSeverityDict[pawnKindSeverity.pawnKindDef] = pawnKindSeverity.severity;
                    }
                }
            }
            
            if (pawnKindSeverityDict != null && pawnKindDef != null && pawnKindSeverityDict.TryGetValue(pawnKindDef, out float severity))
            {
                return severity;
            }
            
            return useDefaultForOtherPawnKinds ? defaultSeverity : 0f;
        }
    }
    
    /// <summary>
    /// PawnKindDef 和严重性的映射
    /// </summary>
    public class PawnKindSeverity
    {
        public PawnKindDef pawnKindDef;
        public float severity = 1f;
    }
}
