using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机UI窗口 - 支持缩小/展开功能
    /// </summary>
    public class Window_FlyoverUI : Window
    {
        private WorldComp_FlyoverManager manager;
        private Vector2 scrollPosition = Vector2.zero;
        private float windowHeight = 500f;
        private float itemHeight = 100f;
        private float margin = 10f;

        // 新增：窗口缩小状态
        private bool isMinimized = false;
        private Rect minimizedRect;
        private Vector2 minimizedSize = new Vector2(60f, 60f);
        private float minimizedOpacity = 0.7f;

        // 新增：缩放动画相关
        private float animationProgress = 1f;
        private bool isAnimating = false;
        private float animationSpeed = 8f;

        // 新增：技能图标尺寸
        private const float SKILL_ICON_SIZE = 32f;
        private const float SKILL_SPACING = 8f;
        private const int MAX_SKILLS_PER_ROW = 4;

        public Window_FlyoverUI(WorldComp_FlyoverManager manager)
        {
            this.manager = manager;
            this.draggable = true;
            this.doCloseX = true;
            this.doWindowBackground = true;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;

            // 设置窗口层级
            this.layer = WindowLayer.SubSuper;

            // 初始化窗口位置和大小
            this.windowRect = new Rect(
                UI.screenWidth - 450f - 20f,
                100f,
                450f,
                windowHeight
            );

            // 初始化缩小后的矩形
            minimizedRect = new Rect(
                windowRect.x,
                windowRect.y,
                minimizedSize.x,
                minimizedSize.y
            );
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 根据状态选择绘制内容
                if (isMinimized)
                {
                    DrawMinimizedWindow(inRect);
                }
                else
                {
                    DrawExpandedWindow(inRect);
                }

                // 处理缩放动画
                if (isAnimating)
                {
                    UpdateAnimation();
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in FlyoverUI: {ex}");
            }
        }

        /// <summary>
        /// 绘制展开状态的窗口
        /// </summary>
        private void DrawExpandedWindow(Rect inRect)
        {
            // 绘制背景
            Widgets.DrawWindowBackground(inRect);

            // 标题栏区域
            Rect titleArea = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            DrawTitleBar(titleArea);

            // 内容区域（减去标题栏高度）
            Rect contentRect = new Rect(inRect.x, titleArea.yMax, inRect.width, inRect.height - titleArea.height);

            // 获取要显示的战机数据
            List<FlyoverData> activeFlyovers = manager.ActiveFlyoverData;

            if (activeFlyovers.Count == 0)
            {
                // 显示无战机的提示
                Rect messageRect = new Rect(0f, 50f, inRect.width, 50f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(messageRect, "No aircraft available");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 计算总内容高度
            float totalItemHeight = (itemHeight + margin) * activeFlyovers.Count;
            float contentHeight = Mathf.Max(contentRect.height, totalItemHeight);

            // 滚动区域
            Rect scrollRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, totalItemHeight);

            // 开始滚动视图
            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);

            float yPos = 0f;
            foreach (var flyover in activeFlyovers)
            {
                DrawFlyoverItemDetailed(new Rect(0f, yPos, viewRect.width, itemHeight), flyover);
                yPos += itemHeight + margin;
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 绘制详细的战机项
        /// </summary>
        private void DrawFlyoverItemDetailed(Rect rect, FlyoverData flyover)
        {
            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Widgets.DrawBox(rect);

            // 内边距
            rect = rect.ContractedBy(5f);

            // 左侧：状态指示器和图标区域
            Rect leftSection = new Rect(rect.x, rect.y, 80f, rect.height);
            DrawLeftSection(leftSection, flyover);

            // 中间：信息区域
            Rect middleSection = new Rect(leftSection.xMax + 5f, rect.y, rect.width - leftSection.width - 85f, rect.height);
            DrawMiddleSection(middleSection, flyover);

            // 右侧：操作按钮区域
            Rect rightSection = new Rect(middleSection.xMax + 5f, rect.y, 70f, rect.height);
            DrawRightSection(rightSection, flyover);
        }

        /// <summary>
        /// 绘制左侧区域（状态和图标）
        /// </summary>
        private void DrawLeftSection(Rect rect, FlyoverData flyover)
        {
            // 状态指示器（顶部小条）
            Rect statusRect = new Rect(rect.x, rect.y, rect.width, 5f);
            Widgets.DrawBoxSolid(statusRect, GetStatusColor(flyover.status));

            // 战机图标
            Texture2D icon = flyover.DisplayIcon;
            if (icon != null)
            {
                float iconSize = Mathf.Min(rect.width - 10f, 50f);
                Rect iconRect = new Rect(
                    rect.x + (rect.width - iconSize) / 2f,
                    rect.y + 10f,
                    iconSize,
                    iconSize
                );

                GUI.DrawTexture(iconRect, icon);

                // 图标边框
                Widgets.DrawBox(iconRect, 1);

                // 状态文本（图标下方）
                Rect statusTextRect = new Rect(
                    rect.x,
                    iconRect.yMax + 5f,
                    rect.width,
                    20f
                );

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = GetStatusColor(flyover.status);
                Widgets.Label(statusTextRect, flyover.StatusDescription.Substring(0, 1));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        /// <summary>
        /// 绘制中间区域（信息和技能）
        /// </summary>
        private void DrawMiddleSection(Rect rect, FlyoverData flyover)
        {
            // 名称区域
            Rect nameRect = new Rect(rect.x, rect.y, rect.width, 25f);
            Text.Font = GameFont.Small;
            Widgets.Label(nameRect, flyover.DisplayName);

            // 技能区域
            Rect skillAreaRect = new Rect(
                rect.x,
                nameRect.yMax + 5f,
                rect.width,
                rect.height - nameRect.height - 10f
            );

            DrawSkillSlots(skillAreaRect, flyover);
        }

        /// <summary>
        /// 绘制技能槽
        /// </summary>
        private void DrawSkillSlots(Rect rect, FlyoverData flyover)
        {
            float totalWidth = (SKILL_ICON_SIZE + SKILL_SPACING) * MAX_SKILLS_PER_ROW - SKILL_SPACING;
            float startX = rect.x + (rect.width - totalWidth) / 2f;
            float startY = rect.y + (rect.height - SKILL_ICON_SIZE) / 2f;

            for (int i = 0; i < MAX_SKILLS_PER_ROW; i++)
            {
                Rect skillRect = new Rect(
                    startX + i * (SKILL_ICON_SIZE + SKILL_SPACING),
                    startY,
                    SKILL_ICON_SIZE,
                    SKILL_ICON_SIZE
                );

                // 技能槽背景
                Widgets.DrawBoxSolid(skillRect, new Color(0.3f, 0.3f, 0.3f, 0.8f));
                Widgets.DrawBox(skillRect, 1);

                // 检查是否有技能
                if (flyover.skillSlots != null && i < flyover.skillSlots.Count)
                {
                    var skillSlot = flyover.skillSlots[i];

                    if (!skillSlot.isEmpty)
                    {
                        // 获取技能组件（需要通过FlyOver获取）
                        CompFlyOverSkillBase skillComp = GetSkillComp(flyover, i);

                        if (skillComp != null)
                        {
                            // 检查技能是否可用
                            bool canUse = skillComp.CanUseNow(out string reason);
                            Color skillColor = canUse ? new Color(0.2f, 0.4f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);

                            // 绘制技能背景色
                            Widgets.DrawBoxSolid(skillRect, skillColor);

                            // 绘制冷却覆盖层
                            if (skillComp.CooldownPercent > 0.01f)
                            {
                                float cooldownHeight = skillRect.height * skillComp.CooldownPercent;
                                Rect cooldownRect = new Rect(
                                    skillRect.x,
                                    skillRect.y,
                                    skillRect.width,
                                    cooldownHeight
                                );
                                Widgets.DrawBoxSolid(cooldownRect, new Color(0f, 0f, 0f, 0.6f));
                            }

                            // 绘制技能图标（如果有）
                            Texture2D skillIcon = skillComp.GetSkillIcon();
                            if (skillIcon != null && skillIcon != BaseContent.BadTex)
                            {
                                GUI.DrawTexture(skillRect, skillIcon);
                            }

                            // 技能点击
                            if (Widgets.ButtonInvisible(skillRect))
                            {
                                OnSkillClicked(skillComp);
                            }
                        }
                    }

                    // 技能槽编号（小字显示在角落）
                    Rect numberRect = new Rect(skillRect.x, skillRect.y, 12f, 12f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                    Widgets.Label(numberRect, (i + 1).ToString());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }

                // 鼠标悬停提示
                if (Mouse.IsOver(skillRect))
                {
                    TooltipHandler.TipRegion(skillRect, GetSkillTooltip(flyover, i));
                }
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
                return $"Skill Slot {slotIndex + 1}\nEmpty";
            }

            var props = skillComp.SkillProps;
            if (props == null) return $"Skill Slot {slotIndex + 1}";

            string tooltip = $"{props.skillName}\n";
            tooltip += $"{props.description}\n\n";

            if (!skillComp.CanUseNow(out string reason))
            {
                tooltip += $"<color=red>{reason}</color>\n";
            }
            else
            {
                tooltip += $"<color=green>Ready to use</color>\n";
            }

            if (props.cooldownTicks > 0)
            {
                tooltip += $"Cooldown: {(props.cooldownTicks / 60f):F1}s\n";
            }

            tooltip += $"Uses: {skillComp.useCount}";
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
        /// 绘制右侧区域（操作按钮）
        /// </summary>
        private void DrawRightSection(Rect rect, FlyoverData flyover)
        {
            // 部署按钮
            Rect deployButtonRect = new Rect(
                rect.x,
                rect.y + (rect.height - 30f) / 2f,
                rect.width,
                30f
            );

            string buttonLabel = "Deploy";
            Color buttonColor = flyover.CanDeploy() ?
                new Color(0.2f, 0.6f, 0.2f, 0.9f) :
                new Color(0.5f, 0.5f, 0.5f, 0.5f);

            if (Widgets.ButtonText(deployButtonRect, buttonLabel))
            {
                OnDeployClicked(flyover);
            }

            // 按钮提示
            if (!flyover.CanDeploy() && Mouse.IsOver(deployButtonRect))
            {
                TooltipHandler.TipRegion(deployButtonRect, $"Cannot deploy: {flyover.StatusDescription}");
            }
        }

        /// <summary>
        /// 绘制缩小状态的窗口
        /// </summary>
        private void DrawMinimizedWindow(Rect inRect)
        {
            // 绘制背景
            Widgets.DrawWindowBackground(inRect);

            // 绘制图标
            Texture2D icon = null;
            var flyovers = manager.ActiveFlyoverData;

            if (flyovers.Count > 0)
            {
                // 使用第一个战机的图标
                icon = flyovers[0].DisplayIcon;
            }

            if (icon != null)
            {
                Rect iconRect = new Rect(
                    inRect.x + 5f,
                    inRect.y + 5f,
                    inRect.width - 10f,
                    inRect.height - 10f
                );

                GUI.color = new Color(1f, 1f, 1f, minimizedOpacity);
                GUI.DrawTexture(iconRect, icon);
                GUI.color = Color.white;

                // 显示战机数量（如果有多架）
                if (flyovers.Count > 1)
                {
                    Rect countRect = new Rect(
                        iconRect.xMax - 20f,
                        iconRect.yMax - 15f,
                        18f,
                        12f
                    );

                    Widgets.DrawBoxSolid(countRect, new Color(0f, 0f, 0f, 0.7f));

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.white;
                    Widgets.Label(countRect, flyovers.Count.ToString());
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
            }
            else
            {
                // 显示默认文本
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                Widgets.Label(inRect, "AC");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 点击图标展开窗口
            if (Widgets.ButtonInvisible(inRect))
            {
                ToggleMinimize();
            }
        }

        /// <summary>
        /// 绘制标题栏
        /// </summary>
        private void DrawTitleBar(Rect rect)
        {
            // 标题
            Rect titleRect = new Rect(rect.x + 10f, rect.y + 5f, rect.width - 100f, 20f);
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            Widgets.Label(titleRect, "Aircraft Manager");
            GUI.color = Color.white;

            // 数量显示
            int count = manager.ActiveFlyoverData.Count;
            Rect countRect = new Rect(titleRect.xMax + 5f, rect.y + 5f, 40f, 20f);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.green;
            Widgets.Label(countRect, $"({count})");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 缩小按钮
            Rect minimizeButtonRect = new Rect(rect.xMax - 25f, rect.y + 5f, 20f, 20f);
            string minimizeLabel = isMinimized ? "+" : "_";

            if (Widgets.ButtonText(minimizeButtonRect, minimizeLabel))
            {
                ToggleMinimize();
            }

            // 分隔线
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
        }

        /// <summary>
        /// 切换缩小/展开状态
        /// </summary>
        private void ToggleMinimize()
        {
            if (isAnimating) return;

            isAnimating = true;
            animationProgress = 0f;

            // 切换状态
            isMinimized = !isMinimized;

            // 更新窗口属性
            if (isMinimized)
            {
                // 保存当前位置和大小
                minimizedRect.position = windowRect.position;

                // 缩小窗口
                windowRect.size = minimizedSize;
                absorbInputAroundWindow = true;
            }
            else
            {
                // 恢复窗口大小
                windowRect = new Rect(
                    minimizedRect.position,
                    new Vector2(450f, windowHeight)
                );
                absorbInputAroundWindow = false;
            }
        }

        /// <summary>
        /// 更新动画
        /// </summary>
        private void UpdateAnimation()
        {
            animationProgress += Time.deltaTime * animationSpeed;

            if (animationProgress >= 1f)
            {
                animationProgress = 1f;
                isAnimating = false;
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        private Color GetStatusColor(FlyoverStatus status)
        {
            switch (status)
            {
                case FlyoverStatus.OnMap:
                    return new Color(0.2f, 0.8f, 0.2f, 0.9f);
                case FlyoverStatus.Standby:
                    return new Color(1f, 0.8f, 0.2f, 0.9f);
                case FlyoverStatus.Deploying:
                    return new Color(0.2f, 0.5f, 1f, 0.9f);
                case FlyoverStatus.Destroyed:
                    return new Color(0.8f, 0.2f, 0.2f, 0.5f);
                default:
                    return Color.gray;
            }
        }

        /// <summary>
        /// 部署按钮点击事件
        /// </summary>
        private void OnDeployClicked(FlyoverData flyover)
        {
            if (flyover.CanDeploy())
            {
                Messages.Message($"Deploying {flyover.DisplayName}...",
                    MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message($"Cannot deploy {flyover.DisplayName}",
                    MessageTypeDefOf.RejectInput);
            }
        }

        /// <summary>
        /// 窗口更新，确保在屏幕内
        /// </summary>
        public override void WindowUpdate()
        {
            base.WindowUpdate();

            // 确保窗口在屏幕内
            Rect rect = windowRect;
            rect.x = Mathf.Clamp(rect.x, 0f, UI.screenWidth - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0f, UI.screenHeight - rect.height);
            windowRect = rect;

            // 更新缩小窗口位置
            if (isMinimized)
            {
                minimizedRect.position = windowRect.position;
            }
        }
    }
}