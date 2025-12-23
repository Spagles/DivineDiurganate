// File: CompMoteEmitterNorthward_Integrated.cs
using RimWorld;
using System;
using UnityEngine;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 组件：持续产生向上（北向）移动的Mote
    /// 整合版本：包含驾驶员条件、移动/静止不同速率、正确的位置计算时机
    /// </summary>
    public class CompMoteEmitterNorthward : ThingComp
    {
        public CompProperties_MoteEmitterNorthward Props => 
            (CompProperties_MoteEmitterNorthward)props;
        
        private int ticksUntilNextEmit;
        
        // 关键缓存引用
        private CompMechPilotHolder pilotHolder;
        private Pawn pawnParent;
        private Vector3 lastDrawPos;
        private bool initialized = false;
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            pawnParent = parent as Pawn;
            
            // 等待PostSpawnSetup进行初始化，避免过早计算位置
            ticksUntilNextEmit = Rand.Range(0, Props.emitIntervalTicks);
            initialized = false;
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // 在机甲完全生成后获取组件和位置
            pilotHolder = parent.TryGetComp<CompMechPilotHolder>();
            
            if (parent.Spawned)
            {
                lastDrawPos = parent.DrawPos;
                
                // 警告：如果需要驾驶员但组件不存在
                if (Props.requirePilot && pilotHolder == null)
                {
                    Log.Warning($"[DD] CompMoteEmitterNorthward on {parent} requires pilot but no CompMechPilotHolder found");
                }
            }
            
            initialized = true;
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            // 检查是否已正确初始化
            if (!initialized)
                return;
            
            if (!parent.Spawned || parent.Map == null)
                return;
            
            // 检查是否可以发射
            if (!CanEmit())
                return;
            
            ticksUntilNextEmit--;
            
            if (ticksUntilNextEmit <= 0)
            {
                EmitMote();
                // 根据移动状态设置下次发射间隔
                UpdateNextEmitInterval();
            }
            
            // 更新位置记录用于移动检测
            if (pawnParent != null && parent.Spawned)
            {
                lastDrawPos = pawnParent.DrawPos;
            }
        }
        
        /// <summary>
        /// 更新下次发射间隔（基于移动状态）
        /// </summary>
        private void UpdateNextEmitInterval()
        {
            bool isMoving = IsCurrentlyMoving();
            
            // 根据移动状态选择间隔
            int baseInterval = isMoving ? Props.emitIntervalMovingTicks : Props.emitIntervalTicks;
            
            // 如果移动间隔为0，使用静止间隔
            if (isMoving && Props.emitIntervalMovingTicks <= 0)
            {
                baseInterval = Props.emitIntervalTicks;
            }
            
            // 添加随机性
            if (Props.randomIntervalFactor > 0f)
            {
                float randomFactor = Rand.Range(1f - Props.randomIntervalFactor, 1f + Props.randomIntervalFactor);
                ticksUntilNextEmit = Mathf.RoundToInt(baseInterval * randomFactor);
            }
            else
            {
                ticksUntilNextEmit = baseInterval;
            }
        }
        
        /// <summary>
        /// 检查单位是否在移动
        /// </summary>
        private bool IsCurrentlyMoving()
        {
            if (pawnParent == null || !parent.Spawned)
                return false;
            
            // 方法1: 检查寻路器
            if (pawnParent.pather != null && pawnParent.pather.Moving)
                return true;
            
            // 方法2: 检查位置变化
            if (lastDrawPos != Vector3.zero)
            {
                float distanceMoved = Vector3.Distance(pawnParent.DrawPos, lastDrawPos);
                if (distanceMoved > 0.01f)
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 检查是否可以发射Mote
        /// </summary>
        private bool CanEmit()
        {
            // 检查驾驶员条件
            if (Props.requirePilot)
            {
                if (pilotHolder == null)
                    return false;
                    
                if (!pilotHolder.HasPilots)
                    return false;
                    
                if (Props.requirePilotAlive)
                {
                    foreach (var pilot in pilotHolder.GetPilots())
                    {
                        if (pilot == null || pilot.Dead || pilot.Downed)
                            return false;
                    }
                }
            }
            
            // 检查电源条件
            if (Props.onlyWhenPowered)
            {
                var powerComp = parent.TryGetComp<CompPowerTrader>();
                if (powerComp != null && !powerComp.PowerOn)
                    return false;
            }
            
            // 检查移动状态限制
            if (Props.onlyWhenMoving && !IsCurrentlyMoving())
                return false;
            
            if (Props.onlyWhenStanding && IsCurrentlyMoving())
                return false;
            
            // 检查天气条件
            if (!string.IsNullOrEmpty(Props.onlyInWeather))
            {
                var currentWeather = parent.Map.weatherManager.curWeather;
                if (currentWeather == null || currentWeather.defName != Props.onlyInWeather)
                    return false;
            }
            
            // 检查地形条件
            if (Props.onlyOnTerrain != null)
            {
                var terrain = parent.Position.GetTerrain(parent.Map);
                if (terrain != Props.onlyOnTerrain)
                    return false;
            }
            
            // 检查时间条件
            float currentHour = GenLocalDate.HourFloat(parent.Map);
            if (!Props.emitDuringHours.Includes(currentHour))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// 根据Pawn面向方向计算偏移位置
        /// </summary>
        private Vector3 GetFacingAdjustedOffset()
        {
            Vector3 baseOffset = Props.offset;
            
            if (pawnParent == null || !Props.adjustOffsetWithFacing)
                return baseOffset;
            
            // 确保Pawn已正确初始化
            if (!pawnParent.Spawned)
                return baseOffset;
            
            Rot4 rotation = pawnParent.Rotation;
            
            switch (rotation.AsInt)
            {
                case 0: // 北
                    return new Vector3(baseOffset.x, baseOffset.y, baseOffset.z);
                case 1: // 东
                    return new Vector3(-baseOffset.z, baseOffset.y, baseOffset.x);
                case 2: // 南
                    return new Vector3(-baseOffset.x, baseOffset.y, -baseOffset.z);
                case 3: // 西
                    return new Vector3(baseOffset.z, baseOffset.y, -baseOffset.x);
                default:
                    return baseOffset;
            }
        }
        
        private void EmitMote()
        {
            try
            {
                // 确保父对象已正确生成
                if (!parent.Spawned || parent.DrawPos == Vector3.zero)
                    return;
                
                // 计算基于Pawn面向的偏移位置
                Vector3 adjustedOffset = GetFacingAdjustedOffset();
                Vector3 emitPos = parent.DrawPos + adjustedOffset;
                
                // 添加垂直偏移
                emitPos += new Vector3(0f, Props.verticalOffset, 0f);
                
                // 应用随机偏移
                if (Props.randomOffset.magnitude > 0)
                {
                    emitPos += new Vector3(
                        Rand.Range(-Props.randomOffset.x, Props.randomOffset.x),
                        Rand.Range(-Props.randomOffset.y, Props.randomOffset.y),
                        Rand.Range(-Props.randomOffset.z, Props.randomOffset.z)
                    );
                }
                
                // 创建Mote
                Mote mote = (Mote)ThingMaker.MakeThing(Props.moteDef);
                
                if (mote is MoteThrown moteThrown)
                {
                    // 设置位置
                    moteThrown.exactPosition = emitPos;
                    
                    // 设置Mote角度
                    float moteAngle = Props.baseAngle;
                    if (Props.adjustMoteAngleWithFacing && pawnParent != null)
                    {
                        float facingAngle = pawnParent.Rotation.AsAngle;
                        moteAngle = Props.baseAngle + facingAngle;
                    }
                    
                    // 设置Mote速度和方向
                    float moveSpeed = IsCurrentlyMoving() && Props.moveSpeedMoving > 0 ? 
                        Props.moveSpeedMoving : Props.moveSpeed;
                    
                    moteThrown.SetVelocity(moteAngle, moveSpeed);
                    
                    // 设置旋转
                    if (Props.randomRotation)
                    {
                        moteThrown.exactRotation = Rand.Range(0f, 360f);
                    }
                    else
                    {
                        float rotation = IsCurrentlyMoving() && Props.rotationMoving != 0 ? 
                            Props.rotationMoving : Props.rotation;
                        moteThrown.exactRotation = rotation;
                    }
                    
                    moteThrown.rotationRate = IsCurrentlyMoving() && Props.rotationRateMoving != 0 ? 
                        Props.rotationRateMoving : Props.rotationRate;
                    
                    // 设置缩放
                    float scale = IsCurrentlyMoving() && Props.scaleMoving > 0 ? 
                        Props.scaleMoving : Props.scale;
                    
                    if (Props.randomScaleRange > 0)
                    {
                        scale *= Rand.Range(1f - Props.randomScaleRange, 1f + Props.randomScaleRange);
                    }
                    moteThrown.Scale = scale;
                    
                    // 设置生存时间
                    float lifetime = IsCurrentlyMoving() && Props.lifetimeMovingTicks > 0 ? 
                        Props.lifetimeMovingTicks : Props.lifetimeTicks;
                    moteThrown.airTimeLeft = lifetime;
                    
                    // 添加到地图
                    GenSpawn.Spawn(mote, parent.Position, parent.Map);
                }
                else
                {
                    // 不是MoteThrown类型
                    mote.exactPosition = emitPos;
                    float scale = IsCurrentlyMoving() && Props.scaleMoving > 0 ? 
                        Props.scaleMoving : Props.scale;
                    mote.Scale = scale;
                    GenSpawn.Spawn(mote, parent.Position, parent.Map);
                }
                
                // 播放发射音效
                if (Props.soundOnEmit != null)
                {
                    float volume = IsCurrentlyMoving() && Props.soundVolumeMoving > 0 ? 
                        Props.soundVolumeMoving : Props.soundVolume;
                    Props.soundOnEmit.PlayOneShot(parent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error emitting mote: {ex}");
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref ticksUntilNextEmit, "ticksUntilNextEmit", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
            
            // 注意：lastDrawPos不保存，每次加载后重新获取
        }
    }
    
    /// <summary>
    /// 整合版组件属性
    /// </summary>
    public class CompProperties_MoteEmitterNorthward : CompProperties
    {
        // === 核心发射间隔 ===
        public int emitIntervalTicks = 60;          // 静止时发射间隔
        public int emitIntervalMovingTicks = 60;    // 移动时发射间隔（0=使用静止间隔）
        public float randomIntervalFactor = 0f;     // 随机间隔因子
        
        // === 基础参数 ===
        public ThingDef moteDef;
        public float moveSpeed = 1f;
        public float moveSpeedMoving = 0f;          // 移动时速度（0=使用基础速度）
        public float lifetimeTicks = 120f;
        public float lifetimeMovingTicks = 0f;      // 移动时生存时间（0=使用基础时间）
        public float rotation = 0f;
        public float rotationMoving = 0f;           // 移动时旋转
        public float rotationRate = 0f;
        public float rotationRateMoving = 0f;       // 移动时旋转速度
        public float scale = 1f;
        public float scaleMoving = 0f;              // 移动时大小（0=使用基础大小）
        
        // === 位置设置 ===
        public Vector3 offset = Vector3.zero;
        public float verticalOffset = 0f;
        public Vector3 randomOffset = Vector3.zero;
        public float randomScaleRange = 0f;
        public bool randomRotation = false;
        
        // === 方向设置 ===
        public float baseAngle = 0f;
        public bool adjustOffsetWithFacing = true;
        public bool adjustMoteAngleWithFacing = false;
        
        // === 音效 ===
        public SoundDef soundOnEmit;
        public float soundVolume = 1f;
        public float soundVolumeMoving = 0f;        // 移动时音量（0=使用基础音量）
        
        // === 条件限制 ===
        public bool requirePilot = true;
        public bool requirePilotAlive = true;
        public bool onlyWhenPowered = false;
        public bool onlyWhenMoving = false;
        public bool onlyWhenStanding = false;
        public string onlyInWeather;
        public TerrainDef onlyOnTerrain;
        public FloatRange emitDuringHours = new FloatRange(0f, 24f);
        
        public CompProperties_MoteEmitterNorthward()
        {
            compClass = typeof(CompMoteEmitterNorthward);
        }
        
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            
            if (moteDef == null)
            {
                yield return $"moteDef is not defined for {parentDef.defName}";
            }
            
            if (emitIntervalTicks <= 0)
            {
                yield return $"emitIntervalTicks must be greater than 0 for {parentDef.defName}";
            }
            
            if (emitIntervalMovingTicks < 0)
            {
                yield return $"emitIntervalMovingTicks must be >= 0 for {parentDef.defName}";
            }
            
            if (onlyWhenMoving && onlyWhenStanding)
            {
                yield return $"onlyWhenMoving and onlyWhenStanding cannot both be true for {parentDef.defName}";
            }
        }
    }
}
