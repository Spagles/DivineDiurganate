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
        public Dictionary<string, PawnRecord> pawnRecords = new Dictionary<string, PawnRecord>();
        
        // 待建立的关系队列
        private List<PendingRelation> pendingRelations = new List<PendingRelation>();
        
        // 存档ID，用于跨存档识别
        private string archiveId = null;
        
        // 日志记录
        private List<LogEntry> logEntries = new List<LogEntry>();
        private const int MAX_LOG_ENTRIES = 100;
        
        // 加载完成后是否已经初始化
        private bool initializedAfterLoad = false;
        
        public UniquePawnManager(Game game) : base()
        {
            instance = this;
            
            // 生成存档唯一ID（如果还没有）
            if (archiveId == null)
            {
                archiveId = GenerateArchiveId();
            }
            
            LogMessage($"UniquePawnManager 初始化，存档ID: {archiveId}", LogLevel.Info);
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            LogMessage($"ExposeData 调用，模式: {Scribe.mode}", LogLevel.Debug);
            
            // 保存存档ID
            Scribe_Values.Look(ref archiveId, "archiveId", null);
            
            // 如果存档ID为空，重新生成
            if (Scribe.mode == LoadSaveMode.PostLoadInit && archiveId == null)
            {
                archiveId = GenerateArchiveId();
            }
            
            // 保存Pawn记录 - 使用自定义的保存方式
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 将字典转换为可保存的格式
                List<string> keys = pawnRecords.Keys.ToList();
                List<PawnRecord> values = pawnRecords.Values.ToList();
                
                Scribe_Collections.Look(ref keys, "pawnRecordKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "pawnRecordValues", LookMode.Deep);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // 加载键值对
                List<string> keys = null;
                List<PawnRecord> values = null;
                
                Scribe_Collections.Look(ref keys, "pawnRecordKeys", LookMode.Value);
                Scribe_Collections.Look(ref values, "pawnRecordValues", LookMode.Deep);
                
                // 重建字典
                if (keys != null && values != null && keys.Count == values.Count)
                {
                    pawnRecords.Clear();
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (!pawnRecords.ContainsKey(keys[i]))
                        {
                            pawnRecords[keys[i]] = values[i];
                        }
                    }
                }
            }
            
            // 保存待处理关系
            Scribe_Collections.Look(ref pendingRelations, "pendingRelations", LookMode.Deep);
            
            // 保存日志（可选）
            if (Prefs.DevMode)
            {
                Scribe_Collections.Look(ref logEntries, "logEntries", LookMode.Deep);
            }
            
            // 如果加载时数据为空，重新初始化
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pawnRecords == null)
                {
                    pawnRecords = new Dictionary<string, PawnRecord>();
                    LogMessage("pawnRecords 为null，重新初始化", LogLevel.Warning);
                }
                
                if (pendingRelations == null)
                {
                    pendingRelations = new List<PendingRelation>();
                    LogMessage("pendingRelations 为null，重新初始化", LogLevel.Warning);
                }
                
                if (logEntries == null)
                {
                    logEntries = new List<LogEntry>();
                }
                
                // 标记为需要重建索引
                initializedAfterLoad = false;
                
                LogMessage($"加载完成，记录数: {pawnRecords?.Count ?? 0}, 待处理关系: {pendingRelations?.Count ?? 0}", 
                    LogLevel.Info);
            }
        }
        
        /// <summary>
        /// 生成存档唯一ID
        /// </summary>
        private string GenerateArchiveId()
        {
            // 使用游戏开始时间 + 随机数
            return $"{DateTime.Now.Ticks}_{new System.Random().Next(100000, 999999)}";
        }
        
        /// <summary>
        /// 游戏组件启动后调用
        /// </summary>
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            LogMessage("开始新游戏，重置管理器状态", LogLevel.Info);
            
            // 重置状态
            pawnRecords.Clear();
            pendingRelations.Clear();
            logEntries.Clear();
            initializedAfterLoad = true;
            
            // 重新生成存档ID
            archiveId = GenerateArchiveId();
        }
        
        /// <summary>
        /// 游戏加载完成后调用
        /// </summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            LogMessage("游戏加载完成，开始重建索引", LogLevel.Info);
            
            // 延迟重建，确保所有Pawn都已加载
            initializedAfterLoad = false;
        }
        
        /// <summary>
        /// 重建索引（确保在加载后执行）
        /// </summary>
        private void RebuildIndices()
        {
            if (initializedAfterLoad)
                return;
            
            LogMessage("开始重建索引...", LogLevel.Info);
            
            // 清理无效记录
            List<string> keysToRemove = new List<string>();
            int validCount = 0;
            
            foreach (var kvp in pawnRecords)
            {
                var record = kvp.Value;
                
                // 尝试查找Pawn（如果引用丢失）
                if (record.pawn == null && record.pawnID > 0)
                {
                    record.pawn = FindPawnByThingID(record.pawnID);
                    LogMessage($"重新查找Pawn: ID={record.pawnID}, 结果={(record.pawn != null ? "找到" : "未找到")}", 
                        LogLevel.Debug);
                }
                
                // 检查Pawn是否仍然有效
                if (record.pawn == null || record.pawn.Destroyed || !PawnExistsInWorld(record.pawn))
                {
                    keysToRemove.Add(kvp.Key);
                    
                    if (record.pawn != null)
                    {
                        LogMessage($"移除无效记录: {record.pawn.LabelCap} (ID={record.pawnID})", 
                            LogLevel.Warning);
                    }
                    else
                    {
                        LogMessage($"移除无效记录: ID={record.pawnID}", 
                            LogLevel.Warning);
                    }
                }
                else
                {
                    // 确保记录中的ID与Pawn的ID一致
                    record.pawnID = record.pawn.thingIDNumber;
                    validCount++;
                    
                    // 重新绑定Pawn死亡事件
                    if (record.extension != null && record.extension.removeOnDeath)
                    {
                        EnsureDeathEventHandler(record.pawn);
                    }
                }
            }
            
            // 移除无效记录
            foreach (var key in keysToRemove)
            {
                pawnRecords.Remove(key);
            }
            
            // 清理无效的待处理关系
            int pendingBefore = pendingRelations.Count;
            pendingRelations.RemoveAll(pr => 
                pr.sourcePawn == null || pr.sourcePawn.Destroyed || 
                !PawnExistsInWorld(pr.sourcePawn) ||
                (pr.targetPawnKind != null && !IsPawnKindValid(pr.targetPawnKind)));
            
            LogMessage($"重建完成: 有效记录 {validCount}/{pawnRecords.Count}, 清理待处理关系 {pendingBefore-pendingRelations.Count}", 
                LogLevel.Info);
            
            initializedAfterLoad = true;
        }
        
        /// <summary>
        /// 确保Pawn死亡事件处理器已注册
        /// </summary>
        private void EnsureDeathEventHandler(Pawn pawn)
        {
            if (pawn == null)
                return;
            
            // 使用Harmony补丁监听Pawn死亡事件
            // 这里我们通过检查Pawn是否死亡来间接处理
        }
        
        /// <summary>
        /// 根据ThingID查找Pawn
        /// </summary>
        private Pawn FindPawnByThingID(int thingID)
        {
            if (Current.Game == null)
                return null;
            
            // 在所有地图中查找
            if (Current.Game.Maps != null)
            {
                foreach (var map in Current.Game.Maps)
                {
                    if (map != null && map.mapPawns != null)
                    {
                        var pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == thingID);
                        if (pawn != null)
                            return pawn;
                    }
                }
            }
            
            // 在世界Pawn中查找
            if (Find.WorldPawns != null)
            {
                foreach (var pawn in Find.WorldPawns.AllPawnsAliveOrDead)
                {
                    if (pawn.thingIDNumber == thingID)
                        return pawn;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查Pawn是否存在于世界中
        /// </summary>
        private bool PawnExistsInWorld(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
                return false;
            
            // 检查是否在地图中
            if (pawn.Map != null && pawn.Map.mapPawns != null)
            {
                if (pawn.Map.mapPawns.AllPawns.Contains(pawn))
                    return true;
            }
            
            // 检查是否在世界Pawn中
            if (Find.WorldPawns != null && Find.WorldPawns.Contains(pawn))
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 检查PawnKindDef是否有效
        /// </summary>
        private bool IsPawnKindValid(PawnKindDef pawnKind)
        {
            return pawnKind != null && !pawnKind.defName.NullOrEmpty();
        }
        
        /// <summary>
        /// 每Tick更新
        /// </summary>
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 加载后首次Tick，重建索引
            if (!initializedAfterLoad && Find.TickManager.TicksGame > 10)
            {
                RebuildIndices();
            }
            
            // 每60Tick处理一次待处理关系
            if (Find.TickManager.TicksGame % 60 == 0 && initializedAfterLoad)
            {
                ProcessPendingRelations();
                
                // 清理旧日志（每300Tick清理一次）
                if (Find.TickManager.TicksGame % 300 == 0)
                {
                    CleanupOldLogs();
                }
                
                // 定期检查Pawn有效性
                if (Find.TickManager.TicksGame % 1200 == 0)
                {
                    CheckPawnValidity();
                }
            }
        }
        
        /// <summary>
        /// 处理待处理的关系
        /// </summary>
        private void ProcessPendingRelations()
        {
            if (pendingRelations.Count == 0 || !initializedAfterLoad)
                return;
            
            List<PendingRelation> processed = new List<PendingRelation>();
            
            foreach (var pendingRelation in pendingRelations.ToList())
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
                pendingRelation.relationDef, pendingRelation.direction);
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
                if (map != null && map.mapPawns != null)
                {
                    foreach (var pawn in map.mapPawns.AllPawns)
                    {
                        if (pawn != null && pawn.kindDef == pawnKindDef && IsPawnValid(pawn))
                        {
                            result.Add(pawn);
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 检查Pawn是否有效
        /// </summary>
        private bool IsPawnValid(Pawn pawn)
        {
            return pawn != null && !pawn.Destroyed && pawn.Spawned;
        }
        
        /// <summary>
        /// 检查Pawn有效性
        /// </summary>
        private void CheckPawnValidity()
        {
            int removed = 0;
            List<string> keysToRemove = new List<string>();
            
            foreach (var kvp in pawnRecords)
            {
                var record = kvp.Value;
                
                // 如果Pawn已死亡且需要移除
                if (record.pawn != null && record.pawn.Dead && 
                    record.extension != null && record.extension.removeOnDeath)
                {
                    keysToRemove.Add(kvp.Key);
                    removed++;
                }
                // 如果Pawn已不存在于世界中
                else if (!PawnExistsInWorld(record.pawn))
                {
                    keysToRemove.Add(kvp.Key);
                    removed++;
                }
            }
            
            // 移除无效记录
            foreach (var key in keysToRemove)
            {
                pawnRecords.Remove(key);
            }
            
            if (removed > 0)
            {
                LogMessage($"清理了 {removed} 个无效Pawn记录", LogLevel.Info);
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
        /// 获取Pawn的唯一键
        /// </summary>
        private string GetPawnKey(Pawn pawn, UniquePawnScope scope)
        {
            if (pawn?.kindDef == null)
                return null;
            
            string baseKey = pawn.kindDef.defName;
            
            switch (scope)
            {
                case UniquePawnScope.Map:
                    // 使用地图的索引作为标识
                    return $"{baseKey}_Map_{pawn.Map?.Index ?? 0}";
                case UniquePawnScope.World:
                    return $"{baseKey}_World";
                case UniquePawnScope.Archive:
                    // 使用存档的唯一标识
                    return $"{baseKey}_Archive_{archiveId}";
                default:
                    return baseKey;
            }
        }
        
        /// <summary>
        /// 注册特殊Pawn
        /// </summary>
        public bool RegisterUniquePawn(Pawn pawn, UniquePawnExtension extension)
        {
            if (pawn == null || extension == null)
                return false;
            
            // 确保已经初始化
            if (!initializedAfterLoad)
            {
                LogMessage($"管理器未初始化，延迟注册Pawn: {pawn.LabelCap}", LogLevel.Warning);
                return false;
            }
            
            string pawnKey = GetPawnKey(pawn, extension.singletonScope);
            if (pawnKey == null)
            {
                LogMessage($"无法生成PawnKey: {pawn.LabelCap}", LogLevel.Error);
                return false;
            }
            
            // 检查是否已经是单例
            if (extension.isSingleton)
            {
                // 检查是否已存在相同Key的Pawn
                if (pawnRecords.TryGetValue(pawnKey, out var existingRecord))
                {
                    if (existingRecord.pawn != null && 
                        existingRecord.pawn != pawn && 
                        PawnExistsInWorld(existingRecord.pawn))
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
            
            pawnRecords[pawnKey] = record;
            
            LogMessage($"已注册特殊Pawn: {pawn.LabelCap} ({pawn.kindDef.label}), Key: {pawnKey}", 
                extension.logLevel);
            
            // 处理关系
            ProcessPawnRelations(pawn, extension);
            
            // 发送生成消息
            if (extension.showSpawnMessage)
            {
                SendSpawnMessage(pawn, extension);
            }
            
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
        /// 检查地图中是否存在指定类型的Pawn
        /// </summary>
        public bool CheckPawnExistsInMap(PawnKindDef pawnKindDef, Map map)
        {
            if (pawnKindDef == null || map == null)
                return false;
            
            string baseKey = pawnKindDef.defName;
            string key = $"{baseKey}_Map_{map.Index}";
            
            return pawnRecords.ContainsKey(key) && 
                   pawnRecords[key]?.pawn != null && 
                   IsPawnValid(pawnRecords[key].pawn);
        }
        
        /// <summary>
        /// 检查世界中是否存在指定类型的Pawn
        /// </summary>
        public bool CheckPawnExistsInWorld(PawnKindDef pawnKindDef)
        {
            if (pawnKindDef == null)
                return false;
            
            string baseKey = pawnKindDef.defName;
            
            foreach (var record in pawnRecords.Values)
            {
                if (record.pawnKindDef == pawnKindDef && record.pawn != null && IsPawnValid(record.pawn))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查存档中是否存在指定类型的Pawn
        /// </summary>
        public bool CheckPawnExistsInArchive(PawnKindDef pawnKindDef)
        {
            // 与CheckPawnExistsInWorld相同，因为管理器只管理当前存档
            return CheckPawnExistsInWorld(pawnKindDef);
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
                            fixedRelation.direction);
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
                            autoRelation.direction);
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
                if (map == null || map.mapPawns == null)
                    continue;
                    
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn == null)
                        continue;
                        
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
            PawnRelationDef relationDef, RelationDirection direction)
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
        /// 获取统计数据
        /// </summary>
        public string GetStatistics()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 特殊Pawn管理器统计 ===");
            sb.AppendLine($"存档ID: {archiveId}");
            sb.AppendLine($"初始化状态: {initializedAfterLoad}");
            sb.AppendLine($"已注册Pawn数量: {pawnRecords.Count}");
            sb.AppendLine($"待处理关系: {pendingRelations.Count}");
            sb.AppendLine($"日志条目: {logEntries.Count}");
            sb.AppendLine();
            
            sb.AppendLine("已注册的特殊Pawn:");
            foreach (var kvp in pawnRecords)
            {
                var record = kvp.Value;
                sb.AppendLine($"  • {record.pawn?.LabelCap ?? "未知"} ({record.pawnKindDef?.label ?? "未知"})");
                sb.AppendLine($"    键: {kvp.Key}");
                sb.AppendLine($"    ID: {record.pawnID}");
                sb.AppendLine($"    生成时间: {record.spawnTime}");
            }
            
            return sb.ToString();
        }
    }
    
    /// <summary>
    /// Pawn记录
    /// </summary>
    public class PawnRecord : IExposable
    {
        [NonSerialized]
        private Pawn _pawn;
        public Pawn pawn 
        { 
            get => _pawn;
            set 
            { 
                _pawn = value;
                if (value != null)
                    pawnID = value.thingIDNumber;
            }
        }
        
        public int pawnID;
        public PawnKindDef pawnKindDef;
        public UniquePawnExtension extension;
        public int spawnTime;
        public List<FixedRelation> establishedRelations = new List<FixedRelation>();
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnID, "pawnID", 0);
            Scribe_Defs.Look(ref pawnKindDef, "pawnKindDef");
            Scribe_Deep.Look(ref extension, "extension");
            Scribe_Values.Look(ref spawnTime, "spawnTime", 0);
            Scribe_Collections.Look(ref establishedRelations, "establishedRelations", LookMode.Deep);
            
            // 注意：我们不保存pawn引用，因为它在加载时需要重新查找
            // pawn引用会在加载后由管理器重建
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
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref message, "message");
            Scribe_Values.Look(ref level, "level");
        }
    }
}
