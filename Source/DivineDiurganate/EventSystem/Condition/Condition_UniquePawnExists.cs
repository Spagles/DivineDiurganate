using Verse;
using RimWorld;
using RimWorld.Planet;

namespace DivineDiurganate
{
    /// <summary>
    /// 检查特殊Pawn是否存在的条件
    /// </summary>
    public class Condition_UniquePawnExists : ConditionBase
    {
        // 要检查的PawnKindDef
        public PawnKindDef pawnKindDef;
        
        // 检查范围（可选，默认为世界范围）
        public UniquePawnScope checkScope = UniquePawnScope.World;
        
        // 是否需要Pawn存活（true=必须存活，false=已注册即可）
        public bool requireAlive = true;
        
        // 需要检查的特定地图（如果checkScope为Map，则需要指定）
        public Map specificMap;
        
        // 是否要求Pawn在地图上（requireAlive为true时有效）
        public bool requireOnMap = true;
        
        // 自定义失败原因
        public string customFailReason;

        // 结果反转
        public bool resultReverse = false;

        public override bool IsMet(out string reason)
        {
            // 验证参数
            if (pawnKindDef == null)
            {
                reason = "PawnKindDef is not specified in Condition_UniquePawnExists.";
                return false;
            }
            
            // 获取UniquePawnManager实例
            var manager = UniquePawnManager.Instance;
            if (manager == null)
            {
                reason = "UniquePawnManager is not initialized.";
                return false;
            }
            
            bool exists = false;
            string details = "";
            
            // 根据检查范围判断Pawn是否存在
            switch (checkScope)
            {
                case UniquePawnScope.World:
                    exists = CheckExistsInWorld(out details);
                    break;
                    
                case UniquePawnScope.Archive:
                    exists = CheckExistsInArchive(out details);
                    break;
                    
                default:
                    reason = $"Unknown check scope: {checkScope}";
                    return false;
            }
            
            // 构建返回结果
            if (exists)
            {
                if (resultReverse)
                {
                    if (!string.IsNullOrEmpty(customFailReason))
                    {
                        reason = customFailReason.Translate();
                    }
                    else
                    {
                        reason = $"特殊Pawn '{pawnKindDef.label}' 存在。{details}";
                    }
                    return false;
                }
                else
                {
                    reason = $"特殊Pawn '{pawnKindDef.label}' 存在。{details}";
                    return true;
                }
            }
            else
            {
                if (resultReverse)
                {
                    reason = $"特殊Pawn '{pawnKindDef.label}' 不存在。{details}";
                    return true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(customFailReason))
                    {
                        reason = customFailReason.Translate();
                    }
                    else
                    {
                        reason = $"特殊Pawn '{pawnKindDef.label}' 不存在。{details}";
                    }
                    return false;
                }
            }
        }
        
        /// <summary>
        /// 检查世界中是否存在
        /// </summary>
        private bool CheckExistsInWorld(out string details)
        {
            details = "";
            
            var manager = UniquePawnManager.Instance;
            if (manager == null)
                return false;
            
            // 检查世界中是否存在该Pawn
            bool exists = manager.CheckPawnExistsInWorld(pawnKindDef);
            
            if (exists)
            {
                details = "在世界中存在。";
                
                // 如果要求存活，进行额外检查
                if (requireAlive)
                {
                    var pawn = FindPawnInWorld(pawnKindDef);
                    if (pawn == null || pawn.Destroyed || (requireOnMap && !pawn.Spawned))
                    {
                        details += " 但Pawn不符合存活要求。";
                        return false;
                    }
                    details += " Pawn存活且符合要求。";
                }
            }
            else
            {
                details = "在世界中不存在。";
            }
            
            return exists;
        }
        
        /// <summary>
        /// 检查存档中是否存在
        /// </summary>
        private bool CheckExistsInArchive(out string details)
        {
            // 存档范围通常与世界范围相同，因为管理器只管理当前存档
            details = "在存档中存在（存档范围检查）。";
            return CheckExistsInWorld(out _);
        }
        
        /// <summary>
        /// 在地图中查找Pawn
        /// </summary>
        private Pawn FindPawnInMap(PawnKindDef pawnKindDef, Map map)
        {
            if (map == null || map.mapPawns == null)
                return null;
            
            // 首先检查UniquePawnManager的记录
            var manager = UniquePawnManager.Instance;
            if (manager != null)
            {
                string baseKey = pawnKindDef.defName;
                string key = $"{baseKey}_{map.Index}";
                
                if (manager.pawnRecords.TryGetValue(key, out var record) && 
                    record.pawn != null && record.pawn.Map == map)
                {
                    return record.pawn;
                }
            }
            
            // 然后检查地图中实际的Pawn
            foreach (var pawn in map.mapPawns.AllPawns)
            {
                if (pawn.kindDef == pawnKindDef && 
                    (!requireAlive || !pawn.Destroyed) &&
                    (!requireOnMap || pawn.Spawned))
                {
                    return pawn;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 在世界中查找Pawn
        /// </summary>
        private Pawn FindPawnInWorld(PawnKindDef pawnKindDef)
        {
            // 首先检查UniquePawnManager的记录
            var manager = UniquePawnManager.Instance;
            if (manager != null)
            {
                foreach (var record in manager.pawnRecords.Values)
                {
                    if (record.pawnKindDef == pawnKindDef && 
                        record.pawn != null && 
                        (!requireAlive || !record.pawn.Destroyed) &&
                        (!requireOnMap || record.pawn.Spawned))
                    {
                        return record.pawn;
                    }
                }
            }
            
            // 然后检查所有地图中的Pawn
            if (Find.Maps != null)
            {
                foreach (var map in Find.Maps)
                {
                    var pawn = FindPawnInMap(pawnKindDef, map);
                    if (pawn != null)
                        return pawn;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Condition_UniquePawnExists 调试信息 ===");
            sb.AppendLine($"PawnKindDef: {pawnKindDef?.defName ?? "NULL"}");
            sb.AppendLine($"检查范围: {checkScope}");
            sb.AppendLine($"要求存活: {requireAlive}");
            sb.AppendLine($"要求在地图上: {requireOnMap}");
            
            // 检查当前状态
            string reason;
            bool isMet = IsMet(out reason);
            sb.AppendLine($"条件是否满足: {isMet}");
            sb.AppendLine($"详细原因: {reason}");
            
            // 显示管理器信息
            var manager = UniquePawnManager.Instance;
            if (manager != null)
            {
                sb.AppendLine($"已注册Pawn总数: {manager.pawnRecords.Count}");
                
                int matchingPawns = 0;
                foreach (var record in manager.pawnRecords.Values)
                {
                    if (record.pawnKindDef == pawnKindDef)
                    {
                        matchingPawns++;
                        sb.AppendLine($"  • {record.pawn?.LabelCap ?? "未知"} (ID: {record.pawnID}, 生成时间: {record.spawnTime})");
                    }
                }
                sb.AppendLine($"匹配的Pawn数量: {matchingPawns}");
            }
            else
            {
                sb.AppendLine("UniquePawnManager未初始化");
            }
            
            return sb.ToString();
        }
    }
}
