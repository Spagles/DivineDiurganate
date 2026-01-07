using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// StorytellerComp：当特定 meme 存在且职位被分配时，授予科技
    /// </summary>
    public class StorytellerComp_GrantTechOnRoleAssignment : StorytellerComp
    {
        private StorytellerCompProperties_GrantTechOnRoleAssignment Props =>
            (StorytellerCompProperties_GrantTechOnRoleAssignment)props;

        // 记录已经授予的科技，避免重复授予
        private HashSet<ResearchProjectDef> grantedTechs = new HashSet<ResearchProjectDef>();

        // 检查间隔（ticks）
        private int checkIntervalTicks = 6000; // 10游戏分钟

        // 上次检查时间
        private int lastCheckTick = -1;

        public override void PostExposeData()
        {
            base.PostExposeData();
            
            // 保存已经授予的科技
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<ResearchProjectDef> grantedTechList = grantedTechs.ToList();
                Scribe_Collections.Look(ref grantedTechList, "grantedTechs", LookMode.Def);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<ResearchProjectDef> grantedTechList = new List<ResearchProjectDef>();
                Scribe_Collections.Look(ref grantedTechList, "grantedTechs", LookMode.Def);
                grantedTechs = new HashSet<ResearchProjectDef>(grantedTechList ?? new List<ResearchProjectDef>());
            }
            
            Scribe_Values.Look(ref lastCheckTick, "lastCheckTick", -1);
        }

        public override void CompTick()
        {
            base.CompTick();

            int currentTick = Find.TickManager.TicksGame;

            // 定期检查
            if (lastCheckTick < 0 || currentTick - lastCheckTick >= checkIntervalTicks)
            {
                CheckAndGrantTechs();
                lastCheckTick = currentTick;
            }
        }

        /// <summary>
        /// 检查并授予符合条件的科技
        /// </summary>
        private void CheckAndGrantTechs()
        {
            try
            {
                // 遍历所有玩家地图
                foreach (Map map in Find.Maps)
                {
                    if (!map.IsPlayerHome) continue;

                    // 检查所有符合条件的科技
                    foreach (var techRule in Props.techRules)
                    {
                        CheckAndGrantTech(map, techRule);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in CheckAndGrantTechs: {ex}");
            }
        }

        /// <summary>
        /// 检查并授予单个科技
        /// </summary>
        private void CheckAndGrantTech(Map map, TechGrantRule rule)
        {
            try
            {
                // 跳过已授予的科技
                if (grantedTechs.Contains(rule.researchDef))
                    return;

                // 检查前置科技（如果有）
                if (rule.requiresPreviousTech != null && !IsResearchCompleted(rule.requiresPreviousTech))
                    return;

                // 检查meme条件
                if (!CheckMemeCondition(map, rule))
                    return;

                // 检查职位条件
                if (!CheckRoleCondition(map, rule))
                    return;

                // 条件满足，授予科技
                GrantTechnology(rule.researchDef, rule.letterMessage);
            }
            catch (Exception ex)
            {
                Log.Error($"Error checking tech rule {rule.researchDef?.defName}: {ex}");
            }
        }

        /// <summary>
        /// 检查meme条件
        /// </summary>
        private bool CheckMemeCondition(Map map, TechGrantRule rule)
        {
            if (rule.requiredMeme == null)
                return true;

            // 获取殖民地的意识形态
            var ideo = map?.GetComponent<IdeoMapComponent>()?.Ideo;
            if (ideo == null)
                return false;

            // 检查是否包含指定的meme
            return ideo.memes.Contains(rule.requiredMeme);
        }

        /// <summary>
        /// 检查职位条件
        /// </summary>
        private bool CheckRoleCondition(Map map, TechGrantRule rule)
        {
            if (rule.requiredRole == null)
                return false;

            // 获取殖民地的意识形态
            var ideo = map?.GetComponent<IdeoMapComponent>()?.Ideo;
            if (ideo == null)
                return false;

            // 查找指定的职位
            foreach (var precept in ideo.PreceptsListForReading)
            {
                if (precept.def == rule.requiredRole && precept is Precept_Role role)
                {
                    // 检查是否有殖民者担任此职位
                    if (role.Active && role.ChosenPawnSingle() != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查研究是否已完成
        /// </summary>
        private bool IsResearchCompleted(ResearchProjectDef researchDef)
        {
            return researchDef?.IsFinished ?? false;
        }

        /// <summary>
        /// 授予科技
        /// </summary>
        private void GrantTechnology(ResearchProjectDef researchDef, string letterMessage)
        {
            try
            {
                if (researchDef == null)
                    return;

                // 如果科技已完成，直接记录
                if (researchDef.IsFinished)
                {
                    grantedTechs.Add(researchDef);
                    return;
                }

                // 完成科技
                researchDef.ResearchPrerequisites?.ForEach(prereq => GrantTechnology(prereq, null));

                Find.ResearchManager.FinishProject(researchDef, doCompletionDialog: false);

                // 记录已授予
                grantedTechs.Add(researchDef);

                // 发送通知
                if (!string.IsNullOrEmpty(letterMessage))
                {
                    SendTechGrantedLetter(researchDef, letterMessage);
                }

                Log.Message($"StorytellerComp: 已授予科技 {researchDef.label}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error granting technology {researchDef?.defName}: {ex}");
            }
        }

        /// <summary>
        /// 发送科技授予通知
        /// </summary>
        private void SendTechGrantedLetter(ResearchProjectDef researchDef, string message)
        {
            try
            {
                string label = "DD_TechGranted_LetterLabel".Translate(researchDef.label);
                string text = message.Formatted(researchDef.label);

                LetterDef letterDef = LetterDefOf.PositiveEvent;

                var letter = LetterMaker.MakeLetter(label, text, letterDef);
                Find.LetterStack.ReceiveLetter(letter);
            }
            catch (Exception ex)
            {
                Log.Error($"Error sending tech granted letter: {ex}");
            }
        }

        /// <summary>
        /// 重置已授予的科技（用于调试）
        /// </summary>
        [DebugAction("DivineDiurganate", "Reset Granted Techs", false)]
        public static void DebugResetGrantedTechs()
        {
            var comp = Find.Storyteller?.storytellerComps?.FirstOrDefault(c => c is StorytellerComp_GrantTechOnRoleAssignment) 
                as StorytellerComp_GrantTechOnRoleAssignment;
            
            if (comp != null)
            {
                comp.grantedTechs.Clear();
                Messages.Message("已重置已授予的科技", MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    /// <summary>
    /// 科技授予规则
    /// </summary>
    public class TechGrantRule
    {
        // 需要的研究项目
        public ResearchProjectDef researchDef;
        
        // 需要的前置科技（可选）
        public ResearchProjectDef requiresPreviousTech;
        
        // 需要的meme（可选）
        public MemeDef requiredMeme;
        
        // 需要的职位（PreceptDef类型，必须是Precept_Role）
        public PreceptDef requiredRole;
        
        // 授予时的信件消息（可选）
        public string letterMessage;
    }

    /// <summary>
    /// StorytellerComp属性
    /// </summary>
    public class StorytellerCompProperties_GrantTechOnRoleAssignment : StorytellerCompProperties
    {
        // 科技授予规则列表
        public List<TechGrantRule> techRules = new List<TechGrantRule>();

        // 检查间隔（秒）
        public float checkIntervalSeconds = 600f; // 10分钟

        public StorytellerCompProperties_GrantTechOnRoleAssignment()
        {
            compClass = typeof(StorytellerComp_GrantTechOnRoleAssignment);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;

            if (techRules == null || techRules.Count == 0)
                yield return "techRules is empty";

            for (int i = 0; i < techRules.Count; i++)
            {
                var rule = techRules[i];
                if (rule.researchDef == null)
                    yield return $"techRules[{i}].researchDef is null";
                
                if (rule.requiredRole == null)
                    yield return $"techRules[{i}].requiredRole is null";
                
                // 验证requiredRole是否是Precept_Role类型
                if (rule.requiredRole != null && !typeof(Precept_Role).IsAssignableFrom(rule.requiredRole.thingClass))
                {
                    yield return $"techRules[{i}].requiredRole ({rule.requiredRole.defName}) is not a Precept_Role";
                }
            }
        }
    }
}
