using RimWorld;
using System.Collections.Generic;
using System.Linq;  // 添加 using
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    public class HediffComp_GiveHediffsInRangeToPawnKind : HediffComp
    {
        private Mote mote;
        private Dictionary<Pawn, int> lastTickChecked = new Dictionary<Pawn, int>();

        public HediffCompProperties_GiveHediffsInRangeToPawnKind Props => (HediffCompProperties_GiveHediffsInRangeToPawnKind)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!parent.pawn.Awake() || parent.pawn.health == null || parent.pawn.health.InPainShock || !parent.pawn.Spawned)
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
            
            // 获取范围内的Pawn - 使用 ToList() 创建副本
            List<Pawn> pawnsList = null;
            if (Props.onlyPawnsInSameFaction && parent.pawn.Faction != null)
            {
                pawnsList = parent.pawn.Map.mapPawns.SpawnedPawnsInFaction(parent.pawn.Faction).ToList();
            }
            else
            {
                pawnsList = parent.pawn.Map.mapPawns.AllPawnsSpawned.ToList();
            }
            
            int currentTick = Find.TickManager.TicksGame;
            
            foreach (Pawn pawn in pawnsList)
            {
                // 跳过无效的Pawn
                if (pawn == null || pawn.Dead || pawn.health == null || pawn == parent.pawn)
                    continue;
                
                // 检查距离
                if (pawn.Position.DistanceTo(parent.pawn.Position) > Props.range)
                    continue;
                
                // 检查目标参数
                if (Props.targetingParameters != null && !Props.targetingParameters.CanTarget(pawn))
                    continue;
                
                // 限制检查频率（每30 tick检查一次）
                if (lastTickChecked.TryGetValue(pawn, out int lastTick) && currentTick - lastTick < 30)
                    continue;
                
                lastTickChecked[pawn] = currentTick;
                
                // 根据PawnKindDef获取对应的严重性
                float severity = Props.GetSeverityForPawnKind(pawn.kindDef);
                
                // 如果严重性为0，则跳过
                if (severity <= 0f)
                    continue;
                
                // 添加或更新Hediff
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                if (hediff == null)
                {
                    // 添加新的Hediff
                    hediff = pawn.health.AddHediff(Props.hediff);
                    hediff.Severity = severity;
                    
                    // 如果有链接组件，设置链接
                    HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                    if (hediffComp_Link != null)
                    {
                        hediffComp_Link.drawConnection = true;
                        hediffComp_Link.other = parent.pawn;
                    }
                }
                else
                {
                    // 更新现有的Hediff严重性
                    hediff.Severity = severity;
                }
                
                // 如果有消失组件，重置消失计时
                HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (hediffComp_Disappears != null)
                {
                    hediffComp_Disappears.ticksToDisappear = 5;
                }
            }
            
            // 清理过期的检查记录（避免内存泄漏）
            if (currentTick % 600 == 0) // 每10秒清理一次
            {
                CleanupOldRecords();
            }
        }
        
        /// <summary>
        /// 清理过期的检查记录
        /// </summary>
        private void CleanupOldRecords()
        {
            try
            {
                // 创建要删除的键列表，避免在遍历时修改字典
                List<Pawn> keysToRemove = new List<Pawn>();
                
                // 首先收集所有要删除的键
                foreach (var kvp in lastTickChecked)
                {
                    if (kvp.Key == null || kvp.Key.Destroyed || !kvp.Key.Spawned)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                // 然后删除这些键
                foreach (var key in keysToRemove)
                {
                    lastTickChecked.Remove(key);
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[DivineDiurganate] 清理HediffComp记录时出错: {ex.Message}");
            }
        }
        
        public override void CompPostMake()
        {
            base.CompPostMake();
            lastTickChecked = new Dictionary<Pawn, int>();
        }
        
        public override void CompExposeData()
        {
            base.CompExposeData();
            
            // 保存和加载检查记录
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                CleanupOldRecords();
            }
        }
    }
}
