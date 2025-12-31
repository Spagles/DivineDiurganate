using RimWorld;
using System.Collections.Generic;
using Verse;
using System.Linq;

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
        
        // 检查间隔（游戏刻）
        private const int CheckIntervalTicks = 2500; // 1游戏天
        
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
            if (pawn == null || !pawn.Spawned || pawn.Dead || !pawn.IsColonist)
                return;
                
            foreach (var hediffDef in managedHediffs)
            {
                CheckPawnForHediff(pawn, hediffDef);
            }
        }
        
        private static void CheckPawnForHediff(Pawn pawn, HediffDef hediffDef)
        {
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
        }
        
        private static void RemoveHediffIfExists(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn == null || hediffDef == null)
                return;
                
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
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
        
        // 强制刷新所有殖民者的Hediff状态
        public static void RefreshAllColonists()
        {
            if (Current.Game == null || Current.Game.CurrentMap == null)
                return;
                
            var map = Find.CurrentMap;
            if (map == null)
                return;
                
            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                CheckPawn(pawn);
            }
            
        }
        
        // 当角色变化时调用
        public static void OnPawnRoleChanged(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonist)
                return;
                
            CheckPawn(pawn);
        }
        
        // 当Meme变化时调用
        public static void OnPawnMemeChanged(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonist)
                return;
                
            CheckPawn(pawn);
        }
    }
}
