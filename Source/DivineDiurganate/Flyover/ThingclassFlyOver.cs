using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    [StaticConstructorOnStartup]
    public class FlyOver : ThingWithComps, IThingHolder
    {
        // 核心字段
        public ThingOwner innerContainer;           // 内部物品容器
        public IntVec3 startPosition;              // 起始位置
        public IntVec3 endPosition;                // 结束位置
        public float flightSpeed = 1f;             // 飞行速度
        public float currentProgress = 0f;         // 当前进度 (0-1)
        public float altitude = 10f;               // 飞行高度
        public Faction faction;                     // 派系引用

        // 淡入效果相关
        public float fadeInDuration = 1.5f;        // 淡入持续时间（秒）
        public float currentFadeInTime = 0f;       // 当前淡入时间
        public bool fadeInCompleted = false;       // 淡入是否完成

        // 淡出效果相关 - 修改：取消动态计算，使用固定时间
        public float fadeOutDuration = 0.5f;       // 淡出持续时间（秒）- 固定时间
        public float currentFadeOutTime = 0f;      // 当前淡出时间
        public bool fadeOutStarted = false;        // 淡出是否开始
        public bool fadeOutCompleted = false;      // 淡出是否完成
        public float fadeOutStartProgress = 0.7f;  // 开始淡出的进度阈值（0-1）

        // 新增：淡入淡出开关
        private bool useFadeEffects = true;
        private bool useFadeIn = true;
        private bool useFadeOut = true;

        // 进场动画相关
        public float approachDuration = 1.0f;      // 进场动画持续时间（秒）
        public float currentApproachTime = 0f;     // 当前进场动画时间
        public bool approachCompleted = false;     // 进场动画是否完成
        public float approachOffsetDistance = 3f;  // 进场偏移距离（格）
        public bool useApproachAnimation = true;   // 是否使用进场动画

        // 离场动画相关 - 新增
        public float departureDuration = 1.0f;     // 离场动画持续时间（秒）
        public float currentDepartureTime = 0f;    // 当前离场动画时间
        public bool departureStarted = false;      // 离场动画是否开始
        public bool departureCompleted = false;    // 离场动画是否完成
        public float departureOffsetDistance = 3f; // 离场偏移距离（格）
        public bool useDepartureAnimation = true;  // 是否使用离场动画

        // 新增：时间限制
        public float maxStayTime = 60f;            // 最大停留时间（秒），0表示无限制
        public float currentStayTime = 0f;         // 当前已停留时间（秒）
        public bool timeLimitExceeded = false;     // 是否已超过时间限制

        // 伴飞相关
        public float escortScale = 1f;             // 伴飞缩放比例
        public bool isEscort = false;              // 是否是伴飞

        // 状态标志
        public bool hasStarted = false;
        public bool hasCompleted = false;

        // 音效系统
        private Sustainer flightSoundPlaying;

        // 视觉效果
        private Material cachedShadowMaterial;
        private static MaterialPropertyBlock shadowPropertyBlock = new MaterialPropertyBlock();
        private static MaterialPropertyBlock fadePropertyBlock = new MaterialPropertyBlock();

        // 配置字段
        public bool spawnContentsOnImpact = false; // 是否在结束时生成内容物
        public bool playFlyOverSound = true;       // 是否播放飞越音效
        public bool createShadow = true;           // 是否创建阴影

        public Pawn caster;                       // 施法者引用

        // 属性 - 修改后的 DrawPos，包含进场和离场动画
        public override Vector3 DrawPos
        {
            get
            {
                // 线性插值计算基础位置
                Vector3 start = startPosition.ToVector3();
                Vector3 end = endPosition.ToVector3();
                Vector3 basePos = Vector3.Lerp(start, end, currentProgress);

                // 添加高度偏移
                basePos.y = altitude;

                // 应用进场动画偏移
                if (useApproachAnimation && !approachCompleted && !departureStarted)
                {
                    basePos = ApplyApproachAnimation(basePos);
                }

                // 应用离场动画偏移
                if (useDepartureAnimation && departureStarted && !departureCompleted)
                {
                    basePos = ApplyDepartureAnimation(basePos);
                }

                return basePos;
            }
        }

        // 进场动画位置计算
        private Vector3 ApplyApproachAnimation(Vector3 basePos)
        {
            float approachProgress = currentApproachTime / approachDuration;

            // 使用缓动函数让移动更自然
            float easedProgress = EasingFunction(approachProgress, EasingType.OutCubic);

            // 计算偏移方向（飞行方向的反方向）
            Vector3 approachDirection = -MovementDirection.normalized;

            // 计算偏移量：从最大偏移逐渐减少到0
            float currentOffset = approachOffsetDistance * (1f - easedProgress);

            // 应用偏移
            Vector3 offsetPos = basePos + approachDirection * currentOffset;

            return offsetPos;
        }

        // 离场动画位置计算 - 新增
        private Vector3 ApplyDepartureAnimation(Vector3 basePos)
        {
            float departureProgress = currentDepartureTime / departureDuration;

            // 使用缓动函数让移动更自然
            float easedProgress = EasingFunction(departureProgress, EasingType.OutCubic);

            // 计算偏移方向（飞行方向的正方向）
            Vector3 departureDirection = MovementDirection.normalized;

            // 计算偏移量：从0逐渐增加到最大偏移
            float currentOffset = departureOffsetDistance * easedProgress;

            // 应用偏移
            Vector3 offsetPos = basePos + departureDirection * currentOffset;

            return offsetPos;
        }

        // 缓动函数 - 让动画更自然
        private float EasingFunction(float t, EasingType type)
        {
            switch (type)
            {
                case EasingType.OutCubic:
                    return 1f - Mathf.Pow(1f - t, 3f);
                case EasingType.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case EasingType.OutSine:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);
                default:
                    return t;
            }
        }

        // 缓动类型枚举
        private enum EasingType
        {
            Linear,
            OutQuad,
            OutCubic,
            OutSine
        }

        // 进场动画进度（0-1）
        public float ApproachProgress
        {
            get
            {
                if (approachCompleted) return 1f;
                return Mathf.Clamp01(currentApproachTime / approachDuration);
            }
        }

        // 离场动画进度（0-1）- 新增
        public float DepartureProgress
        {
            get
            {
                if (departureCompleted) return 1f;
                return Mathf.Clamp01(currentDepartureTime / departureDuration);
            }
        }

        public override Graphic Graphic
        {
            get
            {
                Thing thingForGraphic = GetThingForGraphic();
                if (thingForGraphic == this)
                {
                    return base.Graphic;
                }
                return thingForGraphic.Graphic.ExtractInnerGraphicFor(thingForGraphic);
            }
        }

        protected Material ShadowMaterial
        {
            get
            {
                if (cachedShadowMaterial == null && createShadow)
                {
                    cachedShadowMaterial = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent);
                }
                return cachedShadowMaterial;
            }
        }

        // 精确旋转 - 模仿原版 Projectile
        public virtual Quaternion ExactRotation
        {
            get
            {
                Vector3 direction = (endPosition.ToVector3() - startPosition.ToVector3()).normalized;
                return Quaternion.LookRotation(direction.Yto0());
            }
        }

        // 简化的方向计算方法
        public Vector3 MovementDirection
        {
            get
            {
                return (endPosition.ToVector3() - startPosition.ToVector3()).normalized;
            }
        }

        // 修改后的淡入透明度属性
        public float FadeInAlpha
        {
            get
            {
                if (!useFadeIn || fadeInCompleted) return 1f;
                return Mathf.Clamp01(currentFadeInTime / fadeInDuration);
            }
        }

        // 修改后的淡出透明度属性
        public float FadeOutAlpha
        {
            get
            {
                // 离场动画时不使用淡出
                if (departureStarted) return 1f;

                if (!fadeOutStarted) return 1f;
                if (fadeOutCompleted) return 0f;
                return Mathf.Clamp01(1f - (currentFadeOutTime / fadeOutDuration));
            }
        }

        // 修改后的总体透明度属性
        public float OverallAlpha
        {
            get
            {
                if (!useFadeEffects && !fadeOutStarted) return 1f;
                return FadeInAlpha * FadeOutAlpha;
            }
        }

        // 修改后的 Tick 方法，添加时间限制和离场动画处理
        protected override void Tick()
        {
            base.Tick();
            if (!hasStarted || hasCompleted)
                return;

            // 更新时间（用于时间限制）
            currentStayTime += 1f / 60f;

            // 检查时间限制
            if (maxStayTime > 0f && currentStayTime >= maxStayTime && !timeLimitExceeded)
            {
                OnTimeLimitExceeded();
                return;
            }

            // 更新进场动画
            if (useApproachAnimation && !approachCompleted && !departureStarted)
            {
                currentApproachTime += 1f / 60f;
                if (currentApproachTime >= approachDuration)
                {
                    approachCompleted = true;
                    currentApproachTime = approachDuration;
                }
            }

            // 更新淡入效果（仅在启用时）
            if (useFadeIn && !fadeInCompleted && !departureStarted)
            {
                currentFadeInTime += 1f / 60f;
                if (currentFadeInTime >= fadeInDuration)
                {
                    fadeInCompleted = true;
                    currentFadeInTime = fadeInDuration;
                }
            }

            // 更新飞行进度（不在离场动画期间更新进度）
            if (!departureStarted)
            {
                currentProgress += flightSpeed * 0.001f;
            }

            // 检查是否应该开始淡出（仅在启用时且未开始离场动画）
            if (useFadeOut && !fadeOutStarted && !departureStarted &&
                !timeLimitExceeded && currentProgress >= fadeOutStartProgress)
            {
                StartFadeOut();
            }

            // 更新淡出效果（仅在启用时）
            if (useFadeOut && fadeOutStarted && !fadeOutCompleted && !departureStarted)
            {
                currentFadeOutTime += 1f / 60f;
                if (currentFadeOutTime >= fadeOutDuration)
                {
                    fadeOutCompleted = true;
                    currentFadeOutTime = fadeOutDuration;
                    // 淡出完成后立即销毁
                    CompleteFlyOver();
                    return;
                }
            }

            // 更新离场动画
            if (departureStarted && !departureCompleted)
            {
                currentDepartureTime += 1f / 60f;
                if (currentDepartureTime >= departureDuration)
                {
                    departureCompleted = true;
                    currentDepartureTime = departureDuration;
                    // 离场动画完成后销毁
                    CompleteFlyOver();
                    return;
                }
            }

            // 更新当前位置
            UpdatePosition();

            // 维持飞行音效
            UpdateFlightSound();

            // 检查是否到达终点（不在离场动画期间检查）
            if (!departureStarted && currentProgress >= 1f)
            {
                // 到达终点时立即销毁，不使用淡出
                CompleteFlyOver(useFadeOut: false);
                return;
            }

            // 生成飞行轨迹特效
            CreateFlightEffects();
        }

        // 时间限制超时处理 - 新增
        private void OnTimeLimitExceeded()
        {
            timeLimitExceeded = true;

            // 判断使用哪种方式销毁
            if (useDepartureAnimation)
            {
                // 使用离场动画
                StartDepartureAnimation();
            }
            else if (useFadeOut)
            {
                // 使用淡出效果
                StartFadeOut();
            }
            else
            {
                // 立即销毁
                CompleteFlyOver();
            }
        }

        // 开始离场动画 - 新增
        private void StartDepartureAnimation()
        {
            departureStarted = true;

            // 停止淡出效果（如果已经开始）
            if (fadeOutStarted)
            {
                fadeOutStarted = false;
                fadeOutCompleted = false;
                currentFadeOutTime = 0f;
            }

            // 停止淡入效果（如果还没完成）
            if (!fadeInCompleted)
            {
                fadeInCompleted = true;
                currentFadeInTime = fadeInDuration;
            }

            // 设置离场动画持续时间
            departureDuration = GetDepartureDuration();

            // 播放离场音效（如果有）
            PlayDepartureSound();
        }

        // 获取离场动画持续时间 - 新增
        private float GetDepartureDuration()
        {
            var extension = def.GetModExtension<FlyOverShadowExtension>();
            if (extension != null)
            {
                return extension.departureDuration;
            }
            return 1.0f; // 默认1秒
        }

        // 播放离场音效 - 新增
        private void PlayDepartureSound()
        {
            var extension = def.GetModExtension<FlyOverShadowExtension>();
            if (extension != null && extension.departureSound != null)
            {
                extension.departureSound.PlayOneShot(
                    SoundInfo.InMap(new TargetInfo(Position, base.Map)));
            }
        }

        /// <summary>
        /// 完成飞行并销毁
        /// </summary>
        private void CompleteFlyOver(bool useFadeOut = true)
        {
            if (hasCompleted) return;
            hasCompleted = true;
            try
            {
                // 如果是正常到达终点，设置进度为1
                if (!timeLimitExceeded)
                {
                    currentProgress = 1f;
                }
                // 生成内容物（如果需要）
                if (spawnContentsOnImpact && innerContainer.Any)
                {
                    SpawnContents();
                }
                // 播放完成音效（仅在正常到达终点时播放）
                if (!timeLimitExceeded && def.skyfaller?.impactSound != null)
                {
                    def.skyfaller.impactSound.PlayOneShot(
                        SoundInfo.InMap(new TargetInfo(endPosition, base.Map)));
                }
                // 停止音效
                flightSoundPlaying?.End();

                // 直接销毁，不使用延迟，避免显示弹框
                try
                {
                    if (!this.Destroyed)
                    {
                        this.Destroy();
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"Error destroying flyover: {ex}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in CompleteFlyOver: {ex}");
                // 出错时立即销毁
                if (!this.Destroyed)
                {
                    this.Destroy();
                }
            }
        }

        // 修改后的 UpdatePosition 方法
        private void UpdatePosition()
        {
            if (hasCompleted) return;

            Vector3 currentWorldPos = Vector3.Lerp(startPosition.ToVector3(), endPosition.ToVector3(), currentProgress);
            IntVec3 newPos = currentWorldPos.ToIntVec3();
            if (newPos != base.Position && newPos.InBounds(base.Map))
            {
                base.Position = newPos;
            }
        }

        // 修改后的开始淡出效果 - 使用固定时间
        private void StartFadeOut()
        {
            fadeOutStarted = true;
            fadeOutDuration = GetFixedFadeOutDuration();
        }

        // 获取固定淡出持续时间 - 新增
        private float GetFixedFadeOutDuration()
        {
            var extension = def.GetModExtension<FlyOverShadowExtension>();
            if (extension != null)
            {
                return extension.fixedFadeOutDuration;
            }
            return 0.5f; // 默认0.5秒
        }

        public FlyOver()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref startPosition, "startPosition");
            Scribe_Values.Look(ref endPosition, "endPosition");
            Scribe_Values.Look(ref flightSpeed, "flightSpeed", 1f);
            Scribe_Values.Look(ref currentProgress, "currentProgress", 0f);
            Scribe_Values.Look(ref altitude, "altitude", 10f);
            Scribe_Values.Look(ref hasStarted, "hasStarted", false);
            Scribe_Values.Look(ref hasCompleted, "hasCompleted", false);
            Scribe_Values.Look(ref spawnContentsOnImpact, "spawnContentsOnImpact", false);
            Scribe_Values.Look(ref fadeInDuration, "fadeInDuration", 1.5f);
            Scribe_Values.Look(ref currentFadeInTime, "currentFadeInTime", 0f);
            Scribe_Values.Look(ref fadeInCompleted, "fadeInCompleted", false);

            // 淡出效果数据保存
            Scribe_Values.Look(ref fadeOutDuration, "fadeOutDuration", 0.5f);
            Scribe_Values.Look(ref currentFadeOutTime, "currentFadeOutTime", 0f);
            Scribe_Values.Look(ref fadeOutStarted, "fadeOutStarted", false);
            Scribe_Values.Look(ref fadeOutCompleted, "fadeOutCompleted", false);
            Scribe_Values.Look(ref fadeOutStartProgress, "fadeOutStartProgress", 0.7f);

            // 时间限制数据保存 - 新增
            Scribe_Values.Look(ref maxStayTime, "maxStayTime", 0f);
            Scribe_Values.Look(ref currentStayTime, "currentStayTime", 0f);
            Scribe_Values.Look(ref timeLimitExceeded, "timeLimitExceeded", false);

            // 进场动画数据保存
            Scribe_Values.Look(ref approachDuration, "approachDuration", 1.0f);
            Scribe_Values.Look(ref currentApproachTime, "currentApproachTime", 0f);
            Scribe_Values.Look(ref approachCompleted, "approachCompleted", false);
            Scribe_Values.Look(ref approachOffsetDistance, "approachOffsetDistance", 3f);
            Scribe_Values.Look(ref useApproachAnimation, "useApproachAnimation", true);

            // 离场动画数据保存 - 新增
            Scribe_Values.Look(ref departureDuration, "departureDuration", 1.0f);
            Scribe_Values.Look(ref currentDepartureTime, "currentDepartureTime", 0f);
            Scribe_Values.Look(ref departureStarted, "departureStarted", false);
            Scribe_Values.Look(ref departureCompleted, "departureCompleted", false);
            Scribe_Values.Look(ref departureOffsetDistance, "departureOffsetDistance", 3f);
            Scribe_Values.Look(ref useDepartureAnimation, "useDepartureAnimation", true);

            // 淡入淡出开关保存
            Scribe_Values.Look(ref useFadeEffects, "useFadeEffects", true);
            Scribe_Values.Look(ref useFadeIn, "useFadeIn", true);
            Scribe_Values.Look(ref useFadeOut, "useFadeOut", true);
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref faction, "faction");
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

            if (!respawningAfterLoad)
            {
                // 设置初始位置
                base.Position = startPosition;
                hasStarted = true;

                // 从 ModExtension 加载配置
                var extension = def.GetModExtension<FlyOverShadowExtension>();
                if (extension != null)
                {
                    useApproachAnimation = extension.useApproachAnimation;
                    approachDuration = extension.approachDuration;
                    approachOffsetDistance = extension.approachOffsetDistance;

                    // 加载离场动画配置 - 新增
                    useDepartureAnimation = extension.useDepartureAnimation;
                    departureDuration = extension.departureDuration;
                    departureOffsetDistance = extension.departureOffsetDistance;

                    // 加载时间限制配置 - 新增
                    maxStayTime = extension.maxStayTime;

                    // 加载淡入淡出配置
                    useFadeEffects = extension.useFadeEffects;
                    useFadeIn = extension.useFadeIn;
                    useFadeOut = extension.useFadeOut;

                    // 设置淡入持续时间
                    fadeInDuration = extension.defaultFadeInDuration;
                    fadeOutStartProgress = extension.fadeOutStartProgress;
                }

                // 重置淡入状态
                currentFadeInTime = 0f;
                fadeInCompleted = !useFadeIn;

                // 重置淡出状态
                currentFadeOutTime = 0f;
                fadeOutStarted = false;
                fadeOutCompleted = false;

                // 重置进场动画状态
                currentApproachTime = 0f;
                approachCompleted = !useApproachAnimation;

                // 重置离场动画状态 - 新增
                currentDepartureTime = 0f;
                departureStarted = false;
                departureCompleted = false;

                // 重置时间限制状态 - 新增
                currentStayTime = 0f;
                timeLimitExceeded = false;

                // 开始飞行音效
                if (playFlyOverSound && def.skyfaller?.floatingSound != null)
                {
                    flightSoundPlaying = def.skyfaller.floatingSound.TrySpawnSustainer(
                        SoundInfo.InMap(new TargetInfo(startPosition, map), MaintenanceType.PerTick));
                }
            }
        }

        // 修改后的 UpdateFlightSound 方法
        private void UpdateFlightSound()
        {
            if (flightSoundPlaying != null)
            {
                // 离场动画时调整音效
                if (departureStarted && flightSoundPlaying.externalParams != null)
                {
                    // 根据离场进度降低音量
                    float volume = 1f - (currentDepartureTime / departureDuration);
                    flightSoundPlaying.externalParams["VolumeFactor"] = Mathf.Clamp01(volume);
                }
                flightSoundPlaying?.Maintain();
            }
        }

        private void SpawnContents()
        {
            foreach (Thing thing in innerContainer)
            {
                if (thing != null && !thing.Destroyed)
                {
                    GenPlace.TryPlaceThing(thing, endPosition, base.Map, ThingPlaceMode.Near);
                }
            }
            innerContainer.Clear();
        }

        // 修改后的 CreateFlightEffects 方法
        private void CreateFlightEffects()
        {
            // 在飞行轨迹上生成粒子效果
            if (Rand.MTBEventOccurs(0.5f, 1f, 1f) && def.skyfaller?.motesPerCell > 0)
            {
                Vector3 effectPos = DrawPos;
                effectPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

                float effectIntensity = 1f;

                // 离场动画时减少粒子效果
                if (departureStarted)
                {
                    effectIntensity = 1f - (currentDepartureTime / departureDuration);
                }

                FleckMaker.ThrowSmoke(effectPos, base.Map, 1f * effectIntensity);
            }
        }

        // 关键修复：重写 DrawAt 方法，绕过探索状态检查
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // 直接绘制，不检查探索状态
            Vector3 finalDrawPos = drawLoc;

            if (createShadow)
            {
                DrawFlightShadow();
            }

            DrawFlyOverWithFade(finalDrawPos);
        }

        protected virtual void DrawFlyOverWithFade(Vector3 drawPos)
        {
            Thing thingForGraphic = GetThingForGraphic();
            Graphic graphic = thingForGraphic.Graphic;
            if (graphic == null)
                return;
            Material material = graphic.MatSingle;
            if (material == null)
                return;
            float alpha = OverallAlpha;
            if (alpha <= 0.001f)
                return;
            if (fadeInCompleted && !fadeOutStarted && alpha >= 0.999f)
            {
                Vector3 highAltitudePos = drawPos;
                highAltitudePos.y = AltitudeLayer.MetaOverlays.AltitudeFor();

                // 应用伴飞缩放
                Vector3 finalScale = Vector3.one;
                if (def.graphicData != null)
                {
                    finalScale = new Vector3(def.graphicData.drawSize.x * escortScale, 1f, def.graphicData.drawSize.y * escortScale);
                }
                else
                {
                    finalScale = new Vector3(escortScale, 1f, escortScale);
                }

                Matrix4x4 matrix = Matrix4x4.TRS(highAltitudePos, ExactRotation, finalScale);
                Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
                return;
            }
            fadePropertyBlock.SetColor(ShaderPropertyIDs.Color,
                new Color(graphic.Color.r, graphic.Color.g, graphic.Color.b, graphic.Color.a * alpha));

            // 应用伴飞缩放
            Vector3 scale = Vector3.one;
            if (def.graphicData != null)
            {
                scale = new Vector3(def.graphicData.drawSize.x * escortScale, 1f, def.graphicData.drawSize.y * escortScale);
            }
            else
            {
                scale = new Vector3(escortScale, 1f, escortScale);
            }

            Vector3 highPos = drawPos;
            highPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            Matrix4x4 matrix2 = Matrix4x4.TRS(highPos, ExactRotation, scale);
            Graphics.DrawMesh(MeshPool.plane10, matrix2, material, 0, null, 0, fadePropertyBlock);
        }

        protected virtual void DrawFlightShadow()
        {
            var shadowExtension = def.GetModExtension<FlyOverShadowExtension>();

            Material shadowMaterial;
            if (shadowExtension?.useCustomShadow == true && !shadowExtension.customShadowPath.NullOrEmpty())
            {
                shadowMaterial = MaterialPool.MatFrom(shadowExtension.customShadowPath, ShaderDatabase.Transparent);
            }
            else
            {
                shadowMaterial = ShadowMaterial;
            }

            if (shadowMaterial == null)
                return;

            Vector3 shadowPos = DrawPos;
            shadowPos.y = AltitudeLayer.Shadows.AltitudeFor();

            float shadowIntensity = shadowExtension?.shadowIntensity ?? 1f;
            float minAlpha = shadowExtension?.minShadowAlpha ?? 0.3f;
            float maxAlpha = shadowExtension?.maxShadowAlpha ?? 1f;
            float minScale = shadowExtension?.minShadowScale ?? 0.5f;
            float maxScale = shadowExtension?.maxShadowScale ?? 1.5f;

            float shadowAlpha = Mathf.Lerp(minAlpha, maxAlpha, currentProgress) * shadowIntensity;
            float shadowScale = Mathf.Lerp(minScale, maxScale, currentProgress);

            shadowAlpha *= OverallAlpha;

            if (shadowAlpha <= 0.001f)
                return;

            Vector3 s = new Vector3(shadowScale, 1f, shadowScale);
            Vector3 vector = new Vector3(0f, -0.01f, 0f);
            Matrix4x4 matrix = Matrix4x4.TRS(shadowPos + vector, Quaternion.identity, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
        }

        // IThingHolder 接口实现
        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        private Thing GetThingForGraphic()
        {
            if (def.graphicData != null || !innerContainer.Any)
            {
                return this;
            }
            return innerContainer[0];
        }

        // 修改后的 MakeFlyOver 方法，添加新参数
        public static FlyOver MakeFlyOver(ThingDef flyOverDef, IntVec3 start, IntVec3 end, Map map,
            float speed = 1f, float height = 10f, ThingOwner contents = null,
            float fadeInDuration = 1.5f, float defaultFadeOutDuration = 0.5f, Pawn casterPawn = null,
            bool useApproachAnimation = true, float approachDuration = 1.0f, float approachOffsetDistance = 3f,
            bool? useFadeEffects = null, bool? useFadeIn = null, bool? useFadeOut = null,
            bool useDepartureAnimation = true, float departureDuration = 1.0f, float departureOffsetDistance = 3f,
            float maxStayTime = 0f ,Faction faction = null) // 新增参数
        {
            FlyOver flyOver = (FlyOver)ThingMaker.MakeThing(flyOverDef);
            flyOver.startPosition = start;
            flyOver.endPosition = end;
            flyOver.flightSpeed = speed;
            flyOver.altitude = height;
            flyOver.fadeInDuration = fadeInDuration;
            flyOver.caster = casterPawn;

            // 进场动画参数
            flyOver.useApproachAnimation = useApproachAnimation;
            flyOver.approachDuration = approachDuration;
            flyOver.approachOffsetDistance = approachOffsetDistance;

            // 离场动画参数 - 新增
            flyOver.useDepartureAnimation = useDepartureAnimation;
            flyOver.departureDuration = departureDuration;
            flyOver.departureOffsetDistance = departureOffsetDistance;

            // 时间限制参数 - 新增
            flyOver.maxStayTime = maxStayTime;

            // 淡入淡出参数
            if (useFadeEffects.HasValue) flyOver.useFadeEffects = useFadeEffects.Value;
            if (useFadeIn.HasValue) flyOver.useFadeIn = useFadeIn.Value;
            if (useFadeOut.HasValue) flyOver.useFadeOut = useFadeOut.Value;

            // 简化派系设置
            if (faction != null)
            {
                flyOver.faction = faction;
            }
            if (faction == null && casterPawn != null && casterPawn.Faction != null)
            {
                flyOver.faction = casterPawn.Faction;
            }

            if (contents != null)
            {
                flyOver.innerContainer.TryAddRangeOrTransfer(contents);
            }

            GenSpawn.Spawn(flyOver, start, map);
            return flyOver;
        }

        public static FlyOver MakeFlyOverForReEnter(ThingDef flyOverDef, string existingFlyoverDataGuid,
    IntVec3 start, IntVec3 end, Map map, float speed = 1f, float height = 10f)
        {
            FlyOver flyOver = (FlyOver)ThingMaker.MakeThing(flyOverDef);
            flyOver.startPosition = start;
            flyOver.endPosition = end;
            flyOver.flightSpeed = speed;
            flyOver.altitude = height;

            // 设置重新入场标志
            var managedComp = flyOver.GetComp<CompFlyoverManaged>();
            if (managedComp != null)
            {
                // 在Spawn之前设置guid，确保PostSpawnSetup能识别这是重新入场
                managedComp.SetFlyoverDataGuidForReEnter(existingFlyoverDataGuid);
            }

            // 生成到地图
            GenSpawn.Spawn(flyOver, start, map);
            return flyOver;
        }

        /// <summary>
        /// 计算直线与地图边界的交点（公开静态方法）
        /// </summary>
        public static bool CalculateMapIntersections(IntVec3 point1, IntVec3 point2, Map map,
            out IntVec3 intersection1, out IntVec3 intersection2)
        {
            intersection1 = IntVec3.Invalid;
            intersection2 = IntVec3.Invalid;

            if (map == null)
            {
                Log.Warning("Map is null in CalculateMapIntersections");
                return false;
            }

            // 将点转换为Vector3以便计算
            Vector3 p1 = point1.ToVector3();
            Vector3 p2 = point2.ToVector3();

            // 计算方向向量
            Vector3 dir = (p2 - p1).normalized;

            // 地图边界
            float minX = 0f;
            float maxX = map.Size.x - 1;
            float minZ = 0f;
            float maxZ = map.Size.z - 1;

            List<Vector3> intersections = new List<Vector3>();

            // 计算与四条边界的交点
            // 1. 左边界 (x = minX)
            if (Mathf.Abs(dir.x) > 0.001f)
            {
                float tLeft = (minX - p1.x) / dir.x;
                Vector3 intersectLeft = p1 + dir * tLeft;
                if (intersectLeft.z >= minZ && intersectLeft.z <= maxZ)
                    intersections.Add(intersectLeft);
            }

            // 2. 右边界 (x = maxX)
            if (Mathf.Abs(dir.x) > 0.001f)
            {
                float tRight = (maxX - p1.x) / dir.x;
                Vector3 intersectRight = p1 + dir * tRight;
                if (intersectRight.z >= minZ && intersectRight.z <= maxZ)
                    intersections.Add(intersectRight);
            }

            // 3. 下边界 (z = minZ)
            if (Mathf.Abs(dir.z) > 0.001f)
            {
                float tBottom = (minZ - p1.z) / dir.z;
                Vector3 intersectBottom = p1 + dir * tBottom;
                if (intersectBottom.x >= minX && intersectBottom.x <= maxX)
                    intersections.Add(intersectBottom);
            }

            // 4. 上边界 (z = maxZ)
            if (Mathf.Abs(dir.z) > 0.001f)
            {
                float tTop = (maxZ - p1.z) / dir.z;
                Vector3 intersectTop = p1 + dir * tTop;
                if (intersectTop.x >= minX && intersectTop.x <= maxX)
                    intersections.Add(intersectTop);
            }

            // 我们需要两个交点（一个在p1之前，一个在p1之后）
            if (intersections.Count < 2)
                return false;

            // 计算每个交点到p1的距离（带符号，表示方向）
            List<(Vector3 point, float distance)> signedDistances = new List<(Vector3, float)>();
            foreach (Vector3 intersection in intersections)
            {
                Vector3 toIntersection = intersection - p1;
                float distance = toIntersection.magnitude;

                // 判断方向：如果点积为正，则是相同方向；如果为负，则是相反方向
                float dot = Vector3.Dot(toIntersection.normalized, dir);
                float signedDistance = dot > 0 ? distance : -distance;

                signedDistances.Add((intersection, signedDistance));
            }

            // 排序并找到最远的负距离和最远的正距离
            signedDistances.Sort((a, b) => a.distance.CompareTo(b.distance));

            // 找到最远的负距离（p1之前最远的点）
            Vector3? farNegative = null;
            foreach (var (point, distance) in signedDistances)
            {
                if (distance < 0)
                {
                    farNegative = point;
                    break;
                }
            }

            // 找到最远的正距离（p1之后最远的点）
            Vector3? farPositive = null;
            for (int i = signedDistances.Count - 1; i >= 0; i--)
            {
                if (signedDistances[i].distance > 0)
                {
                    farPositive = signedDistances[i].point;
                    break;
                }
            }

            if (!farNegative.HasValue || !farPositive.HasValue)
                return false;

            intersection1 = farNegative.Value.ToIntVec3();
            intersection2 = farPositive.Value.ToIntVec3();

            return true;
        }
        /// <summary>
        /// 根据航线信息创建 FlyOver
        /// </summary>
        public static FlyOver MakeFlyOverWithPathInfo(ThingDef flyOverDef, FlightPathInfo pathInfo, Map map,
            float speed = 1f, float height = 10f, ThingOwner contents = null,
            float fadeInDuration = 1.5f, float defaultFadeOutDuration = 0.5f, Pawn casterPawn = null,
            bool useApproachAnimation = true, float approachDuration = 1.0f, float approachOffsetDistance = 3f,
            bool? useFadeEffects = null, bool? useFadeIn = null, bool? useFadeOut = null,
            bool useDepartureAnimation = true, float departureDuration = 1.0f, float departureOffsetDistance = 3f,
            float maxStayTime = 0f)
        {
            // 验证路径信息
            if (!pathInfo.Validate(out string validationError))
            {
                Log.Error($"Invalid flight path: {validationError}");
                return null;
            }

            // 生成路径
            if (!pathInfo.GeneratePath(map, out IntVec3 startPoint, out IntVec3 endPoint))
            {
                Log.Error("Failed to generate flight path");
                return null;
            }

            // 创建 FlyOver
            FlyOver flyOver = MakeFlyOver(
                flyOverDef,
                startPoint,
                endPoint,
                map,
                speed,
                height,
                contents,
                fadeInDuration,
                defaultFadeOutDuration,
                casterPawn,
                useApproachAnimation,
                approachDuration,
                approachOffsetDistance,
                useFadeEffects,
                useFadeIn,
                useFadeOut,
                useDepartureAnimation,
                departureDuration,
                departureOffsetDistance,
                maxStayTime
            );

            return flyOver;
        }
    }

    // 扩展的 ModExtension 配置 - 修改：添加新参数
    public class FlyOverShadowExtension : DefModExtension
    {
        public string customShadowPath;
        public float shadowIntensity = 0.6f;
        public bool useCustomShadow = false;
        public float minShadowAlpha = 0.05f;
        public float maxShadowAlpha = 0.2f;
        public float minShadowScale = 0.5f;
        public float maxShadowScale = 1.0f;
        public float defaultFadeInDuration = 1.5f;
        public float fixedFadeOutDuration = 0.5f; // 固定淡出持续时间 - 修改
        public float fadeOutStartProgress = 0.7f;

        // 进场动画配置
        public bool useApproachAnimation = true;
        public float approachDuration = 1.0f;
        public float approachOffsetDistance = 3f;

        // 离场动画配置 - 新增
        public bool useDepartureAnimation = true;
        public float departureDuration = 1.0f;
        public float departureOffsetDistance = 3f;
        public SoundDef departureSound; // 离场音效

        // 时间限制配置 - 新增
        public float maxStayTime = 0f; // 0表示无限制

        // 淡入淡出开关
        public bool useFadeEffects = true; // 是否启用淡入淡出效果
        public bool useFadeIn = true;      // 是否启用淡入效果
        public bool useFadeOut = true;     // 是否启用淡出效果

        public float ActuallyHeight = 150f;
    }
}