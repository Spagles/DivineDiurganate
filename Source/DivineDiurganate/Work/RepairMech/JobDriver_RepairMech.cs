// JobDriver_RepairMech.cs
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Linq;

namespace DivineDiurganate
{
    public class JobDriver_RepairMech : JobDriver
    {
        private const TargetIndex MechInd = TargetIndex.A;
        
        protected int ticksToNextRepair;
        
        protected Pawn Mech => (Pawn)job.GetTarget(TargetIndex.A).Thing;
        
        protected virtual bool Remote => false;
        
        protected CompMechRepairable RepairableComp => Mech?.TryGetComp<CompMechRepairable>();
        
        // 使用配置的修复周期ticks数，并根据MechRepairSpeed调整
        protected int TicksPerRepairCycle
        {
            get
            {
                if (RepairableComp == null)
                    return 120;
                    
                int baseTicks = RepairableComp.Props.ticksPerRepairCycle;
                return Mathf.RoundToInt(baseTicks / pawn.GetStatValue(StatDefOf.MechRepairSpeed));
            }
        }
        
        // 每次修复的HP量
        protected float RepairAmountPerCycle
        {
            get
            {
                if (RepairableComp == null)
                    return 1f;
                    
                return RepairableComp.Props.repairAmountPerCycle;
            }
        }
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOn(() => !MechRepairable() || !MechNeedsRepair());
            
            if (!Remote)
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            
            Toil repairToil = (Remote ? Toils_General.Wait(int.MaxValue) : Toils_General.WaitWith(TargetIndex.A, int.MaxValue, useProgressBar: true, maintainPosture: true, maintainSleep: true));
            
            // 添加维修特效
            if (RepairableComp?.Props.repairEffect != null)
            {
                repairToil.WithEffect(RepairableComp.Props.repairEffect, TargetIndex.A);
            }
            else
            {
                repairToil.WithEffect(EffecterDefOf.MechRepairing, TargetIndex.A);
            }
            
            // 添加维修音效
            if (RepairableComp?.Props.repairSound != null)
            {
                repairToil.PlaySustainerOrSound(RepairableComp.Props.repairSound);
            }
            else
            {
                repairToil.PlaySustainerOrSound(Remote ? SoundDefOf.RepairMech_Remote : SoundDefOf.RepairMech_Touch);
            }
            
            repairToil.AddPreInitAction(delegate
            {
                ticksToNextRepair = TicksPerRepairCycle;
            });
            
            repairToil.handlingFacing = true;
            
            repairToil.tickIntervalAction = delegate(int delta)
            {
                ticksToNextRepair -= delta;
                if (ticksToNextRepair <= 0)
                {
                    RepairTick(delta);
                    ticksToNextRepair = TicksPerRepairCycle;
                }
                pawn.rotationTracker.FaceTarget(Mech);
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.05f * (float)delta);
                }
            };
            
            repairToil.AddFinishAction(delegate
            {
                // 维修完成后，如果机甲被征召，恢复其工作
                if (Mech.jobs?.curJob != null && job.playerForced)
                {
                    Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
            });
            
            repairToil.AddEndCondition(() => MechNeedsRepair() ? JobCondition.Ongoing : JobCondition.Succeeded);
            
            if (!Remote)
            {
                repairToil.activeSkill = () => SkillDefOf.Crafting;
            }
            
            yield return repairToil;
        }
        
        private bool MechRepairable()
        {
            return RepairableComp != null;
        }
        
        private bool MechNeedsRepair()
        {
            return RepairableComp?.NeedsRepair ?? false;
        }
        
        private void RepairTick(int delta)
        {
            if (Mech == null || Mech.health == null || Mech.Dead)
                return;
            
            // 获取需要修复的伤口
            List<Hediff> injuries = new List<Hediff>();
            injuries.AddRange(Mech.health.hediffSet.hediffs.Where(h => 
                h is Hediff_Injury || 
                h is Hediff_MissingPart ||
                (h.def.tendable && h.Severity > 0f)
            ));
            
            if (!injuries.Any())
                return;
            
            // 计算本次修复的总HP量
            float totalRepairAmount = RepairAmountPerCycle;
            
            // 优先修复最严重的伤口
            var sortedInjuries = injuries.OrderByDescending(i => i.Severity).ToList();
            
            foreach (var injury in sortedInjuries)
            {
                if (totalRepairAmount <= 0f)
                    break;
                    
                if (injury is Hediff_Injury injuryHediff)
                {
                    // 修复伤害
                    float healAmount = Mathf.Min(totalRepairAmount, injuryHediff.Severity);
                    injuryHediff.Severity -= healAmount;
                    totalRepairAmount -= healAmount;
                    
                    if (injuryHediff.Severity <= 0f)
                    {
                        Mech.health.RemoveHediff(injuryHediff);
                    }
                }
                else if (injury is Hediff_MissingPart missingPart)
                {
                    // 缺失部位需要特殊处理
                    // 这里可以设置为需要多次修复，或者需要特殊材料
                    Messages.Message("DD_MissingPartWarning".Translate(Mech.LabelShort, missingPart.Label),
                        Mech, MessageTypeDefOf.NeutralEvent);
                    
                    // 对于缺失部位，修复量消耗更多
                    totalRepairAmount = 0f; // 停止本次修复
                }
                else if (injury.def.tendable)
                {
                    // 其他可治疗的hediff
                    float healAmount = Mathf.Min(totalRepairAmount, injury.Severity);
                    injury.Severity -= healAmount;
                    totalRepairAmount -= healAmount;
                    
                    if (injury.Severity <= 0f)
                    {
                        Mech.health.RemoveHediff(injury);
                    }
                }
            }
            
            // 记录修复统计
            if (RepairableComp != null)
            {
                RepairableComp.RecordRepair(RepairAmountPerCycle - totalRepairAmount);
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksToNextRepair, "ticksToNextRepair", 0);
        }
    }
}
