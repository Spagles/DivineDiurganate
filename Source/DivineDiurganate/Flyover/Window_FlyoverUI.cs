using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机UI窗口 - 展开状态
    /// </summary>
    public class Window_FlyoverUI_Expanded : Window
    {
        private WorldComp_FlyoverManager manager;
        private Vector2 scrollPosition = Vector2.zero;
        private float windowHeight = 600f;
        private float windowWidth = 600f;
        
        // 布局参数
        private float itemHeight = 120f;
        private float itemMargin = 10f;

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
        private static readonly Color DockButtonColor = new Color(0.3f, 0.7f, 0.3f, 0.8f);
        private static readonly Color DockButtonHoverColor = new Color(0.4f, 0.8f, 0.4f, 0.9f);
        private static readonly Color DockButtonPressedColor = new Color(0.2f, 0.6f, 0.2f, 0.9f);
        private static readonly Color LandButtonColor = new Color(0.8f, 0.4f, 0.2f, 0.8f);
        private static readonly Color ReEnterButtonColor = new Color(0.4f, 0.2f, 0.8f, 0.8f);

        // 用于处理按钮点击的临时变量
        private FlyoverData pendingReEnterFlyover;

        // 窗口尺寸属性
        public override Vector2 InitialSize => new Vector2(windowWidth, windowHeight);

        public Window_FlyoverUI_Expanded(WorldComp_FlyoverManager manager)
        {
            this.manager = manager;
            this.draggable = true;
            this.doCloseX = false;
            this.doWindowBackground = true;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.Dialog;
            this.resizeable = false;

            // 设置窗口初始位置
            float initialX = UI.screenWidth - windowWidth - 20f;
            this.windowRect = new Rect(initialX, 100f, windowWidth, windowHeight);
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

                DrawExpandedWindow(inRect);
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in FlyoverUI_Expanded: {ex}");
            }
        }

        /// <summary>
        /// 绘制展开状态的窗口
        /// </summary>
        private void DrawExpandedWindow(Rect inRect)
        {
            // 绘制标题栏
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 50f);
            DrawTitleBar(titleRect);

            // 绘制缩小按钮
            Rect minimizeButtonRect = new Rect(inRect.x + inRect.width - 30f, inRect.y + 5f, 25f, 25f);
            if (Widgets.ButtonImageFitted(minimizeButtonRect, TexButton.Minus))
            {
                OnMinimizeClicked();
                return; // 窗口即将关闭，直接返回
            }

            // 滚动区域
            float scrollAreaWidth = windowWidth - 60f;
            float scrollAreaHeight = windowHeight - 80f;
            
            Rect scrollRect = new Rect(
                inRect.x + 30f,
                titleRect.yMax + 15f,
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
            
            // 滚动视图
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

            // 名字和技能区域
            float infoAreaWidth = windowWidth - 60f - 150f - 20f;
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
                DrawStandbyButtons(skillAreaRect, flyover);
            }
            else if (flyover.status == FlyoverStatus.OnMap)
            {
                DrawOnMapButtons(skillAreaRect, flyover);
            }
            else
            {
                DrawOtherStateUI(skillAreaRect, flyover);
            }
        }

        /// <summary>
        /// 绘制Standby状态的按钮（降落和重新入场）
        /// </summary>
        private void DrawStandbyButtons(Rect areaRect, FlyoverData flyover)
        {
            float buttonSize = 63f;
            float buttonSpacing = 5f;
            float totalButtonsWidth = buttonSize * 2 + buttonSpacing;
            float buttonStartX = areaRect.x + (areaRect.width - totalButtonsWidth) / 2f;
            float buttonStartY = areaRect.y + (areaRect.height - buttonSize) / 2f;

            // 降落按钮
            Rect landButtonRect = new Rect(buttonStartX, buttonStartY, buttonSize, buttonSize);
            DrawLandButton(landButtonRect, flyover);

            // 重新入场按钮
            Rect reEnterButtonRect = new Rect(buttonStartX + buttonSize + buttonSpacing, buttonStartY, buttonSize, buttonSize);
            DrawReEnterButton(reEnterButtonRect, flyover);
        }

        /// <summary>
        /// 绘制OnMap状态的按钮（4个技能槽 + 停靠按钮）
        /// </summary>
        private void DrawOnMapButtons(Rect areaRect, FlyoverData flyover)
        {
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

            // 绘制停靠按钮
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
            switch (flyover.status)
            {
                case FlyoverStatus.Deploying:
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.yellow;
                    Widgets.Label(areaRect, "DD_Flyover_Deploying".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;
                    
                case FlyoverStatus.Destroyed:
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                    Widgets.Label(areaRect, "DD_Flyover_Destroyed".Translate());
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    break;
                    
                default:
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
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            Color buttonBgColor = LandButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            Widgets.Label(rect, "DD_Flyover_Land".Translate());
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (buttonClicked)
            {
                Log.Message($"Window_FlyoverUI: 降落按钮点击，战机={flyover.DisplayName}");
                OnLandButtonClicked(flyover);
            }

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
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            Color buttonBgColor = ReEnterButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            Widgets.Label(rect, "DD_Flyover_ReEnter".Translate());
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (buttonClicked)
            {
                Log.Message($"Window_FlyoverUI: 重新入场按钮点击，战机={flyover.DisplayName}");
                pendingReEnterFlyover = flyover;
            }

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
            
            Widgets.DrawBox(rect);
            
            if (skillComp != null)
            {
                bool canUse = skillComp.CanUseNow(out string reason);
                Color skillColor = canUse ? SkillSlotReadyColor : SkillSlotCooldownColor;
                
                Widgets.DrawBoxSolid(rect, skillColor);

                if (skillComp.CooldownPercent > 0.01f)
                {
                    float cooldownHeight = rect.height * skillComp.CooldownPercent;
                    Rect cooldownRect = new Rect(rect.x, rect.y, rect.width, cooldownHeight);
                    Widgets.DrawBoxSolid(cooldownRect, new Color(0f, 0f, 0f, 0.6f));
                }

                Texture2D skillIcon = skillComp.GetSkillIcon();
                if (skillIcon != null && skillIcon != BaseContent.BadTex)
                {
                    GUI.DrawTexture(rect.ContractedBy(2f), skillIcon);
                }

                Rect numberRect = new Rect(rect.x, rect.y, 12f, 12f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                Widgets.Label(numberRect, (slotIndex + 1).ToString());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                if (Widgets.ButtonInvisible(rect))
                {
                    OnSkillClicked(skillComp);
                }
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(rect, $"DD_Flyover_SkillSlot".Translate(slotIndex + 1));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, GetSkillTooltip(flyover, slotIndex));
            }
        }

        /// <summary>
        /// 绘制停靠按钮槽位
        /// </summary>
        private void DrawDockButtonSlot(Rect rect, FlyoverData flyover)
        {
            bool buttonClicked = Widgets.ButtonInvisible(rect);
            
            Color buttonBgColor = DockButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? DockButtonPressedColor : DockButtonHoverColor;
            }
            
            Widgets.DrawBoxSolid(rect, buttonBgColor);
            Widgets.DrawBox(rect);
            
            string buttonText = flyover.CanDeploy() ? "DD_Flyover_Dock".Translate() : "DD_Flyover_Deploy".Translate();
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.white;
            
            if (buttonText.Length <= 3)
            {
                Widgets.Label(rect, buttonText);
            }
            else
            {
                float lineHeight = rect.height / 2f;
                Rect line1Rect = new Rect(rect.x, rect.y, rect.width, lineHeight);
                Rect line2Rect = new Rect(rect.x, rect.y + lineHeight, rect.width, lineHeight);
                
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
                        int mid = buttonText.Length / 2;
                        Widgets.Label(line1Rect, buttonText.Substring(0, mid));
                        Widgets.Label(line2Rect, buttonText.Substring(mid));
                    }
                }
                else
                {
                    int mid = buttonText.Length / 2;
                    Widgets.Label(line1Rect, buttonText.Substring(0, mid));
                    Widgets.Label(line2Rect, buttonText.Substring(mid));
                }
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            if (buttonClicked)
            {
                OnDockButtonClicked(flyover);
            }

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
            Widgets.DrawMenuSection(rect);
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            Widgets.Label(rect, "DD_Flyover_Title".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        }

        /// <summary>
        /// 缩小按钮点击事件
        /// </summary>
        private void OnMinimizeClicked()
        {
            // 获取当前窗口位置和大小
            Rect currentRect = windowRect;
            
            // 关闭当前窗口
            this.Close();
            
            // 打开缩小窗口，传递相同的位置
            Find.WindowStack.Add(new Window_FlyoverUI_Minimized(manager, currentRect.center));
        }

        /// <summary>
        /// 处理待处理的重新入场请求
        /// </summary>
        private void HandlePendingReEnter()
        {
            if (pendingReEnterFlyover == null) return;

            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("DD_Flyover_NoMap".Translate(), MessageTypeDefOf.RejectInput);
                pendingReEnterFlyover = null;
                return;
            }

            Log.Message($"Window_FlyoverUI: 开始重新入场选择流程，战机={pendingReEnterFlyover.DisplayName}");
            StartReEnterSelection(pendingReEnterFlyover, currentMap);
            pendingReEnterFlyover = null;
        }

        /// <summary>
        /// 获取状态简短描述
        /// </summary>
        private string GetStatusShortDescription(FlyoverStatus status)
        {
            switch (status)
            {
                case FlyoverStatus.OnMap: return "DD_Flyover_Status_OnMap".Translate();
                case FlyoverStatus.Standby: return "DD_Flyover_Status_Standby".Translate();
                case FlyoverStatus.Deploying: return "DD_Flyover_Status_Deploying".Translate();
                case FlyoverStatus.Destroyed: return "DD_Flyover_Status_Destroyed".Translate();
                default: return "DD_Flyover_Status_Unknown".Translate();
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
        /// 重新入场按钮点击事件
        /// </summary>
        private void OnReEnterButtonClicked(FlyoverData flyover)
        {
            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                Messages.Message("DD_Flyover_NoMap".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            StartReEnterSelection(flyover, currentMap);
        }

        /// <summary>
        /// 开始重新入场选择流程
        /// </summary>
        private void StartReEnterSelection(FlyoverData flyover, Map map)
        {
            this.currentReEnterFlyover = flyover;
            this.currentReEnterMap = map;

            Messages.Message("DD_Flyover_SelectFirstPoint".Translate(), MessageTypeDefOf.SilentInput);

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

            Messages.Message("DD_Flyover_SelectSecondPoint".Translate(), MessageTypeDefOf.SilentInput);

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
                Log.Error($"Window_FlyoverUI: 重新入场数据无效");
                ResetReEnterSelection();
                return;
            }

            IntVec3 entryPoint, exitPoint;
            if (!FlyOver.CalculateMapIntersections(point1, point2, currentReEnterMap, out entryPoint, out exitPoint))
            {
                Messages.Message("DD_Flyover_FailedCalculatePath".Translate(), MessageTypeDefOf.RejectInput);
                ResetReEnterSelection();
                return;
            }

            float distance1 = point1.DistanceTo(entryPoint);
            float distance2 = point1.DistanceTo(exitPoint);
            IntVec3 startPoint = distance1 < distance2 ? entryPoint : exitPoint;
            IntVec3 endPoint = distance1 < distance2 ? exitPoint : entryPoint;

            float flightSpeed = currentReEnterFlyover.flightSpeed > 0 ? currentReEnterFlyover.flightSpeed : 1f;
            float altitude = currentReEnterFlyover.altitude > 0 ? currentReEnterFlyover.altitude : 10f;

            Log.Message($"Window_FlyoverUI: 创建FlyOver - 速度={flightSpeed}, 高度={altitude}, 起点={startPoint}, 终点={endPoint}");
            
            FlyOver newFlyOver = FlyOver.MakeFlyOverForReEnter(
                currentReEnterFlyover.flyoverDef,
                currentReEnterFlyover.guid,
                startPoint,
                endPoint,
                currentReEnterMap,
                flightSpeed,
                altitude
            );
            
            if (newFlyOver != null)
            {
                Log.Message($"Window_FlyoverUI: FlyOver创建成功");
                
                if (currentReEnterFlyover != null)
                {
                    currentReEnterFlyover.status = FlyoverStatus.OnMap;
                    currentReEnterFlyover.currentMapIndex = currentReEnterMap.Index;
                    currentReEnterFlyover.startPosition = startPoint;
                    currentReEnterFlyover.endPosition = endPoint;
                    currentReEnterFlyover.flightProgress = 0f;
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

        public override void PreClose()
        {
            base.PreClose();
            ResetReEnterSelection();
            pendingReEnterFlyover = null;
        }
    }

    /// <summary>
    /// 战机UI窗口 - 最小化状态（只显示放大按钮）
    /// </summary>
    public class Window_FlyoverUI_Minimized : Window
    {
        private WorldComp_FlyoverManager manager;
        private Vector2 windowSize = new Vector2(60f, 60f);
        private Texture2D minimizedIcon;
        private Vector2 openPosition;

        public override Vector2 InitialSize => windowSize;

        public Window_FlyoverUI_Minimized(WorldComp_FlyoverManager manager, Vector2 openPosition)
        {
            this.manager = manager;
            this.openPosition = openPosition;
            this.draggable = true;
            this.doCloseX = false; // 最小化窗口没有关闭按钮
            this.doWindowBackground = true;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.Dialog;
            this.resizeable = false;

            // 设置窗口位置（基于传入的位置）
            this.windowRect = new Rect(
                openPosition.x - windowSize.x / 2,
                openPosition.y - windowSize.y / 2,
                windowSize.x,
                windowSize.y
            );

            // 加载图标
            minimizedIcon = ContentFinder<Texture2D>.Get("UI/Icons/AircraftIcon", false);
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 绘制窗口背景
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

                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    GUI.DrawTexture(iconRect, minimizedIcon);
                    GUI.color = Color.white;
                }
                else
                {
                    // 显示默认文本
                    Rect labelRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    Text.Font = GameFont.Medium;
                    Widgets.Label(labelRect, "AC");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                }

                // 点击放大窗口
                if (Widgets.ButtonInvisible(inRect))
                {
                    OnMaximizeClicked();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in FlyoverUI_Minimized: {ex}");
            }
        }

        /// <summary>
        /// 放大按钮点击事件
        /// </summary>
        private void OnMaximizeClicked()
        {
            // 获取当前窗口位置
            Vector2 currentCenter = windowRect.center;
            
            // 关闭当前窗口
            this.Close();
            
            // 打开展开窗口，使用当前中心位置
            var expandedWindow = new Window_FlyoverUI_Expanded(manager);
            expandedWindow.windowRect.center = currentCenter;
            Find.WindowStack.Add(expandedWindow);
        }

        /// <summary>
        /// 确保窗口在屏幕内
        /// </summary>
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            EnsureWindowOnScreen();
        }

        private void EnsureWindowOnScreen()
        {
            Rect rect = windowRect;
            rect.x = Mathf.Clamp(rect.x, 0f, UI.screenWidth - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0f, UI.screenHeight - rect.height);
            windowRect = rect;
        }
    }
}
