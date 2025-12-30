// File: RitualOutcomeComp_ApplyHediffToParticipants_Fixed.cs
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace DivineDiurganate
{
    public class RitualOutcomeComp_ApplyHediffToParticipants : RitualOutcomeComp
    {
        public HediffDef hediffToApply;
        public float severityPerQuality = 0.1f; // 每点质量增加的严重性
        public bool applyToAllParticipants = true; // 是否应用到所有参与者
        public bool requirePositiveQuality = false; // 是否只在质量为正时应用
        
        public override RitualOutcomeComp_Data MakeData()
        {
            return new RitualOutcomeComp_Data_ApplyHediff();
        }
        
        public override void Tick(LordJob_Ritual ritual, RitualOutcomeComp_Data data, float progressAmount)
        {
            // 在仪式结束时应用Hediff
            if (progressAmount >= 1f)
            {
                ApplyHediffToParticipants(ritual, data);
            }
        }
        
        private void ApplyHediffToParticipants(LordJob_Ritual ritual, RitualOutcomeComp_Data data)
        {
            var ritualData = data as RitualOutcomeComp_Data_ApplyHediff;
            if (ritualData == null || hediffToApply == null)
                return;
            
            // 如果已经应用过，跳过
            if (ritualData.hediffApplied)
                return;
            
            try
            {
                // 获取仪式质量 - 使用与RitualOutcomeEffectWorker_FromQuality相同的方法
                float quality = GetRitualQuality(ritual, 1f); // 进度为1表示仪式完成
                
                // 如果需要质量为正但质量不是正数，跳过
                if (requirePositiveQuality && quality <= 0f)
                    return;
                
                // 计算严重性
                float severity = quality * severityPerQuality;
                
                // 确保严重性在合理范围内
                severity = Mathf.Clamp(severity, 0.1f, 1f);
                
                // 获取参与者
                var participants = GetEligibleParticipants(ritual);
                
                if (participants.Count == 0)
                    return;
                
                // 应用Hediff
                foreach (var pawn in participants)
                {
                    ApplyHediffToPawn(pawn, severity);
                }
                
                // 标记为已应用
                ritualData.hediffApplied = true;
                
                // 记录日志
                if (Prefs.DevMode)
                {
                    Log.Message($"[DD] 为 {participants.Count} 名参与者应用Hediff {hediffToApply.defName}，质量: {quality:F2}，严重性: {severity:F2}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] 应用Hediff时出错: {ex}");
            }
        }
        
        // 获取仪式质量 - 参考RitualOutcomeEffectWorker_FromQuality.GetQuality方法
        private float GetRitualQuality(LordJob_Ritual ritual, float progress)
        {
            try
            {
                // 获取仪式效果worker
                var outcomeEffect = ritual.Ritual?.outcomeEffect;
                if (outcomeEffect == null)
                    return 0f;
                
                // 检查是否是FromQuality类型
                if (outcomeEffect is RitualOutcomeEffectWorker_FromQuality fromQualityWorker)
                {
                    // 使用反射调用GetQuality方法，因为它是protected
                    var method = typeof(RitualOutcomeEffectWorker_FromQuality).GetMethod("GetQuality",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    
                    if (method != null)
                    {
                        return (float)method.Invoke(fromQualityWorker, new object[] { ritual, progress });
                    }
                }
                
                // 备选方案：计算基础质量
                return CalculateBaseQuality(ritual, progress);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] 获取仪式质量时出错: {ex}");
                return 0.5f; // 默认质量
            }
        }
        
        // 备选的质量计算方法
        private float CalculateBaseQuality(LordJob_Ritual ritual, float progress)
        {
            float quality = 0.5f; // 基础质量
            
            // 考虑重复惩罚
            if (ritual.repeatPenalty && ritual.Ritual != null)
            {
                quality += ritual.Ritual.RepeatQualityPenalty;
            }
            
            // 考虑进度
            var progressToQualityMapping = new FloatRange(0.25f, 1f);
            quality *= Mathf.Lerp(progressToQualityMapping.min, progressToQualityMapping.max, progress);
            
            return Mathf.Clamp(quality, 0f, 1f);
        }
        
        private List<Pawn> GetEligibleParticipants(LordJob_Ritual ritual)
        {
            var participants = new List<Pawn>();
            
            if (applyToAllParticipants)
            {
                // 获取所有仪式参与者
                if (ritual.assignments?.Participants != null)
                {
                    foreach (var pawn in ritual.assignments.Participants)
                    {
                        if (IsPawnEligible(pawn))
                        {
                            participants.Add(pawn);
                        }
                    }
                }
            }
            else
            {
                // 只获取仪式角色分配中的参与者
                if (ritual.assignments?.SpectatorsForReading != null)
                {
                    foreach (var pawn in ritual.assignments.SpectatorsForReading)
                    {
                        if (IsPawnEligible(pawn))
                        {
                            participants.Add(pawn);
                        }
                    }
                }
            }
            
            return participants;
        }
        
        private bool IsPawnEligible(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed && pawn.health != null;
        }
        
        private void ApplyHediffToPawn(Pawn pawn, float severity)
        {
            try
            {
                // 检查是否已经有相同的Hediff
                var existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffToApply);
                
                if (existingHediff != null)
                {
                    // 更新现有Hediff的严重性（取最大值）
                    existingHediff.Severity = Mathf.Max(existingHediff.Severity, severity);
                    
                    // 如果是有消失组件的临时效果，重置持续时间
                    var disappearsComp = existingHediff.TryGetComp<HediffComp_Disappears>();
                    if (disappearsComp != null)
                    {
                        disappearsComp.ticksToDisappear = Mathf.Max(
                            disappearsComp.ticksToDisappear, 
                            Mathf.RoundToInt(60000 * severity) // 基础1天，根据严重性调整
                        );
                    }
                }
                else
                {
                    // 添加新的Hediff
                    var hediff = HediffMaker.MakeHediff(hediffToApply, pawn);
                    hediff.Severity = severity;
                    
                    // 如果是有消失组件的临时效果，设置初始持续时间
                    var disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
                    if (disappearsComp != null)
                    {
                        disappearsComp.ticksToDisappear = Mathf.RoundToInt(60000 * (1f + severity));
                    }
                    
                    pawn.health.AddHediff(hediff);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] 为 {pawn?.LabelShort} 应用Hediff时出错: {ex}");
            }
        }
        
        public override string GetDesc(LordJob_Ritual ritual = null, RitualOutcomeComp_Data data = null)
        {
            if (hediffToApply == null)
                return base.GetDesc(ritual, data);
            
            string participantDesc = applyToAllParticipants ? "所有参与者" : "观众";
            string qualityCondition = requirePositiveQuality ? "（仅当质量为正时）" : "";
            
            return $"根据仪式质量给予{participantDesc} {hediffToApply.label} {qualityCondition}\n" +
                   $"（每点质量增加 {severityPerQuality:F2} 严重性）";
        }
        
        public override bool Applies(LordJob_Ritual ritual)
        {
            return hediffToApply != null;
        }
    }
    
    public class RitualOutcomeComp_Data_ApplyHediff : RitualOutcomeComp_Data
    {
        public bool hediffApplied = false;
        
        public override void Reset()
        {
            base.Reset();
            hediffApplied = false;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hediffApplied, "hediffApplied", false);
        }
    }
}
