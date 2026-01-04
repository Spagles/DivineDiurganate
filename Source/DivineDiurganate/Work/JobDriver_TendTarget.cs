using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    /// <summary>
    /// 治疗工作 - 支持跟随移动目标
    /// </summary>
    public class JobDriver_TendTargetFollow : JobDriver
    {
        private const TargetIndex TargetInd = TargetIndex.A;
        private const int TicksPerHeal = 60;
        private const float HealAmount = 2f;
        
        private int ticksUntilNextHeal;
        private bool isFollowing = false;
        
        protected Pawn Target => (Pawn)job.GetTarget(TargetInd).Thing;
        
        // 同性恋特性几率（1%）
        private const float GayTraitChance = 0.01f;
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetInd);
            this.FailOn(() => Target.Dead);
            this.FailOn(() => !TargetNeedsHealing());
            
            // 主循环：跟随和治疗
            Toil followAndHealToil = new Toil
            {
                initAction = () =>
                {
                    ticksUntilNextHeal = TicksPerHeal;
                    isFollowing = false;
                },
                
                tickAction = () =>
                {
                    // 检查距离
                    float distance = Target.Position.DistanceTo(pawn.Position);
                    float healDistance = 1.9f; // 治疗距离
                    float followDistance = 10f; // 最大跟随距离
                    
                    if (distance > followDistance)
                    {
                        // 目标太远，结束工作
                        pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                        return;
                    }
                    
                    if (distance > healDistance)
                    {
                        // 需要跟随目标
                        if (!isFollowing || !pawn.pather.Moving || pawn.pather.Destination != Target)
                        {
                            StartFollowing();
                        }
                    }
                    else
                    {
                        // 在治疗距离内，可以治疗
                        if (isFollowing)
                        {
                            StopFollowing();
                        }
                        
                        PerformHealingCycle();
                    }
                },
                
                defaultCompleteMode = ToilCompleteMode.Never
            };
            
            followAndHealToil.AddEndCondition(() => 
            {
                if (!TargetNeedsHealing())
                    return JobCondition.Succeeded;
                return JobCondition.Ongoing;
            });
            
            yield return followAndHealToil;
        }
        
        /// <summary>
        /// 开始跟随目标
        /// </summary>
        private void StartFollowing()
        {
            if (isFollowing) return;
            
            isFollowing = true;
            
            // 停止当前动作
            pawn.pather.StopDead();
            
            // 开始跟随目标
            if (pawn.CanReach(Target, PathEndMode.Touch, Danger.Deadly))
            {
                pawn.pather.StartPath(Target, PathEndMode.Touch);
            }
        }
        
        /// <summary>
        /// 停止跟随
        /// </summary>
        private void StopFollowing()
        {
            if (!isFollowing) return;
            
            isFollowing = false;
            
            // 停止移动
            if (pawn.pather.Moving)
            {
                pawn.pather.StopDead();
            }
        }
        
        /// <summary>
        /// 执行治疗周期
        /// </summary>
        private void PerformHealingCycle()
        {
            pawn.rotationTracker.FaceTarget(Target);
            
            ticksUntilNextHeal--;
            if (ticksUntilNextHeal <= 0)
            {
                PerformHealing();
                ticksUntilNextHeal = TicksPerHeal;
            }
            
            // 学习医疗技能
            if (pawn.skills != null)
            {
                pawn.skills.Learn(SkillDefOf.Medicine, 0.05f);
            }
            
            // 显示治疗特效
            if (Find.TickManager.TicksGame % 30 == 0) // 每0.5秒显示一次
            {
                FleckMaker.ThrowMetaIcon(Target.Position, Target.Map, FleckDefOf.HealingCross);
            }
        }
        
        /// <summary>
        /// 执行治疗
        /// </summary>
        private void PerformHealing()
        {
            if (Target.Dead || Target.health == null)
                return;
                
            float amount = HealAmount;
            
            // 治疗伤害
            HealInjuries(ref amount);
            
            // 检查并尝试添加同性恋特性
            TryAddGayTrait();
        }
        
        /// <summary>
        /// 治疗伤害
        /// </summary>
        private void HealInjuries(ref float amount)
        {
            // 获取所有伤害
            var injuries = Target.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => h.Severity > 0)
                .OrderByDescending(h => h.Severity)
                .ToList();
            
            foreach (var injury in injuries)
            {
                if (amount <= 0) break;
                
                float heal = Mathf.Min(amount, injury.Severity);
                injury.Severity -= heal;
                amount -= heal;
                
                if (injury.Severity <= 0)
                {
                    Target.health.RemoveHediff(injury);
                }
            }
        }
        
        /// <summary>
        /// 尝试添加同性恋特性
        /// </summary>
        private void TryAddGayTrait()
        {
            // 1. 检查目标是否为女性
            if (Target.gender != Gender.Female)
                return;
            
            // 2. 检查目标是否已经有同性恋特性
            if (Target.story?.traits == null)
                return;
                
            // 检查是否已经有同性恋特性
            var gayTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail("Gay");
            if (gayTraitDef == null)
            {
                // 如果找不到"Gay"特性，尝试使用TraitDefOf中的定义（如果有）
                gayTraitDef = TraitDefOf.Gay;
            }
            
            if (gayTraitDef == null)
                return; // 游戏中没有定义同性恋特性
            
            if (Target.story.traits.HasTrait(gayTraitDef))
                return; // 目标已经有这个特性
            
            // 3. 1%的概率检查
            if (Rand.Value > GayTraitChance)
                return;
            
            // 4. 添加同性恋特性
            Trait gayTrait = new Trait(gayTraitDef, 0, true);
            Target.story.traits.GainTrait(gayTrait);
            
            // 5. 可选：添加一个消息通知
            Messages.Message(
                "DD_HolyHealing_GayRevelation".Translate(Target.NameShortColored), 
                Target, 
                MessageTypeDefOf.PositiveEvent
            );
        }
        
        public bool TargetNeedsHealing()
        {
            if (Target == null || Target.health == null || Target.Dead)
                return false;

            return Target.health.summaryHealth.SummaryHealthPercent < 1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksUntilNextHeal, "ticksUntilNextHeal", 0);
            Scribe_Values.Look(ref isFollowing, "isFollowing", false);
        }
    }
}
