using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld.Planet;

namespace DivineDiurganate
{
    /// <summary>
    /// Meme 和 Role 触发器 StorytellerComp
    /// 当玩家的殖民地拥有指定 meme 且分配了指定职位时触发事件
    /// </summary>
    public class StorytellerComp_MemeRoleTrigger : StorytellerComp
    {
        private StorytellerCompProperties_MemeRoleTrigger MemeRoleProps =>
            (StorytellerCompProperties_MemeRoleTrigger)props;

        // 静态时间跟踪
        private static int IntervalsPassed => Find.TickManager.TicksGame / 1000;

        // 实例状态跟踪
        private int lastTriggeredTick = -1;
        private bool isTriggered = false;

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            try
            {
                // 检查基础条件（天数）
                if (IntervalsPassed <= MemeRoleProps.fireAfterDaysPassed * 60)
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Not enough days passed ({IntervalsPassed} < {MemeRoleProps.fireAfterDaysPassed * 60})");
                    yield break;
                }

                // 检查是否满足周期
                if (!PassesIntervalCheck())
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Interval check failed");
                    yield break;
                }

                // 检查事件是否可重复
                if (!MemeRoleProps.repeatable && isTriggered)
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Already triggered and not repeatable");
                    yield break;
                }

                // 检查重复触发冷却
                if (MemeRoleProps.repeatable && lastTriggeredTick > 0)
                {
                    int ticksSinceLastTrigger = Find.TickManager.TicksGame - lastTriggeredTick;
                    float daysSinceLastTrigger = ticksSinceLastTrigger / 60000f;
                    
                    if (daysSinceLastTrigger < MemeRoleProps.repeatIntervalDays)
                    {
                        if (MemeRoleProps.debugLogging)
                            Log.Message($"StorytellerComp_MemeRoleTrigger: In repeat cooldown ({daysSinceLastTrigger:F1} < {MemeRoleProps.repeatIntervalDays} days)");
                        yield break;
                    }
                }

                // 检查派系条件
                if (!PassesFactionFilter(target))
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Faction filter check failed");
                    yield break;
                }

                // 检查 Meme 条件
                if (!PassesMemeCheck())
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Meme check failed");
                    yield break;
                }

                // 检查 Role 条件
                if (!PassesRoleCheck())
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Role check failed");
                    yield break;
                }

                // 检查事件是否可以触发
                if (!MemeRoleProps.incident.TargetAllowed(target))
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Incident {MemeRoleProps.incident.defName} not allowed for target");
                    yield break;
                }

                // 生成事件参数
                IncidentParms parms = GenerateParms(MemeRoleProps.incident.category, target);
                if (!MemeRoleProps.incident.Worker.CanFireNow(parms))
                {
                    if (MemeRoleProps.debugLogging)
                        Log.Message($"StorytellerComp_MemeRoleTrigger: Incident {MemeRoleProps.incident.defName} cannot fire now");
                    yield break;
                }

                // 所有条件满足，触发事件
                if (MemeRoleProps.debugLogging)
                {
                    Log.Message($"StorytellerComp_MemeRoleTrigger: All conditions met, triggering incident {MemeRoleProps.incident.defName}");
                    LogStatus();
                }

                // 更新状态
                lastTriggeredTick = Find.TickManager.TicksGame;
                isTriggered = true;

                yield return new FiringIncident(MemeRoleProps.incident, this, parms);
            }
            finally
            {
            }
        }

        /// <summary>
        /// 检查是否满足触发周期
        /// </summary>
        private bool PassesIntervalCheck()
        {
            int currentInterval = IntervalsPassed;
            int checkInterval = (int)(MemeRoleProps.checkIntervalDays * 60);
            
            if (checkInterval <= 0)
                return true;

            return currentInterval % checkInterval == 0;
        }

        /// <summary>
        /// 检查 Meme 条件
        /// </summary>
        private bool PassesMemeCheck()
        {
            if (MemeRoleProps.requiredMeme == null)
            {
                Log.Error("StorytellerComp_MemeRoleTrigger: requiredMeme is null");
                return false;
            }

            // 获取玩家的意识形态
            Faction playerFaction = Faction.OfPlayer;
            if (playerFaction == null || playerFaction.ideos == null)
                return false;

            // 检查是否拥有指定 meme
            bool hasMeme = false;
            
            if (MemeRoleProps.requireAllColonies)
            {
                // 要求所有殖民地都拥有该 meme
                hasMeme = true;
                foreach (var ideo in playerFaction.ideos.AllIdeos)
                {
                    if (!ideo.memes.Contains(MemeRoleProps.requiredMeme))
                    {
                        hasMeme = false;
                        break;
                    }
                }
            }
            else
            {
                // 只要任一意识形态拥有该 meme
                hasMeme = playerFaction.ideos.AllIdeos.Any(ideo => 
                    ideo.memes.Contains(MemeRoleProps.requiredMeme));
            }

            if (MemeRoleProps.debugLogging)
            {
                Log.Message($"StorytellerComp_MemeRoleTrigger: Meme {MemeRoleProps.requiredMeme.defName} check result: {hasMeme}");
            }

            return hasMeme;
        }

        /// <summary>
        /// 检查 Role 条件
        /// </summary>
        private bool PassesRoleCheck()
        {
            if (MemeRoleProps.requiredRolePrecept == null)
            {
                Log.Error("StorytellerComp_MemeRoleTrigger: requiredRolePrecept is null");
                return false;
            }

            // 获取所有殖民者
            List<Pawn> colonists = PawnsFinder.AllMaps_FreeColonists.ToList();
            
            if (colonists.Count == 0)
                return false;

            int colonistsWithRole = 0;

            foreach (Pawn pawn in colonists)
            {
                // 获取 pawn 的意识形态
                Ideo pawnIdeo = pawn.Ideo;
                if (pawnIdeo == null)
                    continue;

                // 在意识形态中查找指定的 Precept_Role
                var rolePrecept = pawnIdeo.PreceptsListForReading
                    .FirstOrDefault(precept => precept.def == MemeRoleProps.requiredRolePrecept) as Precept_Role;

                if (rolePrecept == null)
                    continue;

                // 检查 pawn 是否被分配了这个角色
                if (rolePrecept.ChosenPawnSingle() == pawn)
                {
                    // 检查是否要求永久职位
                    if (MemeRoleProps.requirePermanentRole)
                    {
                        // 这里需要检查是否是临时角色，但 Precept_Role 没有直接标识
                        // 我们可以通过检查 pawn 的角色是否是临时分配来近似判断
                        // 暂时假设都满足条件
                        colonistsWithRole++;
                    }
                    else
                    {
                        colonistsWithRole++;
                    }
                }
            }

            bool result = colonistsWithRole >= MemeRoleProps.minColonistsWithRole;

            if (MemeRoleProps.debugLogging)
            {
                Log.Message($"StorytellerComp_MemeRoleTrigger: Role {MemeRoleProps.requiredRolePrecept.defName} check result: {result} (found {colonistsWithRole} colonists, required {MemeRoleProps.minColonistsWithRole})");
            }

            return result;
        }

        /// <summary>
        /// 检查派系过滤条件
        /// </summary>
        private bool PassesFactionFilter(IIncidentTarget target)
        {
            if (MemeRoleProps.allowedFactions == null || MemeRoleProps.allowedFactions.Count == 0)
                return true;

            Faction faction = GetTargetFaction(target);
            if (faction == null)
                return false;

            return MemeRoleProps.allowedFactions.Contains(faction.def);
        }

        /// <summary>
        /// 获取目标的派系
        /// </summary>
        private Faction GetTargetFaction(IIncidentTarget target)
        {
            if (target is Map map)
            {
                return map.ParentFaction ?? Faction.OfPlayer;
            }
            else if (target is World world)
            {
                return Faction.OfPlayer;
            }
            else if (target is Caravan caravan)
            {
                return caravan.Faction;
            }
            
            return Faction.OfPlayer;
        }

        /// <summary>
        /// 记录当前状态（用于调试）
        /// </summary>
        private void LogStatus()
        {
            if (!MemeRoleProps.debugLogging)
                return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== StorytellerComp_MemeRoleTrigger Status ===");
            sb.AppendLine($"Required Meme: {MemeRoleProps.requiredMeme?.defName ?? "NULL"}");
            sb.AppendLine($"Required Role: {MemeRoleProps.requiredRolePrecept?.defName ?? "NULL"}");
            sb.AppendLine($"Incident: {MemeRoleProps.incident?.defName ?? "NULL"}");
            
            // Meme 检查状态
            sb.AppendLine($"Meme Check: {(PassesMemeCheck() ? "PASS" : "FAIL")}");
            
            // Role 检查状态
            sb.AppendLine($"Role Check: {(PassesRoleCheck() ? "PASS" : "FAIL")}");
            
            // 时间状态
            sb.AppendLine($"Days Passed: {GenDate.DaysPassed}");
            sb.AppendLine($"Fire After Days: {MemeRoleProps.fireAfterDaysPassed}");
            sb.AppendLine($"Repeatable: {MemeRoleProps.repeatable}");
            sb.AppendLine($"Last Triggered: {lastTriggeredTick}");
            sb.AppendLine($"Is Triggered: {isTriggered}");
            
            // 殖民者统计
            List<Pawn> colonists = PawnsFinder.AllMaps_FreeColonists.ToList();
            int colonistsWithRole = 0;
            
            foreach (Pawn pawn in colonists)
            {
                Ideo pawnIdeo = pawn.Ideo;
                if (pawnIdeo == null) continue;
                
                var rolePrecept = pawnIdeo.PreceptsListForReading
                    .FirstOrDefault(precept => precept.def == MemeRoleProps.requiredRolePrecept) as Precept_Role;
                    
                if (rolePrecept != null && rolePrecept.ChosenPawnSingle() == pawn)
                {
                    colonistsWithRole++;
                }
            }
            
            sb.AppendLine($"Colonists with Role: {colonistsWithRole}/{MemeRoleProps.minColonistsWithRole}");
            sb.AppendLine($"Total Colonists: {colonists.Count}");
            sb.AppendLine("=========================================");
            
            Log.Message(sb.ToString());
        }

        /// <summary>
        /// 序列化状态
        /// </summary>
        public void PostExposeData()
        {
            // 注意：StorytellerComp 通常不序列化，但我们可以通过静态变量或 WorldComponent 来保存状态
            // 这里我们只保存实例状态
            Scribe_Values.Look(ref lastTriggeredTick, "lastTriggeredTick", -1);
            Scribe_Values.Look(ref isTriggered, "isTriggered", false);
        }
    }
}
