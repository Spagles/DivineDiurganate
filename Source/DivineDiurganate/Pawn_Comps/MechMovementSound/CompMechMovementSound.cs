// CompMechMovementSound.cs
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;

namespace DivineDiurganate
{
    public class CompMechMovementSound : ThingComp
    {
        public CompProperties_MechMovementSound Props => (CompProperties_MechMovementSound)props;
        
        // 核心状态
        private Sustainer soundSustainer;
        private bool isPlaying = false;
        private Vector3 lastPosition = Vector3.zero;
        private float currentSpeed = 0f;
        
        // 缓存引用
        private Pawn mechPawn;
        private CompPowerTrader powerComp;
        private CompMechPilotHolder pilotComp;
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            mechPawn = parent as Pawn;
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            // 获取组件引用
            powerComp = parent.TryGetComp<CompPowerTrader>();
            pilotComp = parent.TryGetComp<CompMechPilotHolder>();
            
            // 初始化位置 - 使用安全的获取方式
            if (mechPawn != null && mechPawn.Spawned)
            {
                lastPosition = GetCurrentPositionSafe();
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            // 检查基础条件
            if (!ShouldProcess())
                return;
            
            // 更新移动状态
            UpdateMovementState();
            
            // 更新音效状态
            UpdateSoundState();
        }
        
        public override void CompTickRare()
        {
            base.CompTickRare();
            
            // 稀有tick也检查，用于节省性能
            if (!ShouldProcess())
                return;
            
            UpdateMovementState();
            UpdateSoundState();
        }
        
        // 检查是否应该处理音效
        private bool ShouldProcess()
        {
            if (mechPawn == null || Props.movementSound == null)
                return false;
            
            // 检查是否已生成
            if (!mechPawn.Spawned)
            {
                StopSound();
                return false;
            }
            
            // 基础状态检查
            if (mechPawn.Dead || mechPawn.Downed || mechPawn.InMentalState)
                return false;
            
            // 检查绘制器是否可用
            if (mechPawn.Drawer == null)
                return false;
            
            // 检查电源需求
            if (Props.requirePower && powerComp != null && !powerComp.PowerOn)
                return false;
            
            // 检查驾驶员需求
            if (Props.requirePilot && pilotComp != null && !pilotComp.HasPilots)
                return false;
            
            return true;
        }
        
        // 安全的获取当前位置方法
        private Vector3 GetCurrentPositionSafe()
        {
            try
            {
                if (mechPawn == null || !mechPawn.Spawned || mechPawn.Drawer == null)
                    return mechPawn?.Position.ToVector3Shifted() ?? Vector3.zero;
                
                return mechPawn.DrawPos;
            }
            catch (System.NullReferenceException)
            {
                // 如果DrawPos访问失败，返回网格位置
                return mechPawn?.Position.ToVector3Shifted() ?? Vector3.zero;
            }
        }
        
        // 更新移动状态
        private void UpdateMovementState()
        {
            if (mechPawn == null || !mechPawn.Spawned)
                return;
            
            // 安全的获取当前位置
            Vector3 currentPos = GetCurrentPositionSafe();
            
            // 计算当前速度（使用帧时间或固定deltaTime）
            float deltaTime = 1f / 60f; // 假设60fps
            
            // 使用Vector3.Distance计算距离
            float distance = Vector3.Distance(currentPos, lastPosition);
            
            // 避免除以零和过大的值
            currentSpeed = distance / Mathf.Max(deltaTime, 0.0001f);
            
            // 更新最后位置
            lastPosition = currentPos;
        }
        
        // 更新音效状态
        private void UpdateSoundState()
        {
            bool shouldBeMoving = currentSpeed > Props.minMovementSpeed;
            
            // 状态变化
            if (shouldBeMoving && !isPlaying)
            {
                StartSound();
            }
            else if (!shouldBeMoving && isPlaying)
            {
                StopSound();
            }
            
            // 维持音效（如果正在播放）
            if (soundSustainer != null)
            {
                try
                {
                    soundSustainer.Maintain();
                }
                catch (System.NullReferenceException)
                {
                    // 如果sustainer突然变为null，重置状态
                    soundSustainer = null;
                    isPlaying = false;
                }
            }
        }
        
