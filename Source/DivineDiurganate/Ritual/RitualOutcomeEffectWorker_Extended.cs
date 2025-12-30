// File: RitualOutcomeEffectWorker_Extended.cs
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class RitualOutcomeEffectWorker_Extended : RitualOutcomeEffectWorker
    {
        public RitualOutcomeEffectWorker_Extended()
        {
        }
        
        public RitualOutcomeEffectWorker_Extended(RitualOutcomeEffectDef def) : base(def)
        {
        }
        
        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            // 额外处理：为所有参与者应用Hediff（如果需要）
            ApplyAdditionalEffects(progress, totalPresence, jobRitual);
        }
        
        protected virtual void ApplyAdditionalEffects(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            // 这个方法可以由子类重写以添加额外效果
            // 现在我们使用RitualOutcomeComp来处理，所以这里留空
        }
    }
}
