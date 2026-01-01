// File: IdeoRoleHediffManager_Fixed.cs
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    [StaticConstructorOnStartup]
    public static class IdeoRoleHediffManager
    {
        // 缓存所有需要管理的HediffDef
        private static readonly List<HediffDef> managedHediffs = new List<HediffDef>();
        
        // 角色到Hediff的映射
        private static readonly Dictionary<PreceptDef, List<HediffDef>> roleToHediffs = new Dictionary<PreceptDef, List<HediffDef>>();
        
        // Hediff到角色的映射
        private static readonly Dictionary<HediffDef, PreceptDef> hediffToRole = new Dictionary<HediffDef, PreceptDef>();
        
        // Hediff到Meme的映射
        private static readonly Dictionary<HediffDef, List<MemeDef>> hediffToMemes = new Dictionary<HediffDef, List<MemeDef>>();
        
        static IdeoRoleHediffManager()
        {
            Initialize();
        }
        
        // 初始化，扫描所有HediffDef
        private static void Initialize()
        {
            managedHediffs.Clear();
            roleToHediffs.Clear();
            hediffToRole.Clear();
            hediffToMemes.Clear();
            
            foreach (var hediffDef in DefDatabase<HediffDef>.AllDefs)
            {
                var props = hediffDef.comps?.OfType<HediffCompProperties_IdeoRoleHediff>().FirstOrDefault();
                if (props != null && props.requiredRole != null)
                {
                    managedHediffs.Add(hediffDef);
                    hediffToRole[hediffDef] = props.requiredRole;
                    
                    // 记录Meme要求
                    if (props.requiredMeme != null && props.requiredMeme.Count > 0)
                    {
                        hediffToMemes[hediffDef] = new List<MemeDef>(props.requiredMeme);
                    }
                    
                    if (!roleToHediffs.TryGetValue(props.requiredRole, out var hediffs))
                    {
                        hediffs = new List<HediffDef>();
                        roleToHediffs[props.requiredRole] = hediffs;
                    }
                    hediffs.Add(hediffDef);
                }
            }
        }
        
        // 检查单个Pawn是否满足所有要求
        private static bool PawnMeetsAllRequirements(Pawn pawn, HediffDef hediffDef)
        {
            // 检查是否为排除的Pawn类型（DDmechunit）
            if (IsExcludedPawnType(pawn))
                return false; // 对于DDmechunit，始终返回false，让管理器不干预
            
            var props = hediffDef.comps?.OfType<HediffCompProperties_IdeoRoleHediff>().FirstOrDefault();
            if (props == null)
                return false;
                
            // 1. 检查角色要求
            if (props.requireRole)
            {
                if (!PawnMeetsRoleRequirement(pawn, props))
                    return false;
            }
            
            // 2. 检查Meme要求
            if (props.requireMeme)
            {
                if (!PawnMeetsMemeRequirement(pawn, props))
                    return false;
            }
            
            return true;
        }
        
        // 检查Pawn是否为需要排除的类型（DDmechunit）
        private static bool IsExcludedPawnType(Pawn pawn)
        {
            return pawn is DDmechunit;
        }
        
        private static bool PawnMeetsRoleRequirement(Pawn pawn, HediffCompProperties_IdeoRoleHediff props)
        {
            if (pawn?.Ideo == null || props.requiredRole == null)
                return false;
                
            var pawnRole = pawn.Ideo.GetRole(pawn);
            return pawnRole != null && pawnRole.def == props.requiredRole;
        }
        
        private static bool PawnMeetsMemeRequirement(Pawn pawn, HediffCompProperties_IdeoRoleHediff props)
        {
            if (pawn?.Ideo == null || props.requiredMeme == null || props.requiredMeme.Count == 0)
                return false;
                
            if (props.requireAllMemes)
            {
                // 必须包含所有指定的Meme
                foreach (var meme in props.requiredMeme)
                {
                    if (!pawn.Ideo.HasMeme(meme))
                        return false;
                }
                return true;
            }
            else
            {
                // 只需要包含任意一个指定的Meme
                foreach (var meme in props.requiredMeme)
                {
                    if (pawn.Ideo.HasMeme(meme))
                        return true;
                }
                return false;
            }
        }
        
        // 检查单个Pawn的Hediff状态
        public static void CheckPawn(Pawn pawn)
        {
            // 如果pawn是DDmechunit，跳过检查
            if (IsExcludedPawnType(pawn))
            {
                if (Prefs.DevMode)
                {
                    Log.Message($"[DD] 跳过DDmechunit的IdeoRoleHediff检查: {pawn.LabelShort}");
                }
                return;
            }
            
            if (pawn == null || !pawn.Spawned || pawn.Dead || !pawn.IsColonist)
                return;
                
            foreach (var hediffDef in managedHediffs)
            {
                CheckPawnForHediff(pawn, hediffDef);
            }
        }
        
        private static void CheckPawnForHediff(Pawn pawn, HediffDef hediffDef)
        {
            // 再次检查是否为排除的Pawn类型
            if (IsExcludedPawnType(pawn))
                return;
            
            // 检查pawn是否满足所有要求
            bool shouldHaveHediff = PawnMeetsAllRequirements(pawn, hediffDef);
            
            if (shouldHaveHediff)
            {
                // 应该有这个hediff
                if (!pawn.health.hediffSet.HasHediff(hediffDef))
                {
                    GiveHediff(pawn, hediffDef);
                }
            }
            else
            {
                // 不应该有这个hediff，移除
                RemoveHediffIfExists(pawn, hediffDef);
            }
        }
        
        private static void GiveHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn == null || hediffDef == null || pawn.Dead)
                return;
                
            var props = hediffDef.comps?.OfType<HediffCompProperties_IdeoRoleHediff>().FirstOrDefault();
            if (props == null)
                return;
                
            // 添加hediff
            var hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            if (props.severityLevel > 0)
            {
                hediff.Severity = props.severityLevel;
            }
            
            pawn.health.AddHediff(hediff);
            
            if (Prefs.DevMode)
            {
                Log.Message($"[DD] 为 {pawn.LabelShort} 添加IdeoRoleHediff: {hediffDef.defName}");
            }
        }
        
        private static void RemoveHediffIfExists(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn == null || hediffDef == null)
                return;
                
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[DD] 从 {pawn.LabelShort} 移除IdeoRoleHediff: {hediffDef.defName}");
                }
            }
        }
        
        // 获取指定角色的所有相关Hediff
        public static List<HediffDef> GetHediffsForRole(PreceptDef roleDef)
        {
            return roleToHediffs.TryGetValue(roleDef, out var hediffs) ? hediffs : new List<HediffDef>();
        }
        
        // 检查Hediff是否与角色相关
        public static bool IsRoleHediff(HediffDef hediffDef)
        {
            return hediffToRole.ContainsKey(hediffDef);
        }
        
        // 获取Hediff对应的角色
        public static PreceptDef GetRoleForHediff(HediffDef hediffDef)
        {
            return hediffToRole.TryGetValue(hediffDef, out var role) ? role : null;
        }
        
        // 获取Hediff对应的Meme
        public static List<MemeDef> GetMemesForHediff(HediffDef hediffDef)
        {
            return hediffToMemes.TryGetValue(hediffDef, out var memes) ? memes : new List<MemeDef>();
        }
        
        // 强制刷新所有殖民者的Hediff状态（排除DDmechunit）
        public static void RefreshAllColonists()
        {
            if (Current.Game == null || Current.Game.CurrentMap == null)
                return;
                
            var map = Find.CurrentMap;
            if (map == null)
                return;
                
            int checkedCount = 0;
            int skippedCount = 0;
                
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                // 跳过DDmechunit
                if (IsExcludedPawnType(pawn))
                {
                    skippedCount++;
                    continue;
                }
                
                CheckPawn(pawn);
                checkedCount++;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[DD] IdeoRoleHediff刷新完成: 检查{checkedCount}个殖民者，跳过{skippedCount}个DDmechunit");
            }
        }
        
        // 当角色变化时调用
        public static void OnPawnRoleChanged(Pawn pawn)
        {
            // 跳过DDmechunit
            if (IsExcludedPawnType(pawn))
                return;
            
            if (pawn == null || !pawn.IsColonist)
                return;
                
            CheckPawn(pawn);
        }
        
        // 当Meme变化时调用
        public static void OnPawnMemeChanged(Pawn pawn)
        {
            // 跳过DDmechunit
            if (IsExcludedPawnType(pawn))
                return;
            
            if (pawn == null || !pawn.IsColonist)
                return;
                
            CheckPawn(pawn);
        }
        
        // 检查某个Pawn是否被排除
        public static bool IsPawnExcluded(Pawn pawn)
        {
            return IsExcludedPawnType(pawn);
        }
    }
}