        // 开始音效
        private void StartSound()
        {
            if (Props.movementSound == null || soundSustainer != null)
                return;
            
            try
            {
                // 创建音效信息
                SoundInfo soundInfo = SoundInfo.InMap(mechPawn, MaintenanceType.PerTick);
                
                // 创建sustainer
                soundSustainer = Props.movementSound.TrySpawnSustainer(soundInfo);
                
                if (soundSustainer == null)
                {
                    Log.Warning($"[DD] Failed to create sustainer for {Props.movementSound.defName}");
                    isPlaying = false;
                }
                else
                {
                    isPlaying = true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[DD] Error starting movement sound: {ex}");
                soundSustainer = null;
                isPlaying = false;
            }
        }
        
        // 停止音效
        private void StopSound()
        {
            if (soundSustainer != null)
            {
                try
                {
                    soundSustainer.End();
                }
                catch (System.NullReferenceException)
                {
                    // 如果sustainer已无效，忽略异常
                }
                finally
                {
                    soundSustainer = null;
                    isPlaying = false;
                }
            }
        }
        
        // 当机甲被摧毁或禁用时
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            StopSound();
        }
        
        public void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            StopSound();
        }
        
        // 当机甲倒下时
        public override void Notify_Downed()
        {
            base.Notify_Downed();
            StopSound();
        }
        
        // 序列化状态
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            // 保存基础状态
            Scribe_Values.Look(ref lastPosition, "lastPosition", Vector3.zero);
            Scribe_Values.Look(ref currentSpeed, "currentSpeed", 0f);
            
            // 重置音效状态，加载后重新判断
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                soundSustainer = null;
                isPlaying = false;
            }
        }
        
        // 调试Gizmo
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            if (DebugSettings.ShowDevGizmos && mechPawn != null && mechPawn.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Toggle Sound",
                    defaultDesc = "Toggle movement sound effect",
                    action = () =>
                    {
                        if (soundSustainer == null)
                        {
                            StartSound();
                            Messages.Message("Sound started", mechPawn, MessageTypeDefOf.NeutralEvent);
                        }
                        else
                        {
                            StopSound();
                            Messages.Message("Sound stopped", mechPawn, MessageTypeDefOf.NeutralEvent);
                        }
                    }
                };
                
                yield return new Command_Action
                {
                    defaultLabel = $"Speed: {currentSpeed:F2}",
                    defaultDesc = $"Moving: {currentSpeed > Props.minMovementSpeed}, Min: {Props.minMovementSpeed}",
                    action = () => {}
                };
                
                yield return new Command_Action
                {
                    defaultLabel = $"Sound: {(isPlaying ? "ON" : "OFF")}",
                    defaultDesc = $"Spawned: {mechPawn.Spawned}, Drawer: {mechPawn.Drawer != null}",
                    action = () => {}
                };
            }
        }
        
        // 获取状态信息（用于调试）
        public string GetStatusInfo()
        {
            if (mechPawn == null)
                return "Mech pawn is null";
                
            return $"Movement Sound Status:\n" +
                   $"  Active: {(isPlaying ? "Yes" : "No")}\n" +
                   $"  Moving: {currentSpeed > Props.minMovementSpeed}\n" +
                   $"  Speed: {currentSpeed:F2}\n" +
                   $"  Spawned: {mechPawn.Spawned}\n" +
                   $"  Drawer: {mechPawn.Drawer != null}\n" +
                   $"  Has Pilot: {(pilotComp?.HasPilots ?? false ? "Yes" : "No")}\n" +
                   $"  Has Power: {(powerComp?.PowerOn ?? true ? "Yes" : "No")}";
        }
    }
}
