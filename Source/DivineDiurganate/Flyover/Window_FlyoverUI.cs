using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机UI窗口 - 改进版布局
    /// </summary>
    public class Window_FlyoverUI : Window
    {
        private WorldComp_FlyoverManager manager;
        private Vector2 scrollPosition = Vector2.zero;
        private float windowHeight = 600f;
        private float windowWidth = 600f;
        
        // 布局参数
        private float itemHeight = 120f;
        private float itemMargin = 10f;
        
        // 缩小状态相关
        private bool isMinimized = false;
        private Vector2 minimizedSize = new Vector2(60f, 60f);
        private float minimizedOpacity = 0.8f;
        private Texture2D minimizedIcon;

        // 状态条颜色（基于状态）
        private Dictionary<FlyoverStatus, Color> statusColors = new Dictionary<FlyoverStatus, Color>()
        {
            { FlyoverStatus.OnMap, Color.green },
            { FlyoverStatus.Standby, Color.yellow },
            { FlyoverStatus.Deploying, Color.blue },
            { FlyoverStatus.Destroyed, Color.gray }
        };

        // UI样式
        private static readonly Color ButtonColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        private static readonly Color ButtonHoverColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        private static readonly Color ButtonPressedColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
        private static readonly Color SkillSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        private static readonly Color SkillSlotReadyColor = new Color(0.2f, 0.4f, 0.8f, 0.8f);
        private static readonly Color SkillSlotCooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        private static readonly Color DockButtonColor = new Color(0.3f, 0.7f, 0.3f, 0.8f); // 停靠按钮颜色（绿色系）
        private static readonly Color DockButtonHoverColor = new Color(0.4f, 0.8f, 0.4f, 0.9f);
        private static readonly Color DockButtonPressedColor = new Color(0.2f, 0.6f, 0.2f, 0.9f);
        private static readonly Color LandButtonColor = new Color(0.8f, 0.4f, 0.2f, 0.8f); // 降落按钮颜色（橙色系）
        private static readonly Color ReEnterButtonColor = new Color(0.4f, 0.2f, 0.8f, 0.8f); // 重新入场按钮颜色（紫色系）

        // 用于处理按钮点击的临时变量
        private FlyoverData pendingReEnterFlyover;

        // 窗口尺寸属性
        public override Vector2 InitialSize => isMinimized ? minimizedSize : new Vector2(windowWidth, windowHeight);

        public Window_FlyoverUI(WorldComp_FlyoverManager manager)
        {
            this.manager = manager;
            this.draggable = true;
            this.doCloseX = true;
            this.doWindowBackground = true;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.SubSuper;
            this.resizeable = false; // 禁止用户调整大小，确保尺寸一致

            // 设置窗口初始位置（使用InitialSize属性控制大小）
            float initialX = UI.screenWidth - windowWidth - 20f; // 屏幕右侧留出20px边距
            this.windowRect = new Rect(initialX, 100f, windowWidth, windowHeight);

            // 加载图标
            minimizedIcon = ContentFinder<Texture2D>.Get("UI/Icons/AircraftIcon", false);
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 先检查是否有待处理的重新入场请求
                if (pendingReEnterFlyover != null)
                {
                    HandlePendingReEnter();
                }

                // 根据状态绘制不同内容
                if (isMinimized)
                {
                    DrawMinimizedWindow(inRect);
                }
                else
                {
                    DrawExpandedWindow(inRect);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in FlyoverUI: {ex}");
            }
        }

        /// <summary>
        /// 处理待处理的重新入场请求
        /// </summary>
        private void HandlePendingReEnter()
        {
            if (pendingReEnterFlyover == null) return;

            // 检查是否有地图
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("DD_Flyover_NoMap".Translate(), MessageTypeDefOf.RejectInput);
                pendingReEnterFlyover = null;
                return;
            }

            Log.Message($"Window_FlyoverUI: 开始重新入场选择流程，战机={pendingReEnterFlyover.DisplayName}");

            // 直接开始选择第一个点（模仿CompFlyOverGenerator）
            StartReEnterSelection(pendingReEnterFlyover, currentMap);
            pendingReEnterFlyover = null;
        }

        /// <summary>
        /// 绘制展开状态的窗口
        /// </summary>
        private void DrawExpandedWindow(Rect inRect)
        {
            // 绘制标题栏
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 50f);
            DrawTitleBar(titleRect);

            // 绘制隐藏/缩小按钮
            Rect hideButtonRect = new Rect(inRect.x + inRect.width - 30f, inRect.y + 5f, 25f, 25f);
            if (Widgets.ButtonImageFitted(hideButtonRect, TexButton.Minus))
            {
                ToggleMinimize();
            }

            // 滚动区域 - 使用固定布局，避免动态计算
            float scrollAreaWidth = windowWidth - 60f; // 左右各30px边距
            float scrollAreaHeight = windowHeight - 80f; // 标题栏50px + 上下间距30px
            
            Rect scrollRect = new Rect(
                inRect.x + 30f, // 30px左边距
                titleRect.yMax + 15f, // 标题栏下方15px
                scrollAreaWidth,
                scrollAreaHeight
            );

            // 获取战机数据
            List<FlyoverData> activeFlyovers = manager.ActiveFlyoverData;

            if (activeFlyovers.Count == 0)
            {
                // 无战机提示
                Rect messageRect = new Rect(
                    scrollRect.x,
                    scrollRect.y + scrollRect.height / 2 - 25f,
                    scrollRect.width,
                    50f
                );
                
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(messageRect, "DD_Flyover_NoAircraft".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 计算滚动内容高度
            float contentHeight = (itemHeight + itemMargin) * activeFlyovers.Count - itemMargin;
            
            // 滚动视图 - 设置固定宽度，避免内容宽度变化
            Rect viewRect = new Rect(0f, 0f, scrollAreaWidth - 20f, contentHeight);
            
            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
            
            float currentY = 0f;
            for (int i = 0; i < activeFlyovers.Count; i++)
            {
                var flyover = activeFlyovers[i];
                Rect itemRect = new Rect(0f, currentY, viewRect.width, itemHeight);
                DrawFlyoverItem(itemRect, flyover);
                currentY += itemHeight + itemMargin;
            }
            
            GUI.EndScrollView();
        }

        /// <summary>
        /// 绘制单个战机项
        /// </summary>
        private void DrawFlyoverItem(Rect rect, FlyoverData flyover)
        {
            // 绘制背景
            Widgets.DrawMenuSection(rect);
            
            // 状态指示器（左侧竖条）
            Rect statusBarRect = new Rect(rect.x, rect.y, 10f, rect.height);
            Widgets.DrawBoxSolid(statusBarRect, GetStatusColor(flyover.status));

            // 图标区域
            Rect iconRect = new Rect(
                statusBarRect.xMax + 20f,
                rect.y + (rect.height - 100f) / 2,
                100f,
                100f
            );
            
            // 绘制图标背景
            Widgets.DrawBox(iconRect);
            
            if (flyover.DisplayIcon != null)
            {
                GUI.DrawTexture(iconRect, flyover.DisplayIcon);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(iconRect, "DD_Flyover_Icon".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 名字和技能区域 - 使用固定宽度计算
            float infoAreaWidth = windowWidth - 60f - 150f - 20f; // 总宽度 - 边距 - 图标 - 右边距
            Rect infoRect = new Rect(
                iconRect.xMax + 10f,
                rect.y,
                infoAreaWidth,
                rect.height
            );

            // 名字和状态区域
            Rect nameStatusRect = new Rect(
                infoRect.x,
                infoRect.y + 10f,
                infoRect.width,
                30f
            );

            // 名字（左侧60%）
            Rect nameRect = new Rect(
                nameStatusRect.x + 5f,
                nameStatusRect.y,
                nameStatusRect.width * 0.6f - 5f,
                nameStatusRect.height
            );
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(nameRect, flyover.DisplayName.Truncate(nameRect.width - 10f));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 状态（右侧30%）
            Rect statusRect = new Rect(
                nameRect.xMax + nameStatusRect.width * 0.1f,
                nameStatusRect.y,
                nameStatusRect.width * 0.3f - 5f,
                nameStatusRect.height
            );
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = GetStatusColor(flyover.status);
            Widgets.Label(statusRect, GetStatusShortDescription(flyover.status));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 根据状态绘制不同的按钮区域
            Rect skillAreaRect = new Rect(
                infoRect.x,
                nameStatusRect.yMax + 5f,
                infoRect.width,
                infoRect.height - nameStatusRect.height - 15f
            );

            // 根据战机状态绘制不同的按钮
            if (flyover.status == FlyoverStatus.Standby)
            {
                // Standby状态：绘制降落按钮和重新入场按钮
                DrawStandbyButtons(skillAreaRect, flyover);
            }
            else if (flyover.status == FlyoverStatus.OnMap)
            {
                // OnMap状态：绘制4个技能槽和停靠按钮
                DrawOnMapButtons(skillAreaRect, flyover);
            }
            else
            {
                // 其他状态：不绘制按钮或绘制不同的UI
                DrawOtherStateUI(skillAreaRect, flyover);
            }
        }

        /// <summary>
        /// 绘制Standby状态的按钮（降落和重新入场）
        /// </summary>
        private void DrawStandbyButtons(Rect areaRect, FlyoverData flyover)
        {
            // 绘制2个槽位（降落按钮 + 重新入场按钮）
            float buttonSize = 63f;
            float buttonSpacing = 5f;
            float totalButtonsWidth = buttonSize * 2 + buttonSpacing;
            float buttonStartX = areaRect.x + (areaRect.width - totalButtonsWidth) / 2f;
            float buttonStartY = areaRect.y + (areaRect.height - buttonSize) / 2f;

            // 降落按钮（slot1位置）
            Rect landButtonRect = new Rect(
                buttonStartX,
                buttonStartY,
                buttonSize,
                buttonSize
            );

            DrawLandButton(landButtonRect, flyover);

            // 重新入场按钮（slot2位置）
            Rect reEnterButtonRect = new Rect(
                buttonStartX + buttonSize + buttonSpacing,
                buttonStartY,
                buttonSize,
                buttonSize
            );

            DrawReEnterButton(reEnterButtonRect, flyover);
        }

        /// <summary>
        /// 绘制OnMap状态的按钮（4个技能槽 + 停靠按钮）
        /// </summary>
        private void DrawOnMapButtons(Rect areaRect, FlyoverData flyover)
        {
            // 绘制5个槽位（4个技能槽 + 1个停靠按钮）
            float skillSize = 63f;
            float skillSpacing = 5f;
            float totalSkillsWidth = skillSize * 5 + skillSpacing * 4;
            float skillStartX = areaRect.x + (areaRect.width - totalSkillsWidth) / 2f;
            float skillStartY = areaRect.y + (areaRect.height - skillSize) / 2f;

            // 绘制4个技能槽
            for (int i = 0; i < 4; i++)
            {
                Rect skillRect = new Rect(
                    skillStartX + i * (skillSize + skillSpacing),
                    skillStartY,
                    skillSize,
                    skillSize
                );

                DrawSkillSlot(skillRect, flyover, i);
            }

            // 绘制第5个槽位：停靠按钮
            Rect dockButtonRect = new Rect(
                skillStartX + 4 * (skillSize + skillSpacing),
                skillStartY,
                skillSize,
                skillSize
            );

            DrawDockButtonSlot(dockButtonRect, flyover);
        }

        /// <summary>
        /// 绘制其他状态的UI
        /// </summary>
        private void DrawOtherStateUI(Rect areaRect, FlyoverData flyover)
        {
            // 根据其他状态绘制不同的UI
            switch (flyover.status)
            {
                case FlyoverStatus.Deploying:
                    // 部署中状态
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.yellow;
                    Widgets.Label(areaRect, "DD_Flyover_Deploying".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;
                    
                case FlyoverStatus.Destroyed:
                    // 已销毁状态
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(areaRect, "DD_Flyover_Destroyed".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;
                    
                default:
                    // 未知状态
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.white;
                    Widgets.Label(areaRect, "DD_Flyover_Status_Unknown".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;
            }
        }

        /// <summary>
        /// 绘制降落按钮
        /// </summary>
        private void DrawLandButton(Rect rect, FlyoverData flyover)
        {
            // 只调用一次 Widgets.ButtonInvisible，并保存结果
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            // 绘制按钮背景
            Color buttonBgColor = LandButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            // 绘制按钮文字
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            string buttonText = "DD_Flyover_Land".Translate(); // "降落"
            
            Widgets.Label(rect, buttonText);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 按钮点击（暂不实现逻辑）
            if (buttonClicked)
            {
                Log.Message($"Window_FlyoverUI: 降落按钮点击，战机={flyover.DisplayName}");
                OnLandButtonClicked(flyover);
            }

            // 鼠标悬停提示
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, "DD_Flyover_Land_Tooltip".Translate(flyover.DisplayName));
            }
        }

        /// <summary>
        /// 绘制重新入场按钮
        /// </summary>
        private void DrawReEnterButton(Rect rect, FlyoverData flyover)
        {
            // 只调用一次 Widgets.ButtonInvisible，并保存结果
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            // 绘制按钮背景
            Color buttonBgColor = ReEnterButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            // 绘制按钮文字
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            string buttonText = "DD_Flyover_ReEnter".Translate(); // "重新入场"
            
            Widgets.Label(rect, buttonText);
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 按钮点击
            if (buttonClicked)
            {
                Log.Message($"Window_FlyoverUI: 重新入场按钮点击，战机={flyover.DisplayName}");
                // 不直接调用，而是设置待处理状态
                pendingReEnterFlyover = flyover;
            }

            // 鼠标悬停提示
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, "DD_Flyover_ReEnter_Tooltip".Translate(flyover.DisplayName));
            }
        }

        /// <summary>
        /// 绘制技能槽
        /// </summary>
        private void DrawSkillSlot(Rect rect, FlyoverData flyover, int slotIndex)
        {
            var skillComp = GetSkillComp(flyover, slotIndex);
            
            // 绘制技能槽背景
            Widgets.DrawBox(rect);
            
            if (skillComp != null)
            {
                // 检查技能是否可用
                bool canUse = skillComp.CanUseNow(out string reason);
                Color skillColor = canUse ? SkillSlotReadyColor : SkillSlotCooldownColor;
                
                // 绘制技能背景色
                Widgets.DrawBoxSolid(rect, skillColor);

                // 绘制冷却效果
                if (skillComp.CooldownPercent > 0.01f)
                {
                    float cooldownHeight = rect.height * skillComp.CooldownPercent;
                    Rect cooldownRect = new Rect(
                        rect.x,
                        rect.y,
                        rect.width,
                        cooldownHeight
                    );
                    Widgets.DrawBoxSolid(cooldownRect, new Color(0f, 0f, 0f, 0.6f));
                }

                // 绘制技能图标
                Texture2D skillIcon = skillComp.GetSkillIcon();
                if (skillIcon != null && skillIcon != BaseContent.BadTex)
                {
                    GUI.DrawTexture(rect.ContractedBy(2f), skillIcon);
                }

                // 技能编号（小字显示在角落）
                Rect numberRect = new Rect(rect.x, rect.y, 12f, 12f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                Widgets.Label(numberRect, (slotIndex + 1).ToString());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // 技能点击
                if (Widgets.ButtonInvisible(rect))
                {
                    OnSkillClicked(skillComp);
                }
            }
            else
            {
                // 空的技能槽
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(rect, $"DD_Flyover_SkillSlot".Translate(slotIndex + 1));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 鼠标悬停提示
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, GetSkillTooltip(flyover, slotIndex));
            }
        }

        /// <summary>
        /// 绘制停靠按钮槽位（作为第五个技能槽）
        /// </summary>
        private void DrawDockButtonSlot(Rect rect, FlyoverData flyover)
        {
            // 只调用一次 Widgets.ButtonInvisible，并保存结果
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            // 绘制按钮背景
            Color buttonBgColor = DockButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? DockButtonPressedColor : DockButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            // 绘制按钮图标（使用停靠/部署图标）
            string buttonText = flyover.CanDeploy() ? "DD_Flyover_Dock".Translate() : "DD_Flyover_Deploy".Translate();
            
            // 将文本分成两行显示（如果需要）
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            // 根据文字长度决定是否换行
            if (buttonText.Length <= 3)
            {
                // 短文本单行显示
                Widgets.Label(rect, buttonText);
            }
            else
            {
                // 长文本分成两行
                float lineHeight = rect.height / 2f;
                Rect line1Rect = new Rect(rect.x, rect.y, rect.width, lineHeight);
                Rect line2Rect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight);
                
                // 简单分割：如果包含空格，按空格分割，否则平均分割
                if (buttonText.Contains(" "))
                {
                    string[] parts = buttonText.Split(' ');
                    if (parts.Length >= 2)
                    {
                        Widgets.Label(line1Rect, parts[0]);
                        Widgets.Label(line2Rect, parts[1]);
                    }
                    else
                    {
                        // 如果没有空格，尝试均匀分割
                        int mid = buttonText.Length / 2;
                        Widgets.Label(line1Rect, buttonText.Substring(0, mid));
                        Widgets.Label(line2Rect, buttonText.Substring(mid));
                    }
                }
                else
                {
                    // 对于中文等无空格文字，尝试按字符数分割
                    int mid = buttonText.Length / 2;
                    Widgets.Label(line1Rect, buttonText.Substring(0, mid));
                    Widgets.Label(line2Rect, buttonText.Substring(mid));
                }
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // 按钮点击
            if (buttonClicked)
            {
                OnDockButtonClicked(flyover);
            }

            // 鼠标悬停提示
            if (Mouse.IsOver(rect))
            {
                string tooltip = flyover.CanDeploy() 
                    ? "DD_Flyover_Dock_Tooltip".Translate(flyover.DisplayName)
                    : "DD_Flyover_Deploy_Tooltip".Translate(flyover.DisplayName);
                TooltipHandler.TipRegion(rect, tooltip);
            }
        }

        /// <summary>
        /// 绘制标题栏
        /// </summary>
        private void DrawTitleBar(Rect rect)
        {
            // 绘制标题栏背景
            Widgets.DrawMenuSection(rect);
            
            // 绘制标题文字
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            Widgets.Label(rect, "DD_Flyover_Title".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            // 绘制分隔线
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        }

        /// <summary>
        /// 绘制缩小状态的窗口
        /// </summary>
        private void DrawMinimizedWindow(Rect inRect)
        {
            // 绘制缩小窗口背景
            Widgets.DrawWindowBackground(inRect);
            
            // 绘制图标
            if (minimizedIcon != null)
            {
                Rect iconRect = new Rect(
                    inRect.x + 5f,
                    inRect.y + 5f,
                    inRect.width - 10f,
                    inRect.height - 10f
                );

                GUI.color = new Color(1f, 1f, 1f, minimizedOpacity);
                GUI.DrawTexture(iconRect, minimizedIcon);
                GUI.color = Color.white;
            }
            else
            {
                // 显示默认文本
                Rect labelRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, minimizedOpacity);
                Text.Font = GameFont.Medium;
                Widgets.Label(labelRect, "AC");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 点击展开窗口
            if (Widgets.ButtonInvisible(inRect))
            {
                ToggleMinimize();
            }
        }

        /// <summary>
        /// 切换缩小/展开状态
        /// </summary>
        private void ToggleMinimize()
        {
            isMinimized = !isMinimized;
            
            // 强制重新计算窗口大小
            if (isMinimized)
            {
                // 保存当前位置
                minimizedSize = windowRect.size;
                
                // 缩小窗口
                windowRect.size = new Vector2(60f, 60f);
            }
            else
            {
                // 恢复窗口大小
                windowRect.size = new Vector2(windowWidth, windowHeight);
            }
            
            // 确保窗口在屏幕内
            EnsureWindowOnScreen();
        }

        /// <summary>
        /// 获取状态简短描述
        /// </summary>
        private string GetStatusShortDescription(FlyoverStatus status)
        {
            switch (status)
            {
                case FlyoverStatus.OnMap:
                    return "DD_Flyover_Status_OnMap".Translate();
                case FlyoverStatus.Standby:
                    return "DD_Flyover_Status_Standby".Translate();
                case FlyoverStatus.Deploying:
                    return "DD_Flyover_Status_Deploying".Translate();
                case FlyoverStatus.Destroyed:
                    return "DD_Flyover_Status_Destroyed".Translate();
                default:
                    return "DD_Flyover_Status_Unknown".Translate();
            }
        }

        /// <summary>
        /// 获取技能组件
        /// </summary>
        private CompFlyOverSkillBase GetSkillComp(FlyoverData flyover, int slotIndex)
        {
            var flyOver = flyover.linkedFlyover;
            if (flyOver == null) return null;

            var managedComp = flyOver.GetComp<CompFlyoverManaged>();
            if (managedComp == null) return null;

            var skills = managedComp.SkillComps;
            foreach (var skill in skills)
            {
                var props = skill.SkillProps;
                if (props != null && props.slotIndex == slotIndex)
                {
                    return skill;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取技能提示信息
        /// </summary>
        private string GetSkillTooltip(FlyoverData flyover, int slotIndex)
        {
            var skillComp = GetSkillComp(flyover, slotIndex);
            if (skillComp == null)
            {
                return $"DD_Flyover_SkillSlotEmpty".Translate(slotIndex + 1);
            }

            var props = skillComp.SkillProps;
            if (props == null) return $"DD_Flyover_SkillSlot".Translate(slotIndex + 1);

            string tooltip = $"{props.skillName}\n";
            tooltip += $"{props.description}\n\n";

            if (!skillComp.CanUseNow(out string reason))
            {
                tooltip += $"<color=red>{reason}</color>\n";
            }
            else
            {
                tooltip += $"<color=green>{"DD_FlyoverSkillReady".Translate()}</color>\n";
            }

            if (props.cooldownTicks > 0)
            {
                tooltip += $"{"DD_FlyoverSkillCooldown".Translate(props.cooldownTicks / 60f)}\n";
            }

            tooltip += $"{"DD_Flyover_Uses".Translate()}: {skillComp.useCount}";
            if (props.maxUses > 0)
            {
                tooltip += $"/{props.maxUses}";
            }

            return tooltip;
        }

        /// <summary>
        /// 技能点击事件
        /// </summary>
        private void OnSkillClicked(CompFlyOverSkillBase skillComp)
        {
            if (skillComp == null) return;

            if (skillComp.CanUseNow(out string reason))
            {
                skillComp.Activate();
            }
            else
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// 停靠按钮点击事件
        /// </summary>
        private void OnDockButtonClicked(FlyoverData flyover)
        {
            if (flyover.CanDeploy())
            {
                Messages.Message($"DD_Flyover_Deploying".Translate(flyover.DisplayName), 
                    MessageTypeDefOf.NeutralEvent);
                // 这里添加部署逻辑
            }
            else
            {
                Messages.Message($"DD_Flyover_Docking".Translate(flyover.DisplayName), 
                    MessageTypeDefOf.NeutralEvent);
                // 这里添加停靠逻辑
            }
        }

        /// <summary>
        /// 降落按钮点击事件
        /// </summary>
        private void OnLandButtonClicked(FlyoverData flyover)
        {
            Messages.Message($"DD_Flyover_Landing".Translate(flyover.DisplayName), 
                MessageTypeDefOf.NeutralEvent);
            // TODO: 实现降落逻辑
        }

        /// <summary>
        /// 重新入场按钮点击事件 - 现在只是设置待处理状态
        /// </summary>
        private void OnReEnterButtonClicked(FlyoverData flyover)
        {
            // 检查是否有地图
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("DD_Flyover_NoMap".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // 开始两点选择流程（参考CompFlyOverGenerator）
            StartReEnterSelection(flyover, currentMap);
        }

        /// <summary>
        /// 开始重新入场选择流程
        /// </summary>
        private void StartReEnterSelection(FlyoverData flyover, Map map)
        {
            // 保存当前战机数据以便在回调中使用
            this.currentReEnterFlyover = flyover;
            this.currentReEnterMap = map;

            // 不要关闭窗口，保持打开状态以便可以看到选择过程

            // 显示提示消息
            Messages.Message("DD_Flyover_SelectFirstPoint".Translate(), MessageTypeDefOf.SilentInput);

            // 开始选择第一个点
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate(LocalTargetInfo target)
                {
                    OnFirstPointSelected(target.Cell);
                },
                highlightAction: null,
                null,
                null
            );
        }

        // 用于重新入场选择的临时变量
        private FlyoverData currentReEnterFlyover;
        private Map currentReEnterMap;
        private IntVec3 firstPoint;
        public bool selectingSecondPoint = false;

        /// <summary>
        /// 第一个点选择回调
        /// </summary>
        private void OnFirstPointSelected(IntVec3 cell)
        {
            Log.Message($"Window_FlyoverUI: 第一个点选择回调被调用，cell={cell}");
            
            if (!cell.InBounds(currentReEnterMap))
            {
                Messages.Message("DD_Flyover_PointOutOfBounds".Translate(), MessageTypeDefOf.RejectInput);
                ResetReEnterSelection();
                return;
            }

            firstPoint = cell;
            selectingSecondPoint = true;

            // 显示提示消息
            Messages.Message("DD_Flyover_SelectSecondPoint".Translate(), MessageTypeDefOf.SilentInput);

            // 开始选择第二个点
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate(LocalTargetInfo target)
                {
                    OnSecondPointSelected(target.Cell);
                },
                highlightAction: null,
                null,
                null
            );
        }

        /// <summary>
        /// 第二个点选择回调
        /// </summary>
        private void OnSecondPointSelected(IntVec3 cell)
        {
            Log.Message($"Window_FlyoverUI: 第二个点选择回调被调用，cell={cell}");
            
            if (!cell.InBounds(currentReEnterMap))
            {
                Messages.Message("DD_Flyover_PointOutOfBounds".Translate(), MessageTypeDefOf.RejectInput);
                ResetReEnterSelection();
                return;
            }

            if (cell == firstPoint)
            {
                Messages.Message("DD_Flyover_PointsSame".Translate(), MessageTypeDefOf.RejectInput);
                ResetReEnterSelection();
                return;
            }

            // 计算延长线与地图边界的交点
            CalculateAndCreateFlyOver(firstPoint, cell);
        }

        /// <summary>
        /// 计算延长线并与地图边界相交，然后创建FlyOver
        /// </summary>
        private void CalculateAndCreateFlyOver(IntVec3 point1, IntVec3 point2)
        {
            Log.Message($"Window_FlyoverUI: 开始计算和创建FlyOver，point1={point1}, point2={point2}");
            if (currentReEnterMap == null || currentReEnterFlyover == null)
            {
                Log.Error($"Window_FlyoverUI: 重新入场数据无效 - currentReEnterMap: {currentReEnterMap}, currentReEnterFlyover: {currentReEnterFlyover}");
                ResetReEnterSelection();
                return;
            }

            // 计算延长线与地图边界的交点
            IntVec3 entryPoint, exitPoint;
            if (!FlyOver.CalculateMapIntersections(point1, point2, currentReEnterMap, out entryPoint, out exitPoint))
            {
                Messages.Message("DD_Flyover_FailedCalculatePath".Translate(), MessageTypeDefOf.RejectInput);
                ResetReEnterSelection();
                return;
            }

            // 确定起始点和终点（更靠近第一个点的为起始点）
            float distance1 = point1.DistanceTo(entryPoint);
            float distance2 = point1.DistanceTo(exitPoint);
            IntVec3 startPoint = distance1 < distance2 ? entryPoint : exitPoint;
            IntVec3 endPoint = distance1 < distance2 ? exitPoint : entryPoint;

            // 从FlyoverData中获取飞行速度和高度
            float flightSpeed = currentReEnterFlyover.flightSpeed > 0 ? currentReEnterFlyover.flightSpeed : 1f;
            float altitude = currentReEnterFlyover.altitude > 0 ? currentReEnterFlyover.altitude : 10f;

            Log.Message($"Window_FlyoverUI: 创建FlyOver - 速度={flightSpeed}, 高度={altitude}, 起点={startPoint}, 终点={endPoint}");
            // 使用新的重新入场专用方法创建FlyOver
            FlyOver newFlyOver = FlyOver.MakeFlyOverForReEnter(
                currentReEnterFlyover.flyoverDef,
                currentReEnterFlyover.guid,  // 传递现有FlyoverData的guid
                startPoint,
                endPoint,
                currentReEnterMap,
                flightSpeed,
                altitude
            );
            if (newFlyOver != null)
            {
                Log.Message($"Window_FlyoverUI: FlyOver创建成功，ID={newFlyOver.ThingID}");
                // 更新FlyoverData的状态（现在应该已经通过CompFlyoverManaged自动关联）
                if (currentReEnterFlyover != null)
                {
                    currentReEnterFlyover.status = FlyoverStatus.OnMap;
                    currentReEnterFlyover.currentMapIndex = currentReEnterMap.Index;
                    currentReEnterFlyover.startPosition = startPoint;
                    currentReEnterFlyover.endPosition = endPoint;
                    currentReEnterFlyover.flightProgress = 0f;

                    // 创建航线信息
                    currentReEnterFlyover.CreateDefaultPathInfo(point1, point2);

                    Messages.Message($"DD_Flyover_ReEnterSuccess".Translate(currentReEnterFlyover.DisplayName),
                        MessageTypeDefOf.PositiveEvent);
                }
            }
            else
            {
                Log.Error("Window_FlyoverUI: FlyOver创建失败");
                Messages.Message("DD_Flyover_FailedCreate".Translate(), MessageTypeDefOf.NegativeEvent);
            }

            ResetReEnterSelection();
        }

        /// <summary>
        /// 重置重新入场选择状态
        /// </summary>
        private void ResetReEnterSelection()
        {
            currentReEnterFlyover = null;
            currentReEnterMap = null;
            firstPoint = IntVec3.Invalid;
            selectingSecondPoint = false;
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Color GetStatusColor(FlyoverStatus status)
        {
            if (statusColors.ContainsKey(status))
                return statusColors[status];
            
            return Color.gray;
        }

        /// <summary>
        /// 确保窗口在屏幕内
        /// </summary>
        private void EnsureWindowOnScreen()
        {
            Rect rect = windowRect;
            rect.x = Mathf.Clamp(rect.x, 0f, UI.screenWidth - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0f, UI.screenHeight - rect.height);
            
            // 确保窗口大小正确
            rect.size = isMinimized ? minimizedSize : new Vector2(windowWidth, windowHeight);
            
            windowRect = rect;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            EnsureWindowOnScreen();
        }

        /// <summary>
        /// 当窗口打开时调用
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();
            
            // 确保窗口大小和位置正确
            if (!isMinimized)
            {
                windowRect.size = new Vector2(windowWidth, windowHeight);
            }
            
            EnsureWindowOnScreen();
        }

        /// <summary>
        /// 当窗口关闭时调用
        /// </summary>
        public override void PreClose()
        {
            base.PreClose();
            ResetReEnterSelection();
            pendingReEnterFlyover = null;
        }
    }
}
