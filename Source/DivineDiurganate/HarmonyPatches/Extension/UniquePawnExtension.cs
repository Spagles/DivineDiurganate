using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 特殊Pawn扩展定义
    /// 用于标记一个PawnKindDef为特殊单例Pawn
    /// </summary>
    public class UniquePawnExtension : DefModExtension
    {
        // 是否启用单例模式（每个PawnKindDef只能生成一个）
        public bool isSingleton = true;
        
        // 是否销毁重复生成的Pawn（如果为false，则阻止生成）
        public bool destroyDuplicates = true;
        
        // 单例检查范围：地图、世界、存档（所有存档）
        public UniquePawnScope singletonScope = UniquePawnScope.Map;
        
        // 固定关系定义
        public List<FixedRelation> fixedRelations = new List<FixedRelation>();
        
        // 在生成时自动建立的关系
        public List<AutoRelation> autoRelations = new List<AutoRelation>();
        
        // 需要删除的非固定关系类型（如果为空，删除所有非指定的关系）
        public List<PawnRelationDef> relationsToKeep = new List<PawnRelationDef>();
        
        // 是否在生成时删除所有现有关系（除了固定的）
        public bool clearExistingRelations = true;
        
        // 是否在死亡时从管理器移除
        public bool removeOnDeath = true;
        
        // 是否在生成时发送消息
        public bool showSpawnMessage = true;
        public string spawnMessageKey = "DD_UniquePawnSpawned";
        
        // 日志级别
        public LogLevel logLevel = LogLevel.Info;
        
        /// <summary>
        /// 获取关系定义的唯一键
        /// </summary>
        public string GetRelationKey(PawnRelationDef relationDef)
        {
            return relationDef?.defName ?? "Unknown";
        }
    }
    
    /// <summary>
    /// 固定关系定义
    /// </summary>
    public class FixedRelation
    {
        // 关系目标PawnKindDef
        public PawnKindDef targetPawnKind;
        
        // 关系类型
        public PawnRelationDef relationDef;
        
        // 关系方向（源->目标，目标->源，双向）
        public RelationDirection direction = RelationDirection.Bidirectional;
        
        // 关系强度（0-1）
        public float relationStrength = 0.5f;
        
        // 是否必须存在（如果不存在，则记录为待建立关系）
        public bool required = true;
        
        // 当目标Pawn生成时自动建立关系
        public bool autoEstablish = true;
    }
    
    /// <summary>
    /// 自动关系定义
    /// </summary>
    public class AutoRelation
    {
        // 目标PawnKindDef（如果为空，则针对所有Pawn）
        public PawnKindDef targetPawnKind;
        
        // 关系类型
        public PawnRelationDef relationDef;
        
        // 筛选条件
        public RelationFilter filter = new RelationFilter();
        
        // 关系方向
        public RelationDirection direction = RelationDirection.Bidirectional;
        
        // 关系强度
        public float relationStrength = 0.5f;
        
        // 最大关系数量（-1表示无限制）
        public int maxRelations = -1;
    }
    
    /// <summary>
    /// 关系筛选器
    /// </summary>
    public class RelationFilter
    {
        // 性别限制
        public Gender gender = Gender.None;
        
        // 最小年龄
        public float minAge = 0;
        
        // 最大年龄
        public float maxAge = 999;
        
        // 需要特定特性
        public List<TraitDef> requiredTraits = new List<TraitDef>();
        
        // 需要特定背景故事
        public List<BackstoryDef> requiredBackstories = new List<BackstoryDef>();
        
        /// <summary>
        /// 检查Pawn是否符合条件
        /// </summary>
        public bool Matches(Pawn pawn)
        {
            if (pawn == null)
                return false;
            
            // 检查性别
            if (gender != Gender.None && pawn.gender != gender)
                return false;
            
            // 检查年龄
            float age = pawn.ageTracker.AgeBiologicalYearsFloat;
            if (age < minAge || age > maxAge)
                return false;
            
            // 检查特性
            if (requiredTraits != null && requiredTraits.Count > 0)
            {
                if (pawn.story == null || pawn.story.traits == null)
                    return false;
                
                foreach (var traitDef in requiredTraits)
                {
                    if (!pawn.story.traits.HasTrait(traitDef))
                        return false;
                }
            }
            
            // 检查背景故事
            if (requiredBackstories != null && requiredBackstories.Count > 0)
            {
                if (pawn.story == null)
                    return false;
                
                bool hasRequiredBackstory = false;
                foreach (var backstoryDef in requiredBackstories)
                {
                    if (pawn.story.Childhood == backstoryDef || pawn.story.Adulthood == backstoryDef)
                    {
                        hasRequiredBackstory = true;
                        break;
                    }
                }
                
                if (!hasRequiredBackstory)
                    return false;
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// 关系方向
    /// </summary>
    public enum RelationDirection
    {
        SourceToTarget,  // 源->目标
        TargetToSource,  // 目标->源
        Bidirectional    // 双向
    }
    
    /// <summary>
    /// 单例范围
    /// </summary>
    public enum UniquePawnScope
    {
        Map,     // 当前地图
        World,   // 整个世界（所有地图）
        Archive  // 所有存档（跨存档）
    }
    
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        None,
        Error,
        Warning,
        Info,
        Debug
    }
}
