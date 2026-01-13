using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// Hediff选择窗口 - 展示多个Hediff选项供玩家选择
    /// 类似卡牌游戏的选择界面风格
    /// </summary>
    public class Window_HediffSelection : Window
    {
        // 选项列表
        private readonly List<HediffDef> hediffOptions;
        
        // 目标Pawn（用于显示）
        private readonly Pawn targetPawn;
        
        // 选择回调
        private readonly Action<HediffDef> onSelect;
        
        // 窗口标题
        private readonly string titleKey;
        
        // 是否允许取消
        private readonly bool allowCancel;
        
        // 动画相关
        private float openAnimProgress = 0f;
        private const float OpenAnimDuration = 0.3f;
        
        // 悬停的选项索引
        private int hoveredIndex = -1;
        
        // 卡片尺寸和布局
        private const float CardWidth = 200f;
        private const float CardHeight = 280f;
        private const float CardSpacing = 20f;
        private const float TitleHeight = 50f;
        private const float BottomPadding = 60f;
        
        public override Vector2 InitialSize
        {
            get
            {
                float totalWidth = hediffOptions.Count * CardWidth + (hediffOptions.Count - 1) * CardSpacing + 60f;
                float totalHeight = TitleHeight + CardHeight + BottomPadding + 40f;
                return new Vector2(Mathf.Max(totalWidth, 500f), totalHeight);
            }
        }

        public Window_HediffSelection(
            List<HediffDef> options, 
            Pawn target, 
            Action<HediffDef> selectCallback,
            string title = "DD_HediffGacha_Title",
            bool canCancel = false)
        {
            hediffOptions = options ?? new List<HediffDef>();
            targetPawn = target;
            onSelect = selectCallback;
            titleKey = title;
            allowCancel = canCancel;
            
            // 窗口设置
            doCloseButton = false;
            doCloseX = allowCancel;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnCancel = allowCancel;
            forcePause = true;
            layer = WindowLayer.Dialog;
            
            // 打开动画
            openAnimProgress = 0f;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 更新动画
            if (openAnimProgress < 1f)
            {
                openAnimProgress += Time.deltaTime / OpenAnimDuration;
                openAnimProgress = Mathf.Clamp01(openAnimProgress);
            }
            
            // 绘制标题
            DrawTitle(inRect);
            
            // 绘制目标信息
            DrawTargetInfo(inRect);
            
            // 绘制Hediff卡片
            DrawHediffCards(inRect);
            
            // 绘制底部提示
            DrawBottomHint(inRect);
        }
        
        private void DrawTitle(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string title = titleKey.Translate();
            Rect titleRect = new Rect(0f, 5f, inRect.width, 35f);
            
            // 标题淡入效果
            GUI.color = new Color(1f, 1f, 1f, openAnimProgress);
            Widgets.Label(titleRect, title);
            GUI.color = Color.white;
            
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        private void DrawTargetInfo(Rect inRect)
        {
            if (targetPawn == null)
                return;
                
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string targetInfo = "DD_HediffGacha_TargetInfo".Translate(targetPawn.LabelShortCap);
            Rect infoRect = new Rect(0f, 38f, inRect.width, 20f);
            
            GUI.color = new Color(0.8f, 0.8f, 0.8f, openAnimProgress);
            Widgets.Label(infoRect, targetInfo);
            GUI.color = Color.white;
            
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private void DrawHediffCards(Rect inRect)
        {
            if (hediffOptions.NullOrEmpty())
                return;
                
            float totalCardsWidth = hediffOptions.Count * CardWidth + (hediffOptions.Count - 1) * CardSpacing;
            float startX = (inRect.width - totalCardsWidth) / 2f;
            float cardY = TitleHeight + 15f;
            
            for (int i = 0; i < hediffOptions.Count; i++)
            {
                float cardX = startX + i * (CardWidth + CardSpacing);
                
                // 卡片动画偏移（从下方飞入）
                float animDelay = i * 0.1f;
                float cardAnimProgress = Mathf.Clamp01((openAnimProgress - animDelay) / (1f - animDelay));
                float yOffset = (1f - cardAnimProgress) * 50f;
                
                Rect cardRect = new Rect(cardX, cardY + yOffset, CardWidth, CardHeight);
                
                // 设置透明度
                GUI.color = new Color(1f, 1f, 1f, cardAnimProgress);
                
                DrawHediffCard(cardRect, hediffOptions[i], i);
                
                GUI.color = Color.white;
            }
        }
        
        private void DrawHediffCard(Rect rect, HediffDef hediff, int index)
        {
            bool isHovered = Mouse.IsOver(rect);
            
            // 悬停效果
            if (isHovered)
            {
                hoveredIndex = index;
                rect = rect.ExpandedBy(5f);
            }
            else if (hoveredIndex == index)
            {
                hoveredIndex = -1;
            }
            
            // 绘制卡片背景
            Color bgColor = isHovered ? new Color(0.25f, 0.25f, 0.3f) : new Color(0.15f, 0.15f, 0.18f);
            Widgets.DrawBoxSolid(rect, bgColor);
            
            // 绘制边框
            Color borderColor = isHovered ? new Color(0.8f, 0.7f, 0.4f) : new Color(0.4f, 0.4f, 0.4f);
            Widgets.DrawBox(rect, 2, Texture2D.whiteTexture);
            GUI.color = borderColor;
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
            
            float padding = 10f;
            float contentWidth = rect.width - padding * 2;
            float currentY = rect.y + padding;
            
            // 绘制Hediff图标
            Rect iconRect = new Rect(rect.x + (rect.width - 64f) / 2f, currentY, 64f, 64f);
            DrawHediffIcon(iconRect, hediff);
            currentY += 70f;
            
            // 绘制Hediff名称
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            Rect nameRect = new Rect(rect.x + padding, currentY, contentWidth, 30f);
            string hediffName = hediff.LabelCap;
            Widgets.Label(nameRect, hediffName);
            currentY += 35f;
            
            // 绘制分隔线
            Widgets.DrawLineHorizontal(rect.x + padding, currentY, contentWidth);
            currentY += 10f;
            
            // 绘制Hediff描述
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            
            float descHeight = CardHeight - (currentY - rect.y) - 50f;
            Rect descRect = new Rect(rect.x + padding, currentY, contentWidth, descHeight);
            
            string description = hediff.Description;
            if (description.Length > 200)
            {
                description = description.Substring(0, 197) + "...";
            }
            
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(descRect, description);
            GUI.color = Color.white;
            currentY += descHeight + 5f;
            
            // 绘制选择按钮
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            Rect buttonRect = new Rect(rect.x + padding + 10f, rect.yMax - 40f, contentWidth - 20f, 30f);
            
            if (Widgets.ButtonText(buttonRect, "DD_HediffGacha_Select".Translate()))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                SelectHediff(hediff);
            }
            
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            // 悬停时显示完整工具提示
            if (isHovered)
            {
                string tooltip = hediff.LabelCap + "\n\n" + hediff.Description;
                TooltipHandler.TipRegion(rect, tooltip);
            }
        }
        
        private void DrawHediffIcon(Rect rect, HediffDef hediff)
        {
            // 尝试获取Hediff的图标
            Texture2D icon = null;
            
            // 根据Hediff是好是坏选择不同的默认图标
            string iconPath = hediff.isBad 
                ? "UI/Icons/Medical/Plague" 
                : "UI/Icons/Medical/BandageIcon";
            
            icon = ContentFinder<Texture2D>.Get(iconPath, false);
            
            // 如果还是没有，尝试其他备选图标
            if (icon == null)
            {
                icon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Vaccinate", false);
            }
            
            // 如果找到图标则绘制
            if (icon != null)
            {
                GUI.DrawTexture(rect, icon);
            }
            else
            {
                // 绘制一个带颜色的占位符
                Color placeholderColor = hediff.isBad 
                    ? new Color(0.5f, 0.2f, 0.2f) 
                    : new Color(0.2f, 0.5f, 0.3f);
                Widgets.DrawBoxSolid(rect, placeholderColor);
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                Widgets.Label(rect, hediff.LabelCap.ToString().Substring(0, 1).ToUpper());
            }
        }
        
        private void DrawBottomHint(Rect inRect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, openAnimProgress);
            
            string hint = allowCancel 
                ? "DD_HediffGacha_HintWithCancel".Translate() 
                : "DD_HediffGacha_Hint".Translate();
                
            Rect hintRect = new Rect(0f, inRect.height - 25f, inRect.width, 20f);
            Widgets.Label(hintRect, hint);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
        
        private void SelectHediff(HediffDef hediff)
        {
            onSelect?.Invoke(hediff);
            Close();
        }
        
        public override void OnCancelKeyPressed()
        {
            if (allowCancel)
            {
                onSelect?.Invoke(null);
                base.OnCancelKeyPressed();
            }
            // 如果不允许取消，则忽略ESC键
        }
        
        public override void PreClose()
        {
            base.PreClose();
        }
    }
}
