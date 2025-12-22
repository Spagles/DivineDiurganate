// File: CompMoteEmitterNorthward.cs
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
    /// </summary>
    public class CompMoteEmitterNorthward : ThingComp
    {
        private CompProperties_MoteEmitterNorthward Props => 
            (CompProperties_MoteEmitterNorthward)props;
        
        private int ticksUntilNextEmit;
        
        // 缓存引用
        private CompMechPilotHolder pilotHolder;
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            // 随机化初始计时器，避免所有发射器同时发射
            ticksUntilNextEmit = Rand.Range(0, Props.emitIntervalTicks);
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // 获取驾驶员容器组件
            pilotHolder = parent.TryGetComp<CompMechPilotHolder>();
            
            // 如果需要驾驶员但组件不存在，发出警告
            if (Props.requirePilot && pilotHolder == null)
            {
                Log.Warning($"[DD] CompMoteEmitterNorthward on {parent} requires pilot but no CompMechPilotHolder found");
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (!parent.Spawned || parent.Map == null)
                return;
                
            // 检查是否满足发射条件
            if (!CanEmit())
                return;
                
            ticksUntilNextEmit--;
            
            if (ticksUntilNextEmit <= 0)
            {
                EmitMote();
                ticksUntilNextEmit = Props.emitIntervalTicks;
            }
        }
        
        /// <summary>
        /// 检查是否可以发射Mote
        /// </summary>
        private bool CanEmit()
        {
            // 新增：检查驾驶员条件
            if (Props.requirePilot)
            {
                // 需要至少一个驾驶员
                if (pilotHolder == null || !pilotHolder.HasPilots)
                    return false;
                    
                // 可选：检查驾驶员是否存活
                foreach (var pilot in pilotHolder.GetPilots())
                {
                    if (pilot.Dead || pilot.Downed)
                        return false;
                }
            }
            
            // 检查电源条件
            if (Props.onlyWhenPowered)
            {
                var powerComp = parent.TryGetComp<CompPowerTrader>();
                if (powerComp != null && !powerComp.PowerOn)
                    return false;
            }
                
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
            
            return true;
        }
        
        private void EmitMote()
        {
            try
            {
                // 计算发射位置（可选偏移）
                Vector3 emitPos = parent.DrawPos + Props.offset;
                
                // 创建Mote
                Mote mote = (Mote)ThingMaker.MakeThing(Props.moteDef);
                
                if (mote is MoteThrown moteThrown)
                {
                    // 设置初始位置
                    moteThrown.exactPosition = emitPos;
                    
                    // 设置向北移动的速度
                    moteThrown.SetVelocity(
                        angle: 0f, // 0度 = 北向
                        speed: Props.moveSpeed
                    );
                    
                    // 设置旋转
                    moteThrown.exactRotation = Props.rotation;
                    moteThrown.rotationRate = Props.rotationRate;
                    
                    // 设置缩放
                    moteThrown.Scale = Props.scale;
                    
                    // 设置存活时间
                    moteThrown.airTimeLeft = Props.lifetimeTicks;
                    
                    // 添加到地图
                    GenSpawn.Spawn(mote, parent.Position, parent.Map);
                }
                else
                {
                    // 不是MoteThrown类型，使用基础设置
                    mote.exactPosition = emitPos;
                    mote.Scale = Props.scale;
                    GenSpawn.Spawn(mote, parent.Position, parent.Map);
                }
                
                // 播放发射音效
                if (Props.soundOnEmit != null)
                {
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
        }
        
        /// <summary>
        /// 获取组件状态信息（用于调试）
        /// </summary>
        public string GetStatusInfo()
        {
            string pilotStatus = "N/A";
            if (pilotHolder != null)
            {
                pilotStatus = pilotHolder.HasPilots ? 
                    $"Has {pilotHolder.CurrentPilotCount} pilot(s)" : 
                    "No pilots";
            }
            
            return $"Mote Emitter Status:\n" +
                   $"  Can Emit: {CanEmit()}\n" +
                   $"  Pilot Status: {pilotStatus}\n" +
                   $"  Next Emit: {ticksUntilNextEmit} ticks\n" +
                   $"  Powered: {(Props.onlyWhenPowered ? CheckPowerStatus() : "N/A")}\n" +
                   $"  Time: {GenLocalDate.HourFloat(parent):F1}h";
        }
        
        private string CheckPowerStatus()
        {
            var powerComp = parent.TryGetComp<CompPowerTrader>();
            if (powerComp == null)
                return "No power comp";
            return powerComp.PowerOn ? "Powered" : "No power";
        }
    }
    
    /// <summary>
    /// 组件属性（更新版）
    /// </summary>
    public class CompProperties_MoteEmitterNorthward : CompProperties
    {
        /// <summary>Mote定义</summary>
        public ThingDef moteDef;
        
        /// <summary>发射间隔（ticks）</summary>
        public int emitIntervalTicks = 60; // 默认1秒
        
        /// <summary>移动速度</summary>
        public float moveSpeed = 1f;
        
        /// <summary>Mote生命周期（ticks）</summary>
        public float lifetimeTicks = 120f; // 默认2秒
        
        /// <summary>初始旋转角度</summary>
        public float rotation = 0f;
        
        /// <summary>旋转速度（度/秒）</summary>
        public float rotationRate = 0f;
        
        /// <summary>缩放大小</summary>
        public float scale = 1f;
        
        /// <summary>偏移位置（相对于父物体）</summary>
        public Vector3 offset = Vector3.zero;
        
        /// <summary>发射时的音效</summary>
        public SoundDef soundOnEmit;
        
        /// <summary>是否只在启用的状态发射</summary>
        public bool onlyWhenPowered = false;
        
        /// <summary>是否只在至少有一个驾驶员时发射</summary>
        public bool requirePilot = false; // 新增：驾驶员条件
        
        /// <summary>天气条件：只在指定天气发射（用逗号分隔）</summary>
        public string onlyInWeather;
        
        /// <summary>地形条件：只在指定地形发射</summary>
        public TerrainDef onlyOnTerrain;
        
        /// <summary>驾驶员条件：只在驾驶员存活时发射</summary>
        public bool requirePilotAlive = true; // 新增：要求驾驶员存活
        
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
            
            if (lifetimeTicks <= 0)
            {
                yield return $"lifetimeTicks must be greater than 0 for {parentDef.defName}";
            }
            
            if (requirePilot && parentDef.GetCompProperties<CompProperties_MechPilotHolder>() == null)
            {
                yield return $"requirePilot is true but no CompProperties_MechPilotHolder found for {parentDef.defName}";
            }
        }
    }
}
