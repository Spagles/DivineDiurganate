using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    public class HediffComp_GiveHediffsInRangeToPawnKind : HediffComp
    {
        private Mote mote;
        private Dictionary<Pawn, int> lastTickChecked = new Dictionary<Pawn, int>();
        private Dictionary<Pawn, Hediff> activeHediffs = new Dictionary<Pawn, Hediff>();

        public HediffCompProperties_GiveHediffsInRangeToPawnKind Props => (HediffCompProperties_GiveHediffsInRangeToPawnKind)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            // 检查持有者是否有效
            if (parent.pawn == null || !parent.pawn.Spawned || parent.pawn.Map == null || 
                parent.pawn.health == null || parent.pawn.Dead)
            {
                CleanupAllHediffs();
                return;
            }

            if (!parent.pawn.Awake() || parent.pawn.health.InPainShock)
            {
                return;
            }
            
            // 显示Mote效果
            if (!Props.hideMoteWhenNotDrafted || parent.pawn.Drafted)
            {
                if (Props.mote != null && (mote == null || mote.Destroyed))
                {
                    mote = MoteMaker.MakeAttachedOverlay(parent.pawn, Props.mote, Vector3.zero);
                }
                if (mote != null)
                {
                    mote.Maintain();
                }
            }
            
            int currentTick = Find.TickManager.TicksGame;
            
            // 先收集所有潜在的目标Pawn
            List<Pawn> potentialPawns = new List<Pawn>();
            
            if (Props.onlyPawnsInSameFaction && parent.pawn.Faction != null)
            {
                var factionPawns = parent.pawn.Map.mapPawns.SpawnedPawnsInFaction(parent.pawn.Faction);
                if (factionPawns != null)
                {
                    potentialPawns.AddRange(factionPawns);
                }
            }
            else
            {
                var allPawns = parent.pawn.Map.mapPawns.AllPawnsSpawned;
                if (allPawns != null)
                {
                    potentialPawns.AddRange(allPawns);
                }
            }
            
            // 创建要处理的Pawn列表副本
            List<Pawn> pawnsToProcess = new List<Pawn>();
            
            foreach (Pawn pawn in potentialPawns)
            {
                if (pawn == null || pawn.Dead || pawn.health == null || pawn == parent.pawn)
                    continue;
                    
                // 检查目标参数
                if (Props.targetingParameters != null && !Props.targetingParameters.CanTarget(pawn))
                    continue;
                    
                pawnsToProcess.Add(pawn);
            }
            
            // 处理当前在范围内的Pawn
            foreach (Pawn pawn in pawnsToProcess)
            {
                // 检查距离
                if (pawn.Position.DistanceTo(parent.pawn.Position) > Props.range)
                {
                    // 如果不在范围内，但之前有hediff，则移除
                    RemoveHediffIfExists(pawn);
                    continue;
                }
                
                // 限制检查频率（每10 tick检查一次，提高频率避免闪烁）
                if (lastTickChecked.TryGetValue(pawn, out int lastTick) && currentTick - lastTick < 10)
                    continue;
                
                lastTickChecked[pawn] = currentTick;
                
                // 根据PawnKindDef获取对应的严重性
                float severity = Props.GetSeverityForPawnKind(pawn.kindDef);
                
                // 如果严重性为0，则跳过
                if (severity <= 0f)
                {
                    RemoveHediffIfExists(pawn);
                    continue;
                }
                
                // 添加或更新Hediff
                ApplyHediffToPawn(pawn, severity);
            }
            
            // 清理已死亡或不再存在的Pawn
            CleanupOldRecords();
        }
        
        /// <summary>
        /// 应用Hediff到Pawn
        /// </summary>
        private void ApplyHediffToPawn(Pawn pawn, float severity)
        {
            try
            {
                // 检查是否已经有这个Hediff
                if (activeHediffs.TryGetValue(pawn, out Hediff existingHediff))
                {
                    if (existingHediff == null || existingHediff.pawn == null || existingHediff.pawn.health == null)
                    {
                        // Hediff无效，重新获取
                        existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                        if (existingHediff == null)
                        {
                            existingHediff = null;
                        }
                        else
                        {
                            activeHediffs[pawn] = existingHediff;
                        }
                    }
                }
                
                // 如果已有Hediff，更新严重性和消失时间
                if (existingHediff != null && !existingHediff.pawn.Dead)
                {
                    // 更新严重性
                    existingHediff.Severity = severity;
                    
                    // 更新链接
                    HediffComp_Link hediffComp_Link = existingHediff.TryGetComp<HediffComp_Link>();
                    if (hediffComp_Link != null)
                    {
                        hediffComp_Link.drawConnection = true;
                        hediffComp_Link.other = parent.pawn;
                    }
                    
                    // 重置消失计时（使用较长时间避免闪烁）
                    HediffComp_Disappears hediffComp_Disappears = existingHediff.TryGetComp<HediffComp_Disappears>();
                    if (hediffComp_Disappears != null)
                    {
                        // 使用大于检查间隔的时间（例如60 tick）
                        hediffComp_Disappears.ticksToDisappear = Mathf.Max(hediffComp_Disappears.ticksToDisappear, 60);
                    }
                }
                else
                {
                    // 添加新的Hediff
                    Hediff hediff = pawn.health.AddHediff(Props.hediff);
                    if (hediff != null)
                    {
                        hediff.Severity = severity;
                        
                        // 设置链接
                        HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                        if (hediffComp_Link != null)
                        {
                            hediffComp_Link.drawConnection = true;
                            hediffComp_Link.other = parent.pawn;
                        }
                        
                        // 设置较长的消失时间（例如120 tick）
                        HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                        if (hediffComp_Disappears != null)
                        {
                            hediffComp_Disappears.ticksToDisappear = 120;
                        }
                        
                        activeHediffs[pawn] = hediff;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[DivineDiurganate] 应用Hediff到Pawn {pawn?.LabelShortCap ?? "未知"} 时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 移除Pawn的Hediff（如果存在）
        /// </summary>
        private void RemoveHediffIfExists(Pawn pawn)
        {
            try
            {
                if (activeHediffs.TryGetValue(pawn, out Hediff hediff))
                {
                    // 快速移除（不通过消失计时）
                    if (hediff != null && hediff.pawn != null && hediff.pawn.health != null)
                    {
                        hediff.pawn.health.RemoveHediff(hediff);
                    }
                    activeHediffs.Remove(pawn);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[DivineDiurganate] 移除Pawn {pawn?.LabelShortCap ?? "未知"} 的Hediff时出错: {ex.Message}");
                activeHediffs.Remove(pawn);
            }
        }
        
        /// <summary>
        /// 清理所有Hediff
        /// </summary>
        private void CleanupAllHediffs()
        {
            try
            {
                // 创建要处理的Pawn列表副本
                List<Pawn> pawnsToCleanup = new List<Pawn>(activeHediffs.Keys);
                
                foreach (Pawn pawn in pawnsToCleanup)
                {
                    if (pawn == null || pawn.Dead || pawn.health == null)
                    {
                        activeHediffs.Remove(pawn);
                        continue;
                    }
                    
                    RemoveHediffIfExists(pawn);
                }
                
                activeHediffs.Clear();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[DivineDiurganate] 清理所有Hediff时出错: {ex.Message}");
                activeHediffs.Clear();
            }
        }
        
        /// <summary>
        /// 清理过期的检查记录（避免内存泄漏）
        /// </summary>
        private void CleanupOldRecords()
        {
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                
                // 清理lastTickChecked字典
                List<Pawn> keysToRemove = new List<Pawn>();
                foreach (var kvp in lastTickChecked)
                {
                    if (kvp.Key == null || kvp.Key.Destroyed || !kvp.Key.Spawned || 
                        kvp.Key.Dead || currentTick - kvp.Value > 300) // 5秒未检查
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    lastTickChecked.Remove(key);
                }
                
                // 清理activeHediffs字典
                keysToRemove.Clear();
                foreach (var kvp in activeHediffs)
                {
                    if (kvp.Key == null || kvp.Key.Destroyed || !kvp.Key.Spawned || 
                        kvp.Key.Dead || kvp.Value == null || kvp.Value.pawn == null)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    activeHediffs.Remove(key);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[DivineDiurganate] 清理HediffComp记录时出错: {ex.Message}");
                lastTickChecked.Clear();
                activeHediffs.Clear();
            }
        }
        
        public override void CompPostMake()
        {
            base.CompPostMake();
            lastTickChecked = new Dictionary<Pawn, int>();
            activeHediffs = new Dictionary<Pawn, Hediff>();
        }
        
        public override void CompExposeData()
        {
            base.CompExposeData();
            
            // 保存和加载检查记录
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                CleanupOldRecords();
            }
            
            // 注意：我们不保存activeHediffs，因为Hediff已经保存在Pawn的健康状态中
        }
        
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            CleanupAllHediffs();
            
            if (mote != null)
            {
                mote.Destroy();
                mote = null;
            }
        }
    }
}
