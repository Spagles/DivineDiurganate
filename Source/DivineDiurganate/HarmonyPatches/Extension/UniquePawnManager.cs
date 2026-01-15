using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 特殊Pawn管理器
    /// 管理所有特殊单例Pawn的生成、关系和销毁
    /// </summary>
    public class UniquePawnManager : GameComponent
    {
        // 单例实例
        private static UniquePawnManager instance;
        public static UniquePawnManager Instance => instance;
        
        // 已生成的单例Pawn记录
        private Dictionary<string, PawnRecord> pawnRecords = new Dictionary<string, PawnRecord>();
        
        // 待建立的关系队列
        private List<PendingRelation> pendingRelations = new List<PendingRelation>();
        
        // 日志记录
        private List<LogEntry> logEntries = new List<LogEntry>();
        private const int MAX_LOG_ENTRIES = 100;
        
        public UniquePawnManager(Game game)
        {
            instance = this;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            // 保存Pawn记录
            Scribe_Collections.Look(ref pawnRecords, "pawnRecords", LookMode.Value, LookMode.Deep);
            
            // 保存待处理关系
            Scribe_Collections.Look(ref pendingRelations, "pendingRelations", LookMode.Deep);
            
            // 如果加载时pawnRecords为null，重新初始化
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pawnRecords == null)
            {
                pawnRecords = new Dictionary<string, PawnRecord>();
                pendingRelations = new List<PendingRelation>();
            }
            
            // 重建索引（加载后）
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RebuildIndices();
            }
        }
        
        /// <summary>
        /// 重建索引（加载存档后）
        /// </summary>
        private void RebuildIndices()
        {
            // 清理无效记录
            List<string> keysToRemove = new List<string>();
            
            foreach (var kvp in pawnRecords)
            {
                var record = kvp.Value;
                
                // 检查Pawn是否仍然存在
                if (record.pawn == null || record.pawn.Destroyed || !record.pawn.Spawned)
                {
                    keysToRemove.Add(kvp.Key);
                    continue;
                }
                
                // 更新记录的Pawn引用
                record.pawn = FindPawnByThingID(record.pawnID);
                if (record.pawn == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            // 移除无效记录
            foreach (var key in keysToRemove)
            {
                pawnRecords.Remove(key);
            }
            
            // 清理无效的待处理关系
            pendingRelations.RemoveAll(pr => 
                !IsPawnValid(pr.sourcePawn) || 
                (pr.targetPawnKind != null && !IsPawnKindValid(pr.targetPawnKind)));
        }
        
        /// <summary>
        /// 根据ThingID查找Pawn
        /// </summary>
        private Pawn FindPawnByThingID(int thingID)
        {
            if (Current.Game == null || Current.Game.Maps == null)
                return null;
            
            foreach (var map in Current.Game.Maps)
            {
                var pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == thingID);
                if (pawn != null)
                    return pawn;
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查Pawn是否有效
        /// </summary>
        private bool IsPawnValid(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && pawn.Spawned;
        }
        
        /// <summary>
        /// 检查PawnKindDef是否有效
        /// </summary>
        private bool IsPawnKindValid(PawnKindDef pawnKind)
        {
            return pawnKind != null;
        }
        
        /// <summary>
        /// 每Tick更新
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 每60Tick处理一次待处理关系
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                ProcessPendingRelations();
                
                // 清理旧日志（每300Tick清理一次）
                if (Find.TickManager.TicksGame % 300 == 0)
                {
                    CleanupOldLogs();
                }
            }
        }
        
        /// <summary>
        /// 处理待处理的关系
        /// </summary>
        private void ProcessPendingRelations()
        {
            if (pendingRelations.Count == 0)
                return;
            
            List<PendingRelation> processed = new List<PendingRelation>();
            
            foreach (var pendingRelation in pendingRelations)
            {
                try
                {
                    if (TryEstablishPendingRelation(pendingRelation))
                    {
                        processed.Add(pendingRelation);
                        
                        LogMessage($"已建立待处理关系: {pendingRelation.sourcePawn?.LabelShort} -> {pendingRelation.targetPawnKind?.label}",
                            LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"处理待处理关系时出错: {ex}", LogLevel.Error);
                }
            }
            
            // 移除已处理的关系
            foreach (var relation in processed)
            {
                pendingRelations.Remove(relation);
            }
        }
        
        /// <summary>
        /// 尝试建立待处理关系
        /// </summary>
        private bool TryEstablishPendingRelation(PendingRelation pendingRelation)
        {
            if (!IsPawnValid(pendingRelation.sourcePawn))
                return false;
            
            // 查找目标Pawn
            var targetPawns = FindPawnsByKindDef(pendingRelation.targetPawnKind);
            if (targetPawns.Count == 0)
                return false;
            
            var targetPawn = targetPawns.FirstOrDefault();
            if (targetPawn == null)
                return false;
            
            // 建立关系
            return EstablishRelation(pendingRelation.sourcePawn, targetPawn, 
                pendingRelation.relationDef, pendingRelation.direction, 
                pendingRelation.relationStrength);
        }
        
        /// <summary>
        /// 根据PawnKindDef查找Pawn
        /// </summary>
        private List<Pawn> FindPawnsByKindDef(PawnKindDef pawnKindDef)
        {
            var result = new List<Pawn>();
            
            if (pawnKindDef == null || Current.Game == null || Current.Game.Maps == null)
                return result;
            
            foreach (var map in Current.Game.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.kindDef == pawnKindDef && IsPawnValid(pawn))
                    {
                        result.Add(pawn);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 注册特殊Pawn
        /// </summary>
        public bool RegisterUniquePawn(Pawn pawn, UniquePawnExtension extension)
        {
            if (pawn == null || extension == null)
                return false;
            
            // 检查是否已经是单例
            if (extension.isSingleton)
            {
                string pawnKey = GetPawnKey(pawn, extension.singletonScope);
                
                // 检查是否已存在相同Key的Pawn
                if (pawnRecords.ContainsKey(pawnKey))
                {
                    var existingRecord = pawnRecords[pawnKey];
                    
                    if (existingRecord.pawn != null && 
                        existingRecord.pawn != pawn && 
                        IsPawnValid(existingRecord.pawn))
                    {
                        // 处理重复生成
                        return HandleDuplicatePawn(pawn, existingRecord.pawn, extension);
                    }
                }
            }
            
            // 创建新记录
            var record = new PawnRecord
            {
                pawn = pawn,
                pawnID = pawn.thingIDNumber,
                pawnKindDef = pawn.kindDef,
                extension = extension,
                spawnTime = Find.TickManager.TicksGame
            };
            
            string key = GetPawnKey(pawn, extension.singletonScope);
            pawnRecords[key] = record;
            
            // 处理关系
            ProcessPawnRelations(pawn, extension);
            
            // 发送生成消息
            if (extension.showSpawnMessage)
            {
                SendSpawnMessage(pawn, extension);
            }
            
            LogMessage($"已注册特殊Pawn: {pawn.LabelCap} ({pawn.kindDef.label})", 
                extension.logLevel);
            
            return true;
        }
        
        /// <summary>
        /// 处理重复Pawn
        /// </summary>
        private bool HandleDuplicatePawn(Pawn newPawn, Pawn existingPawn, UniquePawnExtension extension)
        {
            if (extension.destroyDuplicates)
            {
                // 销毁新生成的Pawn
                if (newPawn.Spawned)
                {
                    newPawn.Destroy();
                }
                
                LogMessage($"销毁重复的特殊Pawn: {newPawn.LabelCap} (已存在: {existingPawn.LabelCap})", 
                    extension.logLevel);
                
                return false;
            }
            else
            {
                // 阻止生成（理论上不会执行到这里，因为生成已经被拦截）
                return false;
            }
        }
        
        /// <summary>
        /// 获取Pawn的唯一键
        /// </summary>
        private string GetPawnKey(Pawn pawn, UniquePawnScope scope)
        {
            string baseKey = pawn.kindDef.defName;
            
            switch (scope)
            {
                case UniquePawnScope.Map:
                    return $"{baseKey}_{pawn.Map?.uniqueID ?? 0}";
                case UniquePawnScope.World:
                    return baseKey;
                case UniquePawnScope.Archive:
                    return $"{baseKey}_{Current.Game?.uniqueID ?? 0}";
                default:
                    return baseKey;
            }
        }
        
        /// <summary>
        /// 处理Pawn的关系
        /// </summary>
        private void ProcessPawnRelations(Pawn pawn, UniquePawnExtension extension)
        {
            if (pawn == null || extension == null)
                return;
            
            try
            {
                // 1. 清理现有关系（如果需要）
                if (extension.clearExistingRelations)
                {
                    ClearNonFixedRelations(pawn, extension);
                }
                
                // 2. 建立固定关系
                EstablishFixedRelations(pawn, extension);
                
                // 3. 建立自动关系
                EstablishAutoRelations(pawn, extension);
            }
            catch (Exception ex)
            {
                LogMessage($"处理Pawn关系时出错 ({pawn.LabelShort}): {ex}", LogLevel.Error);
            }
        }
        
        /// <summary>
        /// 清理非固定关系
        /// </summary>
        private void ClearNonFixedRelations(Pawn pawn, UniquePawnExtension extension)
        {
            if (pawn.relations == null)
                return;
            
            // 获取所有现有关系
            var relations = pawn.relations.DirectRelations.ToList();
            
            foreach (var relation in relations)
            {
                // 检查是否需要保留
                bool shouldKeep = ShouldKeepRelation(relation, pawn, extension);
                
                if (!shouldKeep)
                {
                    pawn.relations.RemoveDirectRelation(relation.def, relation.otherPawn);
                    
                    LogMessage($"移除关系: {pawn.LabelShort} 的 {relation.def.label} 关系",
                        extension.logLevel);
                }
            }
        }
        
        /// <summary>
        /// 检查关系是否需要保留
        /// </summary>
        private bool ShouldKeepRelation(DirectPawnRelation relation, Pawn pawn, UniquePawnExtension extension)
        {
            // 如果relationsToKeep为空，保留所有关系
            if (extension.relationsToKeep == null || extension.relationsToKeep.Count == 0)
                return true;
            
            // 检查是否在保留列表中
            foreach (var relationDef in extension.relationsToKeep)
            {
                if (relation.def == relationDef)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 建立固定关系
        /// </summary>
        private void EstablishFixedRelations(Pawn pawn, UniquePawnExtension extension)
        {
            if (extension.fixedRelations == null)
                return;
            
            foreach (var fixedRelation in extension.fixedRelations)
            {
                try
                {
                    if (fixedRelation.targetPawnKind == null || fixedRelation.relationDef == null)
                        continue;
                    
                    // 查找目标Pawn
                    var targetPawns = FindPawnsByKindDef(fixedRelation.targetPawnKind);
                    
                    if (targetPawns.Count > 0)
                    {
                        // 建立与第一个找到的目标Pawn的关系
                        var targetPawn = targetPawns.First();
                        EstablishRelation(pawn, targetPawn, fixedRelation.relationDef, 
                            fixedRelation.direction, fixedRelation.relationStrength);
                    }
                    else
                    {
                        // 如果没有找到目标Pawn，记录为待处理关系
                        if (fixedRelation.autoEstablish)
                        {
                            var pending = new PendingRelation
                            {
                                sourcePawn = pawn,
                                targetPawnKind = fixedRelation.targetPawnKind,
                                relationDef = fixedRelation.relationDef,
                                direction = fixedRelation.direction,
                                relationStrength = fixedRelation.relationStrength
                            };
                            
                            pendingRelations.Add(pending);
                            
                            LogMessage($"记录待处理关系: {pawn.LabelShort} -> {fixedRelation.targetPawnKind.label}",
                                extension.logLevel);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"建立固定关系时出错: {ex}", LogLevel.Error);
                }
            }
        }
        
        /// <summary>
        /// 建立自动关系
        /// </summary>
        private void EstablishAutoRelations(Pawn pawn, UniquePawnExtension extension)
        {
            if (extension.autoRelations == null)
                return;
            
            foreach (var autoRelation in extension.autoRelations)
            {
                try
                {
                    if (autoRelation.relationDef == null)
                        continue;
                    
                    // 查找符合条件的Pawn
                    var candidatePawns = FindCandidatePawns(pawn, autoRelation);
                    
                    // 限制关系数量
                    if (autoRelation.maxRelations > 0 && candidatePawns.Count > autoRelation.maxRelations)
                    {
                        candidatePawns = candidatePawns.Take(autoRelation.maxRelations).ToList();
                    }
                    
                    // 建立关系
                    foreach (var candidatePawn in candidatePawns)
                    {
                        EstablishRelation(pawn, candidatePawn, autoRelation.relationDef, 
                            autoRelation.direction, autoRelation.relationStrength);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"建立自动关系时出错: {ex}", LogLevel.Error);
                }
            }
        }
        
        /// <summary>
        /// 查找符合条件的Pawn
        /// </summary>
        private List<Pawn> FindCandidatePawns(Pawn sourcePawn, AutoRelation autoRelation)
        {
            var candidates = new List<Pawn>();
            
            if (Current.Game == null || Current.Game.Maps == null)
                return candidates;
            
            foreach (var map in Current.Game.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    // 跳过自己
                    if (pawn == sourcePawn)
                        continue;
                    
                    // 检查PawnKindDef（如果指定了）
                    if (autoRelation.targetPawnKind != null && pawn.kindDef != autoRelation.targetPawnKind)
                        continue;
                    
                    // 检查筛选条件
                    if (autoRelation.filter != null && !autoRelation.filter.Matches(pawn))
                        continue;
                    
                    // 检查是否已有相同类型的关系
                    if (HasRelation(sourcePawn, pawn, autoRelation.relationDef))
                        continue;
                    
                    candidates.Add(pawn);
                }
            }
            
            return candidates;
        }
        
        /// <summary>
        /// 检查是否已有关系
        /// </summary>
        private bool HasRelation(Pawn pawn1, Pawn pawn2, PawnRelationDef relationDef)
        {
            if (pawn1.relations == null || pawn2.relations == null)
                return false;
            
            return pawn1.relations.DirectRelationExists(relationDef, pawn2);
        }
        
        /// <summary>
        /// 建立关系
        /// </summary>
        private bool EstablishRelation(Pawn sourcePawn, Pawn targetPawn, 
            PawnRelationDef relationDef, RelationDirection direction, float strength)
        {
            if (sourcePawn == null || targetPawn == null || relationDef == null)
                return false;
            
            try
            {
                switch (direction)
                {
                    case RelationDirection.SourceToTarget:
                        sourcePawn.relations.AddDirectRelation(relationDef, targetPawn);
                        break;
                        
                    case RelationDirection.TargetToSource:
                        targetPawn.relations.AddDirectRelation(relationDef, sourcePawn);
                        break;
                        
                    case RelationDirection.Bidirectional:
                        sourcePawn.relations.AddDirectRelation(relationDef, targetPawn);
                        targetPawn.relations.AddDirectRelation(relationDef, sourcePawn);
                        break;
                }
                
                // 设置关系强度（如果支持）
                SetRelationStrength(sourcePawn, targetPawn, relationDef, strength);
                
                LogMessage($"已建立关系: {sourcePawn.LabelShort} <-> {targetPawn.LabelShort} ({relationDef.label})", 
                    LogLevel.Info);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"建立关系时出错: {ex}", LogLevel.Error);
                return false;
            }
        }
        
        /// <summary>
        /// 设置关系强度
        /// </summary>
        private void SetRelationStrength(Pawn pawn1, Pawn pawn2, PawnRelationDef relationDef, float strength)
        {
            // 注意：RimWorld原生不支持直接设置关系强度
            // 这里可以扩展或使用自定义系统
            // 目前作为占位符
        }
        
        /// <summary>
        /// 发送生成消息
        /// </summary>
        private void SendSpawnMessage(Pawn pawn, UniquePawnExtension extension)
        {
            string messageKey = extension.spawnMessageKey;
            string defaultMessage = $"{pawn.LabelCap} 已出现！";
            
            Messages.Message(defaultMessage, pawn, MessageTypeDefOf.PositiveEvent);
        }
        
        /// <summary>
        /// 检查Pawn是否为特殊Pawn
        /// </summary>
        public bool IsUniquePawn(Pawn pawn)
        {
            if (pawn == null || pawn.kindDef == null)
                return false;
            
            var extension = pawn.kindDef.GetModExtension<UniquePawnExtension>();
            return extension != null && extension.isSingleton;
        }
        
        /// <summary>
        /// 获取特殊Pawn的记录
        /// </summary>
        public PawnRecord GetPawnRecord(Pawn pawn)
        {
            if (pawn == null || pawn.kindDef == null)
                return null;
            
            var extension = pawn.kindDef.GetModExtension<UniquePawnExtension>();
            if (extension == null)
                return null;
            
            string key = GetPawnKey(pawn, extension.singletonScope);
            pawnRecords.TryGetValue(key, out var record);
            
            return record;
        }
        
        /// <summary>
        /// 移除Pawn记录
        /// </summary>
        public void RemovePawnRecord(Pawn pawn)
        {
            if (pawn == null || pawn.kindDef == null)
                return;
            
            var extension = pawn.kindDef.GetModExtension<UniquePawnExtension>();
            if (extension == null)
                return;
            
            string key = GetPawnKey(pawn, extension.singletonScope);
            pawnRecords.Remove(key);
            
            // 清理相关待处理关系
            pendingRelations.RemoveAll(pr => pr.sourcePawn == pawn);
            
            LogMessage($"移除Pawn记录: {pawn.LabelCap}", LogLevel.Info);
        }
        
        /// <summary>
        /// 记录日志消息
        /// </summary>
        public void LogMessage(string message, LogLevel level)
        {
            if (level == LogLevel.None)
                return;
            
            var entry = new LogEntry
            {
                tick = Find.TickManager.TicksGame,
                message = message,
                level = level
            };
            
            logEntries.Add(entry);
            
            // 控制台日志
            switch (level)
            {
                case LogLevel.Error:
                    Log.Error($"[UniquePawnManager] {message}");
                    break;
                case LogLevel.Warning:
                    Log.Warning($"[UniquePawnManager] {message}");
                    break;
                case LogLevel.Info:
                case LogLevel.Debug:
                    if (Prefs.DevMode)
                        Log.Message($"[UniquePawnManager] {message}");
                    break;
            }
        }
        
        /// <summary>
        /// 清理旧日志
        /// </summary>
        private void CleanupOldLogs()
        {
            if (logEntries.Count > MAX_LOG_ENTRIES)
            {
                logEntries = logEntries.Skip(logEntries.Count - MAX_LOG_ENTRIES).ToList();
            }
        }
        
        /// <summary>
        /// 获取统计数据
        /// </summary>
        public string GetStatistics()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 特殊Pawn管理器统计 ===");
            sb.AppendLine($"已注册Pawn数量: {pawnRecords.Count}");
            sb.AppendLine($"待处理关系: {pendingRelations.Count}");
            sb.AppendLine($"日志条目: {logEntries.Count}");
            sb.AppendLine();
            
            sb.AppendLine("已注册的特殊Pawn:");
            foreach (var kvp in pawnRecords)
            {
                var record = kvp.Value;
                sb.AppendLine($"  • {record.pawn?.LabelCap ?? "未知"} ({record.pawnKindDef?.label ?? "未知"})");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Pawn记录
    /// </summary>
    public class PawnRecord : IExposable
    {
        public Pawn pawn;
        public int pawnID;
        public PawnKindDef pawnKindDef;
        public UniquePawnExtension extension;
        public int spawnTime;
        public List<FixedRelation> establishedRelations = new List<FixedRelation>();
        
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref pawnID, "pawnID");
            Scribe_Defs.Look(ref pawnKindDef, "pawnKindDef");
            Scribe_Deep.Look(ref extension, "extension");
            Scribe_Values.Look(ref spawnTime, "spawnTime");
            Scribe_Collections.Look(ref establishedRelations, "establishedRelations", LookMode.Deep);
        }
    }
    
    /// <summary>
    /// 待处理关系
    /// </summary>
    public class PendingRelation : IExposable
    {
        public Pawn sourcePawn;
        public PawnKindDef targetPawnKind;
        public PawnRelationDef relationDef;
        public RelationDirection direction;
        public float relationStrength;
        
        public void ExposeData()
        {
            Scribe_References.Look(ref sourcePawn, "sourcePawn");
            Scribe_Defs.Look(ref targetPawnKind, "targetPawnKind");
            Scribe_Defs.Look(ref relationDef, "relationDef");
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref relationStrength, "relationStrength");
        }
    }
    
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        public int tick;
        public string message;
        public LogLevel level;
    }
}
