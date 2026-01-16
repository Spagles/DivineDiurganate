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
                
                // 重建字典
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
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
            }
            else
            {
                // 直接保存和加载字典
                Scribe_Collections.Look(ref pawnRecords, "pawnRecords", LookMode.Value, LookMode.Deep);
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
            
            // 检查是否已经注册了事件处理器
            bool hasHandler = false;
            
            // 这里可以使用反射检查，但为了简单，我们每次重新注册
            // 移除旧的事件处理器（如果有）
            // 然后添加新的事件处理器
            
            // 使用Harmony补丁或直接监听事件
            // 由于事件监听比较复杂，这里我们通过每Tick检查来实现
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
                    var pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == thingID);
                    if (pawn != null)
                        return pawn;
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
        
        // ... [其他方法保持不变，但需要更新GetPawnKey方法以使用archiveId] ...
        
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
        /// 注册特殊Pawn（更新版）
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
        
        // ... [其他方法保持不变] ...
        
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
    /// Pawn记录（更新版）
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
    
    // ... [其他类保持不变] ...
}
