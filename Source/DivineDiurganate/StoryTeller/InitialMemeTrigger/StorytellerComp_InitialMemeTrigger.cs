using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 初始文化Meme触发器
    /// 检测玩家初始文化是否包含特定meme，并根据结果触发不同事件
    /// </summary>
    public class StorytellerComp_InitialMemeTrigger : StorytellerComp
    {
        private StorytellerCompProperties_InitialMemeTrigger InitialMemeProps =>
            (StorytellerCompProperties_InitialMemeTrigger)props;
            
        // 静态时间跟踪
        private static int IntervalsPassed => Find.TickManager.TicksGame / 1000;
        
        // 实例状态跟踪
        private bool hasMemeResult = false;
        private bool hasMemeChecked = false;
        private bool hasTriggered = false;
        private int lastTriggeredTick = -1;
        
        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            // 检查基础条件（天数）
            if (IntervalsPassed <= InitialMemeProps.fireAfterDaysPassed * 60)
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: Not enough days passed ({IntervalsPassed} < {InitialMemeProps.fireAfterDaysPassed * 60})");
                yield break;
            }
                
            // 检查是否满足周期
            if (!PassesIntervalCheck())
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: Interval check failed");
                yield break;
            }
                
            // 检查是否已经触发过（如果配置为整个游戏只触发一次）
            if (InitialMemeProps.onlyOncePerGame && hasTriggered)
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: Already triggered and onlyOncePerGame is true");
                yield break;
            }
                
            // 检查重复触发冷却
            if (InitialMemeProps.repeatable && lastTriggeredTick > 0)
            {
                int ticksSinceLastTrigger = Find.TickManager.TicksGame - lastTriggeredTick;
                float daysSinceLastTrigger = ticksSinceLastTrigger / 60000f;
                    
                if (daysSinceLastTrigger < InitialMemeProps.repeatIntervalDays)
                {
                    if (InitialMemeProps.debugLogging)
                        Log.Message($"StorytellerComp_InitialMemeTrigger: In repeat cooldown ({daysSinceLastTrigger:F1} < {InitialMemeProps.repeatIntervalDays} days)");
                    yield break;
                }
            }
                
            // 检查Meme条件
            bool hasMeme = CheckMemeCondition();
                
            // 根据结果选择要触发的事件
            IncidentDef incidentToTrigger = null;
                
            if (hasMeme && InitialMemeProps.incidentIfHasMeme != null)
            {
                incidentToTrigger = InitialMemeProps.incidentIfHasMeme;
            }
            else if (!hasMeme && InitialMemeProps.incidentIfNoMeme != null)
            {
                incidentToTrigger = InitialMemeProps.incidentIfNoMeme;
            }
                
            if (incidentToTrigger == null)
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: No incident to trigger (hasMeme={hasMeme}, incidentIfHasMeme={InitialMemeProps.incidentIfHasMeme}, incidentIfNoMeme={InitialMemeProps.incidentIfNoMeme})");
                yield break;
            }
                
            // 检查事件是否可以触发
            if (!incidentToTrigger.TargetAllowed(target))
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: Incident {incidentToTrigger.defName} not allowed for target");
                yield break;
            }
                
            // 生成事件参数
            IncidentParms parms = GenerateParms(incidentToTrigger.category, target);
            if (!incidentToTrigger.Worker.CanFireNow(parms))
            {
                if (InitialMemeProps.debugLogging)
                    Log.Message($"StorytellerComp_InitialMemeTrigger: Incident {incidentToTrigger.defName} cannot fire now");
                yield break;
            }
                
            // 所有条件满足，触发事件
            if (InitialMemeProps.debugLogging)
            {
                Log.Message($"StorytellerComp_InitialMemeTrigger: All conditions met, triggering incident {incidentToTrigger.defName}");
                LogMessage($"Meme check result: {(hasMeme ? "HAS meme" : "NO meme")} - {InitialMemeProps.memeToCheck.defName}");
            }
                
            // 更新状态
            lastTriggeredTick = Find.TickManager.TicksGame;
            hasTriggered = true;

            yield return new FiringIncident(incidentToTrigger, this, GenerateParms(incidentToTrigger.category, target));
        }
        
        /// <summary>
        /// 检查是否满足触发周期
        /// </summary>
        private bool PassesIntervalCheck()
        {
            int currentInterval = IntervalsPassed;
            int checkInterval = (int)(InitialMemeProps.checkIntervalDays * 60);
            
            if (checkInterval <= 0)
                return true;
            
            return currentInterval % checkInterval == 0;
        }
        
        /// <summary>
        /// 检查Meme条件
        /// </summary>
        private bool CheckMemeCondition()
        {
            // 如果已经检查过，返回缓存结果
            if (hasMemeChecked)
            {
                return hasMemeResult;
            }
            
            bool result = false;
            
            if (InitialMemeProps.checkPlayerFaction)
            {
                // 检查玩家派系
                Faction playerFaction = Faction.OfPlayer;
                if (playerFaction == null || playerFaction.ideos == null)
                {
                    if (InitialMemeProps.debugLogging)
                        Log.Message("StorytellerComp_InitialMemeTrigger: Player faction or ideos is null");
                }
                else
                {
                    if (InitialMemeProps.checkPrimaryIdeoOnly)
                    {
                        // 只检查主要文化
                        Ideo primaryIdeo = playerFaction.ideos.PrimaryIdeo;
                        if (primaryIdeo != null)
                        {
                            result = primaryIdeo.memes.Contains(InitialMemeProps.memeToCheck);
                        }
                    }
                    else
                    {
                        // 检查所有文化
                        result = playerFaction.ideos.AllIdeos.Any(ideo => 
                            ideo != null && ideo.memes.Contains(InitialMemeProps.memeToCheck));
                    }
                }
            }
            else
            {
                // 检查所有殖民者的个人文化
                var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
                if (colonists != null)
                {
                    result = colonists.Any(pawn => 
                        pawn != null && pawn.Ideo != null && 
                        pawn.Ideo.memes.Contains(InitialMemeProps.memeToCheck));
                }
            }
            
            // 缓存结果
            hasMemeResult = result;
            hasMemeChecked = true;
            
            if (InitialMemeProps.debugLogging)
            {
                Log.Message($"StorytellerComp_InitialMemeTrigger: Meme check result = {result} for {InitialMemeProps.memeToCheck.defName}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 记录日志消息
        /// </summary>
        private void LogMessage(string message)
        {
            if (InitialMemeProps.debugLogging)
            {
                Log.Message($"[StorytellerComp_InitialMemeTrigger] {message}");
            }
        }
        
        /// <summary>
        /// 获取当前状态（用于调试）
        /// </summary>
        public string GetStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Initial Meme Trigger Status ===");
            sb.AppendLine($"Meme to check: {InitialMemeProps.memeToCheck?.defName ?? "NULL"}");
            sb.AppendLine($"Has meme result: {hasMemeResult} (checked: {hasMemeChecked})");
            sb.AppendLine($"Has triggered: {hasTriggered}");
            sb.AppendLine($"Last triggered tick: {lastTriggeredTick}");
            sb.AppendLine($"Incident if has meme: {InitialMemeProps.incidentIfHasMeme?.defName ?? "NONE"}");
            sb.AppendLine($"Incident if no meme: {InitialMemeProps.incidentIfNoMeme?.defName ?? "NONE"}");
            sb.AppendLine($"Only once per game: {InitialMemeProps.onlyOncePerGame}");
            sb.AppendLine($"Repeatable: {InitialMemeProps.repeatable}");
            sb.AppendLine($"Days passed: {GenDate.DaysPassed}");
            sb.AppendLine($"Fire after days: {InitialMemeProps.fireAfterDaysPassed}");
            sb.AppendLine($"Check interval days: {InitialMemeProps.checkIntervalDays}");
            
            // 当前检查结果
            bool currentHasMeme = CheckMemeCondition();
            sb.AppendLine($"Current meme check: {currentHasMeme}");
            
            // 下一个检查间隔
            int currentInterval = IntervalsPassed;
            int checkInterval = (int)(InitialMemeProps.checkIntervalDays * 60);
            int nextCheck = ((currentInterval / checkInterval) + 1) * checkInterval;
            sb.AppendLine($"Next check interval: {nextCheck}");
            
            return sb.ToString();
        }
        
        public void ExposeData()
        {
            // 保存状态
            Scribe_Values.Look(ref hasMemeResult, "hasMemeResult", false);
            Scribe_Values.Look(ref hasMemeChecked, "hasMemeChecked", false);
            Scribe_Values.Look(ref hasTriggered, "hasTriggered", false);
            Scribe_Values.Look(ref lastTriggeredTick, "lastTriggeredTick", -1);
        }
    }
}
