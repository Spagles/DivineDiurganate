using RimWorld;
using UnityEngine;
using Verse;
using System.Text;

namespace DivineDiurganate
{
    [StaticConstructorOnStartup]
    public class Gizmo_FaithStatus : Gizmo
    {
        private WorldComp_FaithSystem faithSystem;
        
        private static readonly Texture2D FullFaithBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.4f, 0.8f));
        private static readonly Texture2D EmptyFaithBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.3f, 0.3f, 0.35f));
        private static readonly Texture2D WarningFaithBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.4f, 0.2f));
        private static readonly Texture2D FaithIcon = ContentFinder<Texture2D>.Get("UI/Icons/FaithIcon", false);
        
        public Gizmo_FaithStatus()
        {
            this.faithSystem = WorldComp_FaithSystem.Instance;
            Order = -95f; // 在护盾Gizmo之前显示
        }
        
        public override float GetWidth(float maxWidth)
        {
            return 160f;
        }
        
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (faithSystem == null || !faithSystem.IsActive || faithSystem.CurrentLeader == null)
            {
                return new GizmoResult(GizmoState.Clear);
            }
            
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 85f);
            Rect contentRect = rect.ContractedBy(6f);
            
            // 绘制窗口背景
            Widgets.DrawWindowBackground(rect);
            
            // 图标区域
            if (FaithIcon != null)
            {
                Rect iconRect = new Rect(contentRect.x, contentRect.y, 32f, 32f);
                GUI.DrawTexture(iconRect, FaithIcon);
            }
            
            // 标题区域
            Rect titleRect = new Rect(contentRect.x + 35f, contentRect.y, contentRect.width - 35f, 30f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.9f, 0.9f, 1f);
            Widgets.Label(titleRect, "DD_Faith_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            
            // 领袖信息区域
            Rect leaderRect = new Rect(contentRect.x, contentRect.y + 30f, contentRect.width, 20f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.8f, 0.8f, 1f);
            
            string leaderName = faithSystem.CurrentLeader?.NameShortColored ?? "Unknown";
            Widgets.Label(leaderRect, "DD_Faith_Leader".Translate(leaderName));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            
            // 信仰条区域
            Rect barRect = new Rect(contentRect.x, contentRect.y + 50f, contentRect.width, 20f);
            
            // 选择颜色
            Texture2D barTex;
            if (faithSystem.FaithPercent < 0.2f)
            {
                barTex = WarningFaithBarTex;
            }
            else
            {
                barTex = FullFaithBarTex;
            }
            
            // 绘制信仰条
            Widgets.FillableBar(barRect, faithSystem.FaithPercent, barTex, EmptyFaithBarTex, doBorder: false);
            
            // 绘制数值文本
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string faithText = $"{faithSystem.CurrentFaith:F0}/{faithSystem.MaxFaith:F0}";
            Widgets.Label(barRect, faithText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 绘制百分比
            Rect percentRect = new Rect(barRect.x, barRect.y - 2f, barRect.width, 15f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            string percentText = $"({faithSystem.FaithPercent:P0})";
            Widgets.Label(percentRect, percentText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 工具提示
            string tip = GenerateTooltip();
            TooltipHandler.TipRegion(rect, tip);
            
            return new GizmoResult(GizmoState.Clear);
        }
        
        private string GenerateTooltip()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("DD_Faith_Tooltip_Title".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine();
            
            // 信仰值信息
            sb.AppendLine("DD_Faith_Current".Translate(
                faithSystem.CurrentFaith.ToString("F0"),
                faithSystem.MaxFaith.ToString("F0"),
                faithSystem.FaithPercent.ToStringPercent()
            ));
            
            // 领袖信息
            if (faithSystem.CurrentLeader != null)
            {
                sb.AppendLine();
                sb.AppendLine("DD_Faith_Leader_Info".Translate(
                    faithSystem.CurrentLeader.NameShortColored
                ));
                
                // 领袖状态影响
                float healthPercent = faithSystem.CurrentLeader.health.summaryHealth.SummaryHealthPercent;
                if (healthPercent < 0.5f)
                {
                    sb.AppendLine("DD_Faith_Leader_Injured".Translate(healthPercent.ToStringPercent()).Colorize(Color.yellow));
                }
                
                if (faithSystem.CurrentLeader.needs?.mood != null)
                {
                    float mood = faithSystem.CurrentLeader.needs.mood.CurLevelPercentage;
                    if (mood < 0.3f)
                    {
                        sb.AppendLine("DD_Faith_Leader_Unhappy".Translate().Colorize(Color.yellow));
                    }
                    else if (mood > 0.8f)
                    {
                        sb.AppendLine("DD_Faith_Leader_Happy".Translate().Colorize(Color.green));
                    }
                }
            }
            
            // 信徒信息
            sb.AppendLine();
            sb.AppendLine("DD_Faith_Followers".Translate(
                faithSystem.FollowerCount,
                faithSystem.FollowerCount * 100 // 每个信徒提供的信仰上限
            ));
            
            // 信仰效果
            sb.AppendLine();
            sb.AppendLine("DD_Faith_Effects_Title".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine("DD_Faith_Effect_1".Translate());
            sb.AppendLine("DD_Faith_Effect_2".Translate());
            sb.AppendLine("DD_Faith_Effect_3".Translate());
            
            // 调试信息
            if (DebugSettings.godMode)
            {
                sb.AppendLine();
                sb.AppendLine("--- Debug Info ---".Colorize(Color.gray));
                sb.AppendLine($"System Active: {faithSystem.IsActive}");
                sb.AppendLine($"Leader Valid: {faithSystem.CurrentLeader != null && !faithSystem.CurrentLeader.Dead}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 检查是否应该显示此Gizmo
        /// </summary>
        public bool ShouldDisplay()
        {
            return faithSystem != null && faithSystem.IsActive && faithSystem.CurrentLeader != null;
        }
    }
}
