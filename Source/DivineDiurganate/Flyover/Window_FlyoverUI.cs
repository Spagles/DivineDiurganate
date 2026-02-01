using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机UI窗口 - 支持展开/最小化切换的单一实例窗口
    /// </summary>
    public class Window_FlyoverUI : Window
    {
        private WorldComp_FlyoverManager manager;
        private Vector2 scrollPosition = Vector2.zero;
        private float expandedHeight = 300f; // 展开高度
        private float expandedWidth = 500f; // 展开宽度

        // 布局参数 - 调整为更紧凑
        private float itemHeight = 105f;    // 从120px减少到105px
        private float itemMargin = 10f;     // 保持10px间距
        private float itemWidthPercent = 0.9f; // Item宽度占窗口的90%
        // 状态条颜色（基于状态）
        private Dictionary<FlyoverStatus, Color> statusColors = new Dictionary<FlyoverStatus, Color>()
        {
            { FlyoverStatus.OnMap, Color.green },
            { FlyoverStatus.Standby, Color.yellow },
            { FlyoverStatus.Deploying, Color.blue },
            { FlyoverStatus.Destroyed, Color.gray }
        };

        // 自定义背景相关
        private static readonly Texture2D CustomBackground = ContentFinder<Texture2D>.Get("DivineDiurganate/Event/DD_Airship_Manager_Bg", false);
        private static readonly Texture2D MinimizedIcon = ContentFinder<Texture2D>.Get("DivineDiurganate/UI/Commands/DD_AirShip_Manager_Icon", false);

        public override Vector2 InitialSize => new Vector2(expandedWidth, expandedHeight);

        public Window_FlyoverUI(WorldComp_FlyoverManager manager, Vector2? position = null)
        {
            this.manager = manager;
            this.draggable = true;
            this.doCloseX = false;
            this.doWindowBackground = false; // 关闭默认背景绘制
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.Dialog;
            this.resizeable = false;
            this.closeOnCancel = false;   // 不响应取消键
            this.closeOnAccept = false;      // 关闭阴影
            this.drawShadow = false;      // 关闭阴影

            // 设置窗口初始位置
            float initialX, initialY;
            if (position.HasValue)
            {
                initialX = position.Value.x;
                initialY = position.Value.y;
            }
            else
            {
                initialX = UI.screenWidth - expandedWidth - 20f;
                initialY = 100f;
            }
            // 确保窗口在屏幕内
            initialX = Mathf.Clamp(initialX, 0f, UI.screenWidth - expandedWidth);
            initialY = Mathf.Clamp(initialY, 0f, UI.screenHeight - expandedHeight);
            this.windowRect = new Rect(initialX, initialY, expandedWidth, expandedHeight);
        }


        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 绘制自定义背景
                GUI.DrawTexture(inRect, CustomBackground);
                // 先检查是否有待处理的重新入场请求
                if (pendingReEnterFlyover != null)
                {
                    HandlePendingReEnter();
                }
                DrawExpandedWindow(inRect);
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in FlyoverUI: {ex}");
            }
        }
        // UI样式 - 调整为更简洁的颜色
        private static readonly Color ItemBackgroundColor = new Color(0.55f, 0.55f, 0.55f, 0.5f); // #8d8d8d
        private static readonly Color ButtonColor = new Color(0.63f, 0.63f, 0.63f, 0.9f); // #a0a0a0
        private static readonly Color ButtonHoverColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color ButtonPressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color SkillSlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        private static readonly Color SkillSlotReadyColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color SkillSlotCooldownColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        // 用于处理按钮点击的临时变量
        private FlyoverData pendingReEnterFlyover;
        /// <summary>
        /// 绘制展开状态的窗口
        /// </summary>
        private void DrawExpandedWindow(Rect inRect)
        {
            // 绘制标题栏 (50px)
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 50f);
            DrawTitleBar(titleRect);
            // 绘制关闭按钮
            Rect closeButtonRect = new Rect(inRect.x + inRect.width - 30f, inRect.y + 10f, 30f, 30f);
            if (Widgets.ButtonImageFitted(closeButtonRect, TexButton.CloseXSmall))
            {
                OnCloseClicked();
                return; // 窗口即将关闭，直接返回
            }
            // 滚动区域 (220px，标题栏下方10px间隔)
            float scrollAreaWidth = expandedWidth;
            float scrollAreaHeight = 220f;
            Rect scrollRect = new Rect(
                inRect.x,
                titleRect.yMax + 10f, // 标题栏下方10px间隔
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
            // 计算滚动内容高度（为每个item添加底部间距）
            float contentHeight = (itemHeight + itemMargin) * activeFlyovers.Count + itemMargin; // 底部加10px间距
            // 滚动视图
            Rect viewRect = new Rect(0f, 0f, scrollAreaWidth - 20f, contentHeight);
            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
            float currentY = 0f;
            for (int i = 0; i < activeFlyovers.Count; i++)
            {
                // 计算Item的宽度（窗口宽度的90%）
                float itemWidth = viewRect.width * itemWidthPercent - 30f;
                // 水平居中
                float itemX = (viewRect.width - itemWidth) / 2f;
                var flyover = activeFlyovers[i];
                Rect itemRect = new Rect(itemX, currentY, itemWidth, itemHeight);
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
            Widgets.DrawBoxSolid(rect, ItemBackgroundColor);
            Widgets.DrawBox(rect, 1);
            // 状态指示器（左侧竖条）
            Rect statusBarRect = new Rect(rect.x, rect.y, 10f, rect.height);
            Widgets.DrawBoxSolid(statusBarRect, GetStatusColor(flyover.status));
            // 图标区域（调整为90px以适应105px的总高度）
            float iconSize = 85f; // 从100px减少到85px
            Rect iconRect = new Rect(
                statusBarRect.xMax + 8f, // 从10px减少到8px
                rect.y + (rect.height - iconSize) / 2,
                iconSize,
                iconSize
            );
            if (flyover.DisplayIcon != null)
            {
                // 绘制图标背景
                GUI.DrawTexture(iconRect, flyover.DisplayIcon);
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, ButtonColor);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, "DD_Flyover_Icon".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 名字和技能区域
            float nameSkillWidth = 300f;
            float nameSkillHeight = 85f; // 从100px减少到85px
            Rect nameSkillRect = new Rect(
                iconRect.xMax + 8f, // 从10px减少到8px
                rect.y + (rect.height - nameSkillHeight) / 2,
                nameSkillWidth,
                nameSkillHeight
            );

            // 名字和状态区域
            Rect nameStatusRect = new Rect(
                nameSkillRect.x,
                nameSkillRect.y,
                nameSkillRect.width,
                25f // 从30px减少到25px
            );
            // 名字（左侧60%）
            float nameWidth = nameStatusRect.width * 0.6f;
            Rect nameRect = new Rect(
                nameStatusRect.x,
                nameStatusRect.y,
                nameWidth,
                nameStatusRect.height
            );
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Widgets.Label(nameRect, flyover.DisplayName.Truncate(nameRect.width - 5f));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            // 状态（右侧30%）
            float statusWidth = nameStatusRect.width * 0.3f;
            // 为状态文本留出10px的右边距
            Rect statusRect = new Rect(
                nameRect.xMax + nameStatusRect.width * 0.1f - 5f, // 稍微向左调整
                nameStatusRect.y,
                statusWidth - 10f, // 减去10px留出右边距
                nameStatusRect.height
            );
            Text.Anchor = TextAnchor.MiddleRight;
            Text.Font = GameFont.Tiny;
            GUI.color = GetStatusColor(flyover.status);
            Widgets.Label(statusRect, GetStatusShortDescription(flyover.status));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            // 技能区域
            Rect skillAreaRect = new Rect(
                nameSkillRect.x,
                nameStatusRect.yMax,
                nameSkillRect.width,
                nameSkillRect.height - nameStatusRect.height
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
            float buttonWidth = areaRect.width / 2f;
            float buttonHeight = areaRect.height;

            // 重新入场按钮（左侧）
            Rect reEnterButtonRect = new Rect(areaRect.x, areaRect.y, buttonWidth, buttonHeight);
            DrawReEnterButton(reEnterButtonRect, flyover);

            // 降落按钮（右侧）
            Rect landButtonRect = new Rect(areaRect.x + buttonWidth, areaRect.y, buttonWidth, buttonHeight);
            DrawLandButton(landButtonRect, flyover);
        }

        /// <summary>
        /// 绘制OnMap状态的按钮（4个技能槽，取消停靠按钮）
        /// </summary>
        private void DrawOnMapButtons(Rect areaRect, FlyoverData flyover)
        {
            float skillSize = areaRect.height;
            float skillSpacing = 0f;
            float totalSkillsWidth = skillSize * 4 + skillSpacing * 3;

            // 如果总宽度超过可用区域，调整大小
            if (totalSkillsWidth > areaRect.width)
            {
                skillSize = (areaRect.width - skillSpacing * 3) / 4f;
                totalSkillsWidth = skillSize * 4 + skillSpacing * 3;
            }

            float skillStartX = areaRect.x + (areaRect.width - totalSkillsWidth) / 2f;
            float skillStartY = areaRect.y;

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

            // 注意：取消了停靠按钮的绘制，但保留逻辑支持
            // 可以通过快捷键或其他方式调用停靠功能
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

            Color buttonBgColor = ButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }

            Widgets.DrawBoxSolid(rect, buttonBgColor);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
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

            Color buttonBgColor = ButtonColor;
            if (Mouse.IsOver(rect))
            {
                buttonBgColor = buttonClicked ? ButtonPressedColor : ButtonHoverColor;
            }

            Widgets.DrawBoxSolid(rect, buttonBgColor);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
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

            // 绘制技能槽背景
            Color slotBgColor = SkillSlotColor;

            if (skillComp != null)
            {
                bool canUse = skillComp.CanUseNow(out string reason);
                slotBgColor = canUse ? SkillSlotReadyColor : SkillSlotCooldownColor;

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

                if (Widgets.ButtonInvisible(rect))
                {
                    OnSkillClicked(skillComp);
                }
            }
            else
            {
                Widgets.DrawBoxSolid(rect, slotBgColor);
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(rect, $"Slot {slotIndex + 1}");
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
        /// 绘制标题栏
        /// </summary>
        private void DrawTitleBar(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            Widgets.Label(rect, "飞行器管理");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void OnCloseClicked()
        {
            Find.WindowStack.TryRemove(this);
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
                case FlyoverStatus.OnMap: return "飞行中";
                case FlyoverStatus.Standby: return "待命";
                case FlyoverStatus.Deploying: return "部署中";
                case FlyoverStatus.Destroyed: return "已摧毁";
                default: return "未知";
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
                return $"技能槽 {slotIndex + 1} (空)";
            }

            var props = skillComp.SkillProps;
            if (props == null) return $"技能槽 {slotIndex + 1}";

            string tooltip = $"{props.skillName}\n";
            tooltip += $"{props.description}\n\n";

            if (!skillComp.CanUseNow(out string reason))
            {
                tooltip += $"<color=red>{reason}</color>\n";
            }
            else
            {
                tooltip += $"<color=green>技能就绪</color>\n";
            }

            if (props.cooldownTicks > 0)
            {
                tooltip += $"冷却时间: {props.cooldownTicks / 60f:F1}秒\n";
            }

            tooltip += $"使用次数: {skillComp.useCount}";
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
        /// 停靠按钮点击事件（保留逻辑支持，但不在UI上显示）
        /// </summary>
        private void OnDockButtonClicked(FlyoverData flyover)
        {
            if (flyover.CanDeploy())
            {
                Messages.Message($"{flyover.DisplayName} 正在部署...",
                    MessageTypeDefOf.NeutralEvent);
                // 这里添加部署逻辑
            }
            else
            {
                Messages.Message($"{flyover.DisplayName} 正在停靠...",
                    MessageTypeDefOf.NeutralEvent);
                // 这里添加停靠逻辑
            }
        }

        /// <summary>
        /// 降落按钮点击事件
        /// </summary>
        private void OnLandButtonClicked(FlyoverData flyover)
        {
            Messages.Message($"{flyover.DisplayName} 正在降落...",
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
                delegate (LocalTargetInfo target)
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
                delegate (LocalTargetInfo target)
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

        public override void PostClose()
        {
            base.PostClose();
            // 通知管理器UI已关闭
            manager.SetUIState(false);
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
