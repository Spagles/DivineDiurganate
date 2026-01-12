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
        private static readonly Texture2D FullWishBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.4f, 0.8f, 0.2f));
        private static readonly Texture2D EmptyWishBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.35f, 0.3f));
        
        // 悬浮窗口文本颜色
        private static readonly Color DescriptionColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        
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
            
            // 75f高度
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect contentRect = rect.ContractedBy(4f);
            
            // 绘制窗口背景
            Widgets.DrawWindowBackground(rect);
            
            // 计算布局
            float titleHeight = 18f;
            float barHeight = 16f;
            float barSpacing = 4f;
            
            // 标题区域
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, titleHeight);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = new Color(0.9f, 0.9f, 1f);
            Widgets.Label(titleRect, "DD_Gizmo_Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            
            // 信仰条区域
            Rect faithBarRect = new Rect(contentRect.x, titleRect.yMax + barSpacing, contentRect.width, barHeight);
            
            // 选择颜色
            Texture2D faithBarTex;
            if (faithSystem.FaithPercent < 0.2f)
            {
                faithBarTex = WarningFaithBarTex;
            }
            else
            {
                faithBarTex = FullFaithBarTex;
            }
            
            // 绘制信仰条
            Widgets.FillableBar(faithBarRect, faithSystem.FaithPercent, faithBarTex, EmptyFaithBarTex, doBorder: true);
            
            // 绘制信仰值文本
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string faithText = $"{faithSystem.CurrentFaith:F0}/{faithSystem.MaxFaith:F0}";
            GUI.color = Color.white;
            Widgets.Label(faithBarRect, faithText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 为信仰条设置独立的悬浮提示
            string faithTooltip = GenerateFaithTooltip();
            TooltipHandler.TipRegion(faithBarRect, new TipSignal(faithTooltip, 32456)); // 使用唯一的key
            
            // 祈愿条区域
            Rect wishBarRect = new Rect(contentRect.x, faithBarRect.yMax + barSpacing, contentRect.width, barHeight);
            
            // 绘制祈愿条
            Widgets.FillableBar(wishBarRect, faithSystem.WishPercent, FullWishBarTex, EmptyWishBarTex, doBorder: true);
            
            // 绘制祈愿值文本
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            string wishText = $"{faithSystem.CurrentWish:F0}/{faithSystem.MaxWish:F0}";
            GUI.color = Color.white;
            Widgets.Label(wishBarRect, wishText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 为祈愿条设置独立的悬浮提示
            string wishTooltip = GenerateWishTooltip();
            TooltipHandler.TipRegion(wishBarRect, new TipSignal(wishTooltip, 32457)); // 使用唯一的key
            
            // 在祈愿条下方显示恢复速率（小字体）
            Rect recoveryRect = new Rect(wishBarRect.x, wishBarRect.yMax + 2f, wishBarRect.width, 12f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            string recoveryText = "DD_Gizmo_RecoveryRate".Translate(faithSystem.WishRecoveryRate.ToString("F1"));
            Widgets.Label(recoveryRect, recoveryText);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            
            // 为整个Gizmo区域设置基础工具提示
            string basicTooltip = GenerateBasicTooltip();
            TooltipHandler.TipRegion(rect, new TipSignal(basicTooltip, 32458));
            
            return new GizmoResult(GizmoState.Clear);
        }
        
        /// <summary>
        /// 生成信仰条的详细悬浮提示
        /// </summary>
        private string GenerateFaithTooltip()
        {
            StringBuilder sb = new StringBuilder();
            
            // 标题
            sb.AppendLine("DD_FaithTooltip_Title".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine();
            
            // 当前数值/最大数值
            sb.AppendLine("DD_FaithTooltip_Current".Translate(faithSystem.CurrentFaith.ToString("F0"), faithSystem.MaxFaith.ToString("F0")));
            sb.AppendLine("DD_FaithTooltip_Percent".Translate(faithSystem.FaithPercent.ToStringPercent()));
            
            // 影响因素
            sb.AppendLine();
            sb.AppendLine("DD_FaithTooltip_Factors".Translate().Colorize(ColoredText.TipSectionTitleColor));
            
            // 信徒数量影响
            sb.AppendLine("DD_FaithTooltip_Followers".Translate(faithSystem.FollowerCount));
            sb.AppendLine("DD_FaithTooltip_FollowerCapacity".Translate());
            sb.AppendLine("DD_FaithTooltip_TotalCapacity".Translate((faithSystem.FollowerCount * 100).ToString("F0")));
            
            // 领袖状态影响
            if (faithSystem.CurrentLeader != null)
            {
                sb.AppendLine("DD_FaithTooltip_Leader".Translate(faithSystem.CurrentLeader.NameShortColored));
                
                float healthPercent = faithSystem.CurrentLeader.health.summaryHealth.SummaryHealthPercent;
                sb.AppendLine("DD_FaithTooltip_Health".Translate(healthPercent.ToStringPercent()));
                
                if (faithSystem.CurrentLeader.needs?.mood != null)
                {
                    float mood = faithSystem.CurrentLeader.needs.mood.CurLevelPercentage;
                    sb.AppendLine("DD_FaithTooltip_Mood".Translate(mood.ToStringPercent()));
                    
                    if (mood < 0.3f)
                        sb.AppendLine("DD_FaithTooltip_MoodPenalty".Translate().Colorize(Color.yellow));
                }
            }
            
            // 描述（使用指定颜色）
            sb.AppendLine();
            sb.AppendLine("DD_FaithTooltip_Description".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine("DD_FaithTooltip_DescriptionText".Translate().Colorize(ColoredText.TipSectionTitleColor));
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 生成祈愿条的详细悬浮提示
        /// </summary>
        private string GenerateWishTooltip()
        {
            StringBuilder sb = new StringBuilder();
            
            // 标题
            sb.AppendLine("DD_WishTooltip_Title".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine();
            
            // 当前数值/最大数值
            sb.AppendLine("DD_WishTooltip_Current".Translate(faithSystem.CurrentWish.ToString("F0"), faithSystem.MaxWish.ToString("F0")));
            sb.AppendLine("DD_WishTooltip_Percent".Translate(faithSystem.WishPercent.ToStringPercent()));
            sb.AppendLine("DD_WishTooltip_Recovery".Translate(faithSystem.WishRecoveryRate.ToString("F1")));
            
            // 影响因素
            sb.AppendLine();
            sb.AppendLine("DD_WishTooltip_Factors".Translate().Colorize(ColoredText.TipSectionTitleColor));
            
            // 上限计算
            float remainingFaith = faithSystem.GetRemainingFaithCapacity();
            sb.AppendLine("DD_WishTooltip_Capacity".Translate(remainingFaith.ToString("F0")));
            sb.AppendLine("DD_WishTooltip_CapacityExplanation".Translate());
            sb.AppendLine("DD_WishTooltip_MaxFaith".Translate(faithSystem.MaxFaith.ToString("F0")));
            sb.AppendLine("DD_WishTooltip_CurrentFaith".Translate(faithSystem.CurrentFaith.ToString("F0")));
            
            // 恢复速率影响因素
            sb.AppendLine("DD_WishTooltip_RecoveryRate".Translate(faithSystem.WishRecoveryRate.ToString("F1")));
            sb.AppendLine("DD_WishTooltip_BaseRate".Translate());
            
            // 领袖影响
            if (faithSystem.CurrentLeader != null)
            {
                float healthPercent = faithSystem.CurrentLeader.health.summaryHealth.SummaryHealthPercent;
                if (healthPercent < 0.5f)
                {
                    sb.AppendLine("DD_WishTooltip_HealthPenalty".Translate(healthPercent.ToStringPercent()).Colorize(Color.yellow));
                }
                
                if (faithSystem.CurrentLeader.needs?.mood != null)
                {
                    float mood = faithSystem.CurrentLeader.needs.mood.CurLevelPercentage;
                    if (mood < 0.3f)
                    {
                        sb.AppendLine("DD_WishTooltip_MoodPenalty".Translate().Colorize(Color.yellow));
                    }
                    else if (mood > 0.8f)
                    {
                        sb.AppendLine("DD_WishTooltip_MoodBonus".Translate().Colorize(Color.green));
                    }
                }
            }
            
            // 信徒数量影响
            sb.AppendLine("DD_WishTooltip_FollowersBonus".Translate(faithSystem.FollowerCount));
            
            // 恢复时间计算
            float wishRemaining = faithSystem.MaxWish - faithSystem.CurrentWish;
            if (faithSystem.WishRecoveryRate > 0f && wishRemaining > 0f)
            {
                float hoursToFull = wishRemaining / faithSystem.WishRecoveryRate * 24f;
                if (hoursToFull > 0f && hoursToFull < 1000f)
                {
                    sb.AppendLine("DD_WishTooltip_TimeToFull".Translate(hoursToFull.ToString("F1")));
                }
            }
            
            // 描述（使用指定颜色）
            sb.AppendLine();
            sb.AppendLine("DD_WishTooltip_Description".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine("DD_WishTooltip_DescriptionText".Translate().Colorize(ColoredText.TipSectionTitleColor));
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 生成Gizmo的基础工具提示（鼠标悬停在非进度条区域时显示）
        /// </summary>
        private string GenerateBasicTooltip()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("DD_Gizmo_BaseTitle".Translate().Colorize(ColoredText.TipSectionTitleColor));
            sb.AppendLine();
            sb.AppendLine("DD_Gizmo_HoverHint".Translate());
            sb.AppendLine();
            sb.AppendLine("DD_Gizmo_FaithDesc".Translate());
            sb.AppendLine("DD_Gizmo_WishDesc".Translate());
            
            if (faithSystem.CurrentLeader != null)
            {
                sb.AppendLine();
                sb.AppendLine("DD_Gizmo_CurrentLeader".Translate(faithSystem.CurrentLeader.NameShortColored));
            }
            
            if (DebugSettings.godMode)
            {
                sb.AppendLine();
                sb.AppendLine("DD_Gizmo_DebugHeader".Translate().Colorize(Color.gray));
                sb.AppendLine("DD_Gizmo_DebugSystemActive".Translate(faithSystem.IsActive.ToString()));
                sb.AppendLine("DD_Gizmo_DebugFollowers".Translate(faithSystem.FollowerCount.ToString()));
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
