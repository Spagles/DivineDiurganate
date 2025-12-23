// File: CompSkillLevelDebug.cs
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 测试组件：在开发模式下显示pawn的技能等级
    /// </summary>
    public class CompSkillLevelDebug : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }
        
        /// <summary>
        /// 获取调试按钮
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 只在开发模式下显示
            if (DebugSettings.ShowDevGizmos)
            {
                var pawn = parent as Pawn;
                if (pawn != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEBUG: 显示技能等级",
                        defaultDesc = "点击输出当前Pawn的所有技能等级",
                        icon = TexCommand.DesirePower,
                        action = () =>
                        {
                            ShowSkills(pawn);
                        }
                    };
                    
                    yield return new Command_Action
                    {
                        defaultLabel = "DEBUG: 技能概述",
                        defaultDesc = "显示技能概述（适合消息框）",
                        icon = TexCommand.Install,
                        action = () =>
                        {
                            ShowSkillsSummary(pawn);
                        }
                    };
                }
            }
        }
        
        /// <summary>
        /// 显示技能详情（输出到日志）
        /// </summary>
        private void ShowSkills(Pawn pawn)
        {
            if (pawn == null || pawn.skills == null)
            {
                Log.Message($"[DD] 没有找到Pawn或技能系统");
                return;
            }
            
            Log.Message("=== 技能等级详细信息 ===");
            Log.Message($"Pawn: {pawn.LabelCap} ({pawn.def.defName})");
            Log.Message($"等级系统: {pawn.skills.GetType().Name}");
            
            // 输出所有技能
            foreach (var skill in pawn.skills.skills)
            {
                if (skill != null)
                {
                    string passionStr = GetPassionString(skill.passion);
                    string disabledStr = skill.TotallyDisabled ? " (已禁用)" : "";
                    
                    Log.Message($"  {skill.def.LabelCap}: {skill.Level}级{disabledStr} {passionStr}");
                }
            }
            
            Log.Message("=== 结束 ===");
            
            // 同时在屏幕上显示简要信息
            Messages.Message("技能信息已输出到日志（按Ctrl+F12查看）", MessageTypeDefOf.SilentInput);
        }
        
        /// <summary>
        /// 显示技能概述（在消息框中）
        /// </summary>
        private void ShowSkillsSummary(Pawn pawn)
        {
            if (pawn == null || pawn.skills == null)
            {
                Messages.Message("错误：Pawn没有技能系统", MessageTypeDefOf.RejectInput);
                return;
            }
            
            string summary = $"<b>{pawn.LabelCap}的技能等级</b>\n\n";
            
            foreach (var skill in pawn.skills.skills)
            {
                if (skill != null && !skill.TotallyDisabled)
                {
                    string passionStr = GetPassionString(skill.passion);
                    summary += $"{skill.def.LabelCap}: <color=cyan>{skill.Level}级</color> {passionStr}\n";
                }
            }
            
            // 添加一些统计信息
            int totalSkills = pawn.skills.skills.Count;
            int activeSkills = pawn.skills.skills.FindAll(s => !s.TotallyDisabled).Count;
            
            summary += $"\n<b>统计</b>\n";
            summary += $"总技能数: {totalSkills}\n";
            summary += $"可用技能数: {activeSkills}\n";
            summary += $"总等级: {GetTotalSkillLevels(pawn)}\n";
            summary += $"平均等级: {GetAverageSkillLevel(pawn):F1}";
            
            // 显示在屏幕上
            Find.WindowStack.Add(new Dialog_MessageBox(summary, "关闭", null, null, null, "技能等级", false));
        }
        
        /// <summary>
        /// 获取热情描述
        /// </summary>
        private string GetPassionString(Passion passion)
        {
            switch (passion)
            {
                case Passion.None:
                    return "";
                case Passion.Minor:
                    return "<color=yellow>+</color>";
                case Passion.Major:
                    return "<color=orange>++</color>";
                default:
                    return "";
            }
        }
        
        /// <summary>
        /// 计算总技能等级
        /// </summary>
        private int GetTotalSkillLevels(Pawn pawn)
        {
            if (pawn.skills == null) return 0;
            
            int total = 0;
            foreach (var skill in pawn.skills.skills)
            {
                if (!skill.TotallyDisabled)
                {
                    total += skill.Level;
                }
            }
            return total;
        }
        
        /// <summary>
        /// 计算平均技能等级
        /// </summary>
        private float GetAverageSkillLevel(Pawn pawn)
        {
            int total = 0;
            int count = 0;
            
            foreach (var skill in pawn.skills.skills)
            {
                if (!skill.TotallyDisabled)
                {
                    total += skill.Level;
                    count++;
                }
            }
            
            return count > 0 ? (float)total / count : 0f;
        }
    }
    
    /// <summary>
    /// 组件属性
    /// </summary>
    public class CompProperties_SkillLevelDebug : CompProperties
    {
        public CompProperties_SkillLevelDebug()
        {
            compClass = typeof(CompSkillLevelDebug);
        }
    }
}
