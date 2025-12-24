// File: CompMechPilotHolder.cs (修改版)
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    public class CompMechPilotHolder : ThingComp, IThingHolder, ISuspendableThingHolder
    {
        public ThingOwner innerContainer;
        
        // 标记是否正在处理死亡/销毁事件，避免重复处理
        private bool isProcessingDestruction = false;
        
        // 新增：低血量状态
        private bool isLowHealth = false;
        private int lastHealthCheckTick = -1;
        private const int HEALTH_CHECK_INTERVAL = 60; // 每60帧检查一次
        
        public CompProperties_MechPilotHolder Props => (CompProperties_MechPilotHolder)props;
        
        public int CurrentPilotCount => innerContainer.Count;
        public bool HasPilots => innerContainer.Count > 0;
        public bool HasRoom => innerContainer.Count < Props.maxPilots;
        public bool IsFull => innerContainer.Count >= Props.maxPilots;
        
        public bool IsContentsSuspended => true;
        
        // 新增：机甲血量的属性
        public float HealthPercent
        {
            get
            {
                var mech = parent as Pawn;
                if (mech == null || mech.Dead)
                    return 0f;
                    
                return mech.health.summaryHealth.SummaryHealthPercent;
            }
        }
        
        // 新增：是否处于低血量状态
        public bool IsLowHealth => HealthPercent < Props.autoEjectHealthPercent;
        
        // 新增：是否可以添加驾驶员（考虑低血量状态）
        public bool CanAcceptPilots
        {
            get
            {
                if (!Props.blockEntryWhenLowHealth)
                    return true;
                    
                return HealthPercent >= Props.minHealthForEntry;
            }
        }
        
        public CompMechPilotHolder()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            if (!(parent is DDmechunit))
            {
                Log.Warning($"[DD] CompMechPilotHolder attached to non-mech: {parent}");
            }
            
            // 确保加载后恢复状态
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner<Pawn>(this);
            }
            
            // 初始检查低血量状态
            CheckLowHealthStatus();
        }
        
        // 新增：检查低血量状态的方法
        private void CheckLowHealthStatus()
        {
            if (!Props.autoEjectEnabled)
                return;
                
            float healthPercent = HealthPercent;
            bool wasLowHealth = isLowHealth;
            isLowHealth = healthPercent < Props.autoEjectHealthPercent;
            
            // 如果从非低血量变为低血量，且有机甲在驾驶
            if (isLowHealth && !wasLowHealth && HasPilots)
            {
                Log.Message($"[DD] 机甲 {parent.LabelShort} 血量低于 {Props.autoEjectHealthPercent * 100}%，准备弹出驾驶员");
                EjectAllPilotsDueToLowHealth();
            }
        }
        
        // 新增：因低血量弹出驾驶员
        private void EjectAllPilotsDueToLowHealth()
        {
            if (!HasPilots || isProcessingDestruction)
                return;
                
            try
            {
                isProcessingDestruction = true;
                
                Log.Message($"[DD] 因低血量弹出驾驶员 - 机甲: {parent.LabelShort}, 血量: {HealthPercent * 100:F1}%");
                
                // 获取安全位置
                IntVec3 ejectPos = FindSafeEjectPosition();
                
                // 弹出所有驾驶员
                var pilots = innerContainer.ToList();
                foreach (var thing in pilots)
                {
                    if (thing is Pawn pawn)
                    {
                        Log.Message($"[DD] 弹出驾驶员: {pawn.LabelShort}");
                        
                        // 从容器中移除
                        innerContainer.Remove(pawn);
                        
                        // 尝试生成到地图上
                        if (TrySpawnPilotAtPosition(pawn, ejectPos))
                        {
                            // 给予适当的伤害（模拟紧急弹射）
                            if (!pawn.Dead && !pawn.Downed)
                            {
                                DamageInfo damageInfo = new DamageInfo(
                                    DamageDefOf.Bomb, 
                                    5f, // 低血量弹出的伤害较小
                                    armorPenetration: 999f, 
                                    instigator: parent,
                                    hitPart: pawn.RaceProps.body.AllParts.FirstOrDefault()
                                );
                                pawn.TakeDamage(damageInfo);
                            }
                            
                            Messages.Message("DD_PilotEjectedLowHealth".Translate(pawn.LabelShort, parent.LabelShort, (HealthPercent * 100).ToString("F1")),
                                pawn, MessageTypeDefOf.NegativeEvent);
                        }
                        else
                        {
                            Log.Error($"[DD] 无法弹出驾驶员: {pawn.LabelShort}");
                        }
                    }
                }
                
                Log.Message($"[DD] 低血量弹出完成，剩余驾驶员: {innerContainer.Count}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] 弹出驾驶员时发生错误: {ex}");
            }
            finally
            {
                isProcessingDestruction = false;
            }
        }
        
        public bool CanAddPilot(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed)
                return false;
                
            if (!HasRoom)
                return false;
                
            if (innerContainer.Contains(pawn))
                return false;
                
            // 新增：检查低血量状态
            if (!CanAcceptPilots)
            {
                if (pawn.Faction == Faction.OfPlayer)
                {
                    Messages.Message("DD_CannotEnterLowHealth".Translate(
                        parent.LabelShort, 
                        (HealthPercent * 100).ToString("F1"),
                        (Props.minHealthForEntry * 100).ToString("F1")),
                        parent, MessageTypeDefOf.RejectInput);
                }
                return false;
            }
                
            // 检查工作标签
            if (!string.IsNullOrEmpty(Props.pilotWorkTag))
            {
                WorkTags tag;
                if (System.Enum.TryParse(Props.pilotWorkTag, out tag))
                {
                    if (pawn.WorkTagIsDisabled(tag))
                        return false;
                }
            }
            
            return true;
        }
        
        public void AddPilot(Pawn pawn)
        {
            if (!CanAddPilot(pawn))
                return;
                
            // 新增：再次检查低血量状态（双重保险）
            if (!CanAcceptPilots)
            {
                Messages.Message("DD_CannotEnterLowHealth".Translate(
                    parent.LabelShort, 
                    (HealthPercent * 100).ToString("F1"),
                    (Props.minHealthForEntry * 100).ToString("F1")),
                    parent, MessageTypeDefOf.RejectInput);
                return;
            }
                
            // 将pawn添加到容器中
            if (pawn.Spawned)
                pawn.DeSpawnOrDeselect();
                
            innerContainer.TryAdd(pawn, true);
            
            // 停止pawn的移动
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll();
            
            // 触发事件
            Notify_PilotAdded(pawn);
        }
        
        public void RemovePilot(Pawn pawn, IntVec3? exitPos = null)
        {
            if (innerContainer.Contains(pawn))
            {
                // 从容器中移除
                innerContainer.Remove(pawn);
                
                // 将pawn放回地图
                TrySpawnPilotAtPosition(pawn, exitPos ?? parent.Position);
                
                // 触发事件
                Notify_PilotRemoved(pawn);
                
                // 停止机甲的工作
                StopMechJobs();
            }
        }
        
        public void RemoveAllPilots(IntVec3? exitPos = null)
        {
            // 记录是否有驾驶员
            bool hadPilots = HasPilots;
            
            // 复制列表以避免迭代时修改的问题
            var pilotsToRemove = innerContainer.ToList();
            
            foreach (var thing in pilotsToRemove)
            {
                if (thing is Pawn pawn)
                {
                    RemovePilot(pawn, exitPos);
                }
            }
            
            // 如果有机甲并且原来有驾驶员，现在没有了，停止工作
            if (hadPilots && parent is Pawn mech)
            {
                StopMechJobs();
            }
        }
        
        // 新增：专门用于低血量检查的方法
        private void CheckAndHandleLowHealth()
        {
            if (!Props.autoEjectEnabled)
                return;
                
            // 检查时间间隔
            if (Find.TickManager.TicksGame - lastHealthCheckTick < HEALTH_CHECK_INTERVAL)
                return;
                
            lastHealthCheckTick = Find.TickManager.TicksGame;
            
            // 检查血量状态
            CheckLowHealthStatus();
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            try
            {
                // 新增：检查低血量状态
                CheckAndHandleLowHealth();
                
                // 检查机甲是否死亡
                var mech = parent as Pawn;
                if (mech != null && mech.Dead && HasPilots)
                {
                    Log.Message($"[DD] CompTick检测到机甲死亡: {mech.LabelShort}");
                    EjectAllPilotsOnDeath();
                    return;
                }
                
                // 定期检查驾驶员状态
                var pilotsToRemove = new List<Pawn>();
                foreach (var thing in innerContainer)
                {
                    if (thing is Pawn pawn && (pawn.Dead || pawn.Downed))
                    {
                        pilotsToRemove.Add(pawn);
                    }
                }
                
                foreach (var pawn in pilotsToRemove)
                {
                    RemovePilot(pawn);
                }
                
                // 确保容器内的pawn处于正确状态
                foreach (var thing in innerContainer)
                {
                    if (thing is Pawn pawn)
                    {
                        // 确保pawn在容器内不执行任何工作
                        pawn.jobs?.StopAll();
                        pawn.pather?.StopDead();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] CompTick error: {ex}");
            }
        }
        
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            
            // 如果机甲死亡，弹出驾驶员
            var mech = parent as Pawn;
            if (mech != null && mech.Dead)
            {
                Log.Message($"[DD] 机甲死亡，弹出驾驶员: {mech.LabelShort}");
                EjectAllPilotsOnDeath();
                return;
            }
            
            // 新增：受到伤害后立即检查低血量状态
            if (Props.autoEjectEnabled && HasPilots)
            {
                CheckLowHealthStatus();
            }
        }
        
        // 获取低血量状态信息（用于调试和显示）
        public string GetLowHealthStatusInfo()
        {
            if (!Props.autoEjectEnabled)
                return "自动弹出: 禁用";
                
            string status = $"血量: {HealthPercent * 100:F1}%\n";
            status += $"自动弹出阈值: {Props.autoEjectHealthPercent * 100:F1}%\n";
            
            if (Props.blockEntryWhenLowHealth)
            {
                status += $"允许进入阈值: {Props.minHealthForEntry * 100:F1}%\n";
            }
            
            if (IsLowHealth)
            {
                status += "状态: <color=red>低血量</color>\n";
                if (HasPilots)
                    status += "<color=yellow>即将弹出驾驶员</color>";
                else if (!CanAcceptPilots)
                    status += "<color=orange>禁止进入</color>";
            }
            else
            {
                status += "状态: <color=green>正常</color>";
            }
            
            return status;
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var mech = parent as DDmechunit;
            if (mech == null || mech.Faction != Faction.OfPlayer)
                yield break;

            // 召唤驾驶员Gizmo
            if (HasRoom)
            {
                Command_Action summonCommand = new Command_Action
                {
                    defaultLabel = "DD_SummonPilot".Translate(),
                    defaultDesc = "DD_SummonPilotDesc".Translate(),
                    icon = Props.GetSummonPilotIcon(),
                    action = () =>
                    {
                        ShowPilotSelectionMenu();
                    },
                    hotKey = KeyBindingDefOf.Misc2
                };
                
                // 新增：低血量时禁用召唤按钮
                if (!CanAcceptPilots)
                {
                    summonCommand.Disable("DD_CannotEnterLowHealthShort".Translate(
                        (HealthPercent * 100).ToString("F1"),
                        (Props.minHealthForEntry * 100).ToString("F1")));
                }
                
                yield return summonCommand;
            }

            // 弹出所有驾驶员按钮
            if (HasPilots)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_EjectAllPilots".Translate(),
                    defaultDesc = "DD_EjectAllPilotsDesc".Translate(),
                    icon = Props.GetEjectPilotIcon(),
                    action = () =>
                    {
                        RemoveAllPilots();
                    },
                    hotKey = KeyBindingDefOf.Misc1
                };
            }
            
            // 新增：低血量状态显示按钮（调试）
            if (Prefs.DevMode && DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "调试: 低血量状态",
                    defaultDesc = GetLowHealthStatusInfo(),
                    action = () =>
                    {
                        string info = $"=== 机甲低血量状态 ===\n";
                        info += $"机甲: {parent.LabelShort}\n";
                        info += $"血量百分比: {HealthPercent * 100:F2}%\n";
                        info += $"自动弹出设置:\n";
                        info += $"  启用: {Props.autoEjectEnabled}\n";
                        info += $"  弹出阈值: {Props.autoEjectHealthPercent * 100}%\n";
                        info += $"  禁止进入: {Props.blockEntryWhenLowHealth}\n";
                        info += $"  进入阈值: {Props.minHealthForEntry * 100}%\n";
                        info += $"当前状态:\n";
                        info += $"  低血量: {IsLowHealth}\n";
                        info += $"  可接受驾驶员: {CanAcceptPilots}\n";
                        info += $"  驾驶员数量: {CurrentPilotCount}/{Props.maxPilots}\n";
                        
                        Log.Message(info);
                        Messages.Message(info, parent, MessageTypeDefOf.SilentInput);
                    }
                };
                
                // 调试按钮：模拟低血量
                yield return new Command_Action
                {
                    defaultLabel = "调试: 触发低血量",
                    defaultDesc = "强制触发低血量状态检查",
                    action = () =>
                    {
                        CheckLowHealthStatus();
                        Messages.Message($"低血量检查完成，状态: {(IsLowHealth ? "低血量" : "正常")}", 
                            parent, MessageTypeDefOf.SilentInput);
                    }
                };
            }
        }
        
        // 原有的 FindSafeEjectPosition, TrySpawnPilotAtPosition, Notify_PilotAdded, 
        // Notify_PilotRemoved, StopMechJobs, EjectAllPilotsOnDeath 等方法保持不变...
        // 只需要在原有方法中添加相应的日志和消息即可
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref isProcessingDestruction, "isProcessingDestruction", false);
            Scribe_Values.Look(ref isLowHealth, "isLowHealth", false);
            Scribe_Values.Look(ref lastHealthCheckTick, "lastHealthCheckTick", -1);
        }
        
        // IThingHolder 接口实现保持不变...
    }
}
