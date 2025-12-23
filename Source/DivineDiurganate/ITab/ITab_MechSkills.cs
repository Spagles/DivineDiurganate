// File: ITab_MechSkills_GridLayout.cs
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 专业版：使用网格系统布局
    /// </summary>
    public class ITab_MechSkills_GridLayout : ITab
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string nameBuffer = "";
        private bool isRenaming = false;
        
        // 网格系统参数
        private const float GridSize = 8f;
        private const float HeaderRows = 4;     // 头部占4行
        private const float PilotRows = 3;      // 驾驶员信息占3行
        private const float TitleRows = 1;      // 技能标题占1行
        private const float SkillRows = 20;     // 技能区域占20行
        
        public ITab_MechSkills_GridLayout()
        {
            this.size = new Vector2(520f, 600f);
            this.labelKey = "Mech Skills";
        }
        
        protected override void FillTab()
        {
            var pawn = SelPawn;
            if (pawn == null)
                return;
                
            if (pawn.TryGetComp<CompMechSkillInheritance>() == null)
            {
                DrawError("No skill inheritance system");
                return;
            }
            
            DrawGridLayout(pawn);
        }
        
        private void DrawGridLayout(Pawn pawn)
        {
            // 创建网格
            float rowHeight = size.y / (HeaderRows + PilotRows + TitleRows + SkillRows);
            
            // 1. 头部区域
            Rect headerRect = new Rect(0, 0, size.x, rowHeight * HeaderRows);
            DrawGridHeader(headerRect, pawn, rowHeight);
            
            // 2. 驾驶员区域
            float curY = headerRect.yMax;
            var pilotComp = pawn.TryGetComp<CompMechPilotHolder>();
            
            if (pilotComp != null)
            {
                Rect pilotRect = new Rect(0, curY, size.x, rowHeight * PilotRows);
                DrawGridPilot(pilotRect, pilotComp, rowHeight);
                curY = pilotRect.yMax;
            }
            else
            {
                curY += rowHeight; // 空一行
            }
            
            // 3. 技能区域
            Rect skillsRect = new Rect(0, curY, size.x, size.y - curY);
            DrawGridSkills(skillsRect, pawn, rowHeight);
        }
        
        private void DrawGridHeader(Rect rect, Pawn pawn, float rowHeight)
        {
            Widgets.DrawMenuSection(rect);
            
            // 使用网格定位
            float padding = rowHeight * 0.5f;
            
            // 名称行（第1-2行）
            Rect nameRect = new Rect(
                padding, 
                padding, 
                rect.width - padding * 2, 
                rowHeight * 1.5f
            );
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            
            if (isRenaming)
            {
                nameBuffer = nameBuffer ?? pawn.Name?.ToStringShort ?? pawn.LabelShort;
                nameBuffer = Widgets.TextField(nameRect, nameBuffer);
                
                // 按钮（右对齐）
                Rect buttonRect = new Rect(
                    nameRect.xMax - 170f, 
                    nameRect.y, 
                    170f, 
                    nameRect.height
                );
                DrawRenameButtons(buttonRect);
            }
            else
            {
                Widgets.Label(nameRect, pawn.Name?.ToStringShort ?? pawn.LabelShort);
                
                // 重命名按钮（右对齐）
                if (pawn.Faction?.IsPlayer == true)
                {
                    Rect renameRect = new Rect(
                        nameRect.xMax - 100f, 
                        nameRect.y, 
                        100f, 
                        nameRect.height
                    );
                    if (Widgets.ButtonText(renameRect, "Rename"))
                    {
                        isRenaming = true;
                        nameBuffer = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                    }
                }
            }
            
            // 状态行（第3-4行）
            Rect statusRect = new Rect(
                padding, 
                nameRect.yMax + padding * 0.5f, 
                rect.width - padding * 2, 
                rowHeight * 1.5f
            );
            
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            
            string status = GetStatus(pawn);
            string type = "Mech Unit";
            Widgets.Label(statusRect, $"{type} | {status}");
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private void DrawGridPilot(Rect rect, CompMechPilotHolder pilotComp, float rowHeight)
        {
            float padding = rowHeight * 0.5f;
            
            Widgets.DrawBox(rect);
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.3f));
            
            // 标题
            Rect titleRect = new Rect(
                padding, 
                rect.y + padding, 
                rect.width - padding * 2, 
                rowHeight
            );
            
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "Pilot Info");
            Text.Font = GameFont.Small;
            
            // 内容
            Rect contentRect = new Rect(
                padding, 
                titleRect.yMax + padding * 0.5f, 
                rect.width - padding * 2, 
                rowHeight * 1.5f
            );
            
            if (pilotComp.HasPilots)
            {
                var pilots = pilotComp.GetPilots();
                List<string> pilotNames = new List<string>();
                foreach (var pilot in pilots)
                {
                    if (pilot != null) pilotNames.Add(pilot.LabelShort);
                }
                Widgets.Label(contentRect, $"Pilots: {string.Join(", ", pilotNames)}");
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(contentRect, "No pilot assigned");
                GUI.color = Color.white;
            }
        }
        
        private void DrawGridSkills(Rect rect, Pawn pawn, float rowHeight)
        {
            Widgets.DrawMenuSection(rect);
            
            float padding = rowHeight * 0.5f;
            
            // 标题
            Rect titleRect = new Rect(
                padding, 
                rect.y + padding, 
                rect.width - padding * 2, 
                rowHeight
            );
            
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "Skills");
            Text.Font = GameFont.Small;
            
            // 技能列表区域
            Rect skillsArea = new Rect(
                0, 
                titleRect.yMax + padding, 
                rect.width, 
                rect.height - (titleRect.yMax - rect.y) - padding
            );
            
            if (pawn.skills == null || pawn.skills.skills.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(skillsArea.ContractedBy(padding * 2), "No skills");
                GUI.color = Color.white;
                return;
            }
            
            // 滚动区域
            float skillHeight = rowHeight * 1.2f;
            float viewHeight = pawn.skills.skills.Count * skillHeight + padding * 2;
            
            Rect viewRect = new Rect(0, 0, skillsArea.width - 16f, viewHeight);
            Widgets.BeginScrollView(skillsArea, ref scrollPosition, viewRect);
            
            float curY = 0;
            foreach (var skill in pawn.skills.skills)
            {
                if (skill == null || skill.TotallyDisabled)
                    continue;
                    
                Rect skillRect = new Rect(
                    padding, 
                    curY, 
                    viewRect.width - padding * 2, 
                    skillHeight
                );
                
                DrawGridSkill(skillRect, skill, rowHeight);
                curY += skillHeight + padding * 0.3f;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawGridSkill(Rect rect, SkillRecord skill, float rowHeight)
        {
            // 背景
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            
            // 布局：名称 | 进度条 | 等级
            float nameWidth = rect.width * 0.35f;
            float barWidth = rect.width * 0.45f;
            float levelWidth = rect.width * 0.2f;
            
            // 名称
            Rect nameRect = new Rect(rect.x, rect.y, nameWidth, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(nameRect, skill.def.LabelCap);
            
            // 进度条
            Rect barRect = new Rect(
                nameRect.xMax + rowHeight * 0.5f, 
                rect.y + (rect.height - 12f) / 2f, 
                barWidth - rowHeight, 
                12f
            );
            Widgets.FillableBar(barRect, skill.Level / 20f);
            
            // 等级
            Rect levelRect = new Rect(barRect.xMax + rowHeight * 0.5f, rect.y, levelWidth, rect.height);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(levelRect, $"Lv.{skill.Level}");
            
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 工具提示
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, 
                    $"<b>{skill.def.LabelCap}</b>\n" +
                    $"Level".Translate() + " : {skill.Level}/20\n\n" +
                    $"{skill.def.description}");
            }
        }
        
        private void DrawRenameButtons(Rect rect)
        {
            float buttonWidth = rect.width / 2 - 2.5f;
            
            Rect confirmRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(confirmRect, "OK"))
            {
                var pawn = SelPawn;
                if (pawn != null && !string.IsNullOrEmpty(nameBuffer))
                {
                    pawn.Name = new NameSingle(nameBuffer, false);
                    isRenaming = false;
                }
            }
            
            Rect cancelRect = new Rect(confirmRect.xMax + 5f, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                isRenaming = false;
            }
        }
        
        private string GetStatus(Pawn pawn)
        {
            if (pawn.Downed) return "Downed";
            if (pawn.Dead) return "Dead";
            if (pawn.Drafted) return "Drafted";
            
            var pilotComp = pawn.TryGetComp<CompMechPilotHolder>();
            if (pilotComp == null || !pilotComp.HasPilots)
                return "No Pilot";
                
            return "Operational";
        }
        
        private void DrawError(string message)
        {
            Rect rect = new Rect(0, 0, size.x, size.y);
            Widgets.DrawMenuSection(rect);
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = Color.yellow;
            Widgets.Label(rect.ContractedBy(30f), message);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        public override bool IsVisible
        {
            get
            {
                var pawn = SelPawn;
                return pawn != null && pawn.TryGetComp<CompMechSkillInheritance>() != null;
            }
        }
    }
}
