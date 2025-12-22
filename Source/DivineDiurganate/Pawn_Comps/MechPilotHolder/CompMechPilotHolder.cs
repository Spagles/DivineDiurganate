// CompMechPilotHolder.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    public class CompProperties_MechPilotHolder : CompProperties
    {
        public int maxPilots = 1;
        public string pilotWorkTag = "MechPilot";

        // 新增：驾驶员图标配置
        public string summonPilotIcon = "DivineDiurganate/UI/Commands/DD_Enter_Mech";
        public string ejectPilotIcon = "DivineDiurganate/UI/Commands/DD_Exit_Mech";

        // 新增：单个驾驶员弹出图标配置
        public string ejectSinglePilotIcon = null; // 如果为null，则使用pawn的头像

        public CompProperties_MechPilotHolder()
        {
            this.compClass = typeof(CompMechPilotHolder);
        }

        // 新增：加载图标的方法
        public Texture2D GetSummonPilotIcon()
        {
            if (!string.IsNullOrEmpty(summonPilotIcon) && ContentFinder<Texture2D>.Get(summonPilotIcon, false) != null)
            {
                return ContentFinder<Texture2D>.Get(summonPilotIcon);
            }
            // 默认图标
            return ContentFinder<Texture2D>.Get("UI/Commands/SummonPilot", false) ??
                   BaseContent.BadTex;
        }

        public Texture2D GetEjectPilotIcon()
        {
            if (!string.IsNullOrEmpty(ejectPilotIcon) && ContentFinder<Texture2D>.Get(ejectPilotIcon, false) != null)
            {
                return ContentFinder<Texture2D>.Get(ejectPilotIcon);
            }
            // 默认图标
            return ContentFinder<Texture2D>.Get("UI/Commands/Eject", false) ??
                   BaseContent.BadTex;
        }
    }

    public class CompMechPilotHolder : ThingComp, IThingHolder, ISuspendableThingHolder
    {
        public ThingOwner innerContainer;
        
        public CompProperties_MechPilotHolder Props => (CompProperties_MechPilotHolder)props;
        
        public int CurrentPilotCount => innerContainer.Count;
        public bool HasPilots => innerContainer.Count > 0;
        public bool HasRoom => innerContainer.Count < Props.maxPilots;
        public bool IsFull => innerContainer.Count >= Props.maxPilots;
        
        public bool IsContentsSuspended => true; // 容器内的东西时间不会流逝
        
        public CompMechPilotHolder()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            if (!(parent is DDmechunit))
            {
                Log.Warning($"[IHFM] Not DDmechunit: {parent}");
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
            
            foreach (var pilot in innerContainer.ToList())
            {
                if (pilot is Pawn pawn)
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
        
        private void TrySpawnPilotAtPosition(Pawn pawn, IntVec3 position)
        {
            Map map = parent.Map;
            if (map == null)
                return;
                
            // 尝试在指定位置生成
            if (GenSpawn.Spawn(pawn, position, map, WipeMode.Vanish) != null)
            {
                // 生成成功
            }
            else
            {
                // 如果指定位置不行，找附近的位置
                IntVec3 spawnPos;
                if (RCellFinder.TryFindRandomCellNearWith(position, 
                    cell => cell.Walkable(map) && !cell.Fogged(map) && 
                           map.reachability.CanReach(cell, parent, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false)),
                    map, out spawnPos, 2, 10))
                {
                    GenSpawn.Spawn(pawn, spawnPos, map, WipeMode.Vanish);
                }
                else
                {
                    // 实在找不到位置，就在任意位置生成
                    GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(position, map, 5), map, WipeMode.Vanish);
                }
            }
        }
        
        public Pawn GetPrimaryPilot()
        {
            if (innerContainer.Count > 0)
            {
                foreach (var thing in innerContainer)
                {
                    if (thing is Pawn pawn)
                        return pawn;
                }
            }
            return null;
        }
        
        public IEnumerable<Pawn> GetPilots()
        {
            foreach (var thing in innerContainer)
            {
                if (thing is Pawn pawn)
                    yield return pawn;
            }
        }// 在 CompMechPilotHolder.cs 中添加以下方法
        public void Notify_PilotAdded(Pawn pilot)
        {
            if (pilot.Faction == Faction.OfPlayer)
            {
                Messages.Message("DD_PilotEnteredMech".Translate(pilot.LabelShort, parent.LabelShort),
                    parent, MessageTypeDefOf.PositiveEvent);
            }

            // 通知默认驾驶员组件
            var defaultPilotComp = parent.TryGetComp<CompMechDefaultPilot>();
            if (defaultPilotComp != null)
            {
                // 当添加驾驶员时，确保不会重新生成默认驾驶员
                // 这里可以添加逻辑防止重复生成
            }
        }
        public void Notify_PilotRemoved(Pawn pilot)
        {
            if (pilot.Faction == Faction.OfPlayer)
            {
                Messages.Message("DD_PilotExitedMech".Translate(pilot.LabelShort, parent.LabelShort),
                    parent, MessageTypeDefOf.NeutralEvent);
            }

            // 通知默认驾驶员组件
            var defaultPilotComp = parent.TryGetComp<CompMechDefaultPilot>();
            if (defaultPilotComp != null)
            {
                defaultPilotComp.Notify_PilotRemoved();
            }
        }
        
        private void StopMechJobs()
        {
            var mech = parent as Pawn;
            if (mech == null)
                return;
                
            // 停止所有工作
            mech.jobs?.StopAll();
            
            // 停止移动
            mech.pather?.StopDead();
            
            // 如果需要，还可以停止其他组件的工作
            var drafter = mech.drafter;
            if (drafter != null && mech.Drafted)
            {
                // 如果有机甲被征兆，取消征兆
                mech.drafter.Drafted = false;
            }
            
            // 停止当前所有工作队列
            mech.jobs?.ClearQueuedJobs();
        }
        
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            
            // 机甲被摧毁时，所有驾驶员安全弹出
            RemoveAllPilots();
        }
        
        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            
            // 如果机甲被摧毁，弹出所有驾驶员
            if (parent.Destroyed)
            {
                RemoveAllPilots();
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            // 定期检查驾驶员状态
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                var thing = innerContainer[i];
                if (thing is Pawn pawn && (pawn.Dead || pawn.Downed))
                {
                    RemovePilot(pawn);
                }
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
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var mech = parent as DDmechunit;
            if (mech == null || mech.Faction != Faction.OfPlayer)
                yield break;

            // 召唤驾驶员Gizmo - 使用XML配置的图标
            if (HasRoom)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_SummonPilot".Translate(),
                    defaultDesc = "DD_SummonPilotDesc".Translate(),
                    icon = Props.GetSummonPilotIcon(),  // 使用动态加载的图标
                    action = () =>
                    {
                        ShowPilotSelectionMenu();
                    },
                    hotKey = KeyBindingDefOf.Misc2
                };
            }

            // 弹出所有驾驶员按钮 - 使用XML配置的图标
            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_EjectAllPilots".Translate(),
                    defaultDesc = "DD_EjectAllPilotsDesc".Translate(),
                    icon = Props.GetEjectPilotIcon(),  // 使用动态加载的图标
                    action = () =>
                    {
                        RemoveAllPilots();
                    },
                    hotKey = KeyBindingDefOf.Misc1
                };
            }
        }


        private void ShowPilotSelectionMenu()
        {
            var mech = parent as DDmechunit;
            if (mech == null)
                return;
                
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 获取所有可用的殖民者
            var availableColonists = mech.Map.mapPawns.FreeColonists
                .Where(p => CanAddPilot(p) && p.CanReach(mech, PathEndMode.Touch, Danger.Deadly))
                .ToList();
                
            if (availableColonists.Count == 0)
            {
                options.Add(new FloatMenuOption("DD_NoAvailablePilots".Translate(), null));
            }
            else
            {
                foreach (var colonist in availableColonists)
                {
                    // 创建FloatMenuOption，正确传递参数
                    string colonistLabel = colonist.LabelShortCap;
                    Action action = () => OrderColonistToEnterMech(colonist);
                    //string tooltipText = TooltipForColonist(colonist);
                    
                    // 使用接受Thing作为图标的构造函数
                    FloatMenuOption option = new FloatMenuOption(
                        colonistLabel,
                        action,
                        colonist,  // iconThing
                        Color.white,
                        MenuOptionPriority.Default,
                        null,      // mouseoverGuiAction
                        null,      // revalidateClickTarget
                        0f,        // extraPartWidth
                        null,      // extraPartOnGUI
                        null,      // revalidateWorldClickTarget
                        true,      // playSelectionSound
                        0          // orderInPriority
                    );
                    
                    // 设置tooltip
                    //option.tooltip = new TipSignal(tooltipText);
                    
                    options.Add(option);
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        private void OrderColonistToEnterMech(Pawn colonist)
        {
            var mech = parent as DDmechunit;
            if (mech == null || colonist == null)
                return;
                
            // 为殖民者安排进入机甲的工作
            Job job = JobMaker.MakeJob(DD_JobDefOf.DD_EnterMech, mech);
            colonist.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
        
        //private string TooltipForColonist(Pawn colonist)
        //{
        //    var tooltip = colonist.LabelShortCap + "\n";
            
        //    // 添加技能信息
        //    tooltip += $"射击: {colonist.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0}\n";
        //    tooltip += $"近战: {colonist.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0}\n";
        //    tooltip += $"智力: {colonist.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0}";
            
        //    return tooltip;
        //}
    }
}
