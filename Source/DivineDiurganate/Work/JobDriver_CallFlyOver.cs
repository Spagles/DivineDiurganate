using RimWorld;
using System.Collections.Generic;
using Unity.Jobs;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    /// <summary>
    /// 飞机呼叫工作驱动程序
    /// </summary>
    public class JobDriver_CallFlyOver : JobDriver
    {
        private const TargetIndex GeneratorIndex = TargetIndex.A;
        
        // 工作总时长
        private int workDurationTicks = 180; // 默认3秒
        
        // 是否正在工作中
        private bool workStarted = false;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workDurationTicks, "workDurationTicks", 180);
            Scribe_Values.Look(ref workStarted, "workStarted", false);
        }
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 尝试预约目标建筑
            return pawn.Reserve(job.GetTarget(GeneratorIndex), job, 1, -1, null, errorOnFailed);
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 验证目标建筑
            this.FailOnDespawnedNullOrForbidden(GeneratorIndex);
            this.FailOn(() => GetFlyOverGenerator() == null);
            
            // 1. 走到建筑旁边
            yield return Toils_Goto.GotoThing(GeneratorIndex, PathEndMode.InteractionCell);
            
            // 2. 开始工作（3秒）
            Toil workToil = CreateWorkToil();
            yield return workToil;
            
            // 3. 工作完成后的清理
            yield return CreateFinishToil();
        }
        
        /// <summary>
        /// 创建工作Toil
        /// </summary>
        private Toil CreateWorkToil()
        {
            Toil work = new Toil();
            work.initAction = delegate
            {
                workDurationTicks = 180;
                
                // 设置工作持续时间和默认Tick
                work.defaultDuration = workDurationTicks;
                work.defaultCompleteMode = ToilCompleteMode.Delay;
                
                workStarted = true;
            };
            
            work.tickAction = delegate
            {
                // 每帧更新
                // 保持面对建筑
                pawn.rotationTracker.FaceTarget(GetFlyOverGenerator());
                
                // 每30ticks显示一次工作效果
                if (pawn.IsHashIntervalTick(30))
                {
                    // 显示粒子效果
                    FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
                }
            };
            
            work.AddFinishAction(delegate
            {
                // 工作完成或中断
                workStarted = false;
                
                // 如果工作被中断，可以添加一些清理操作
                if (pawn.CurJobDef == job.def && pawn.jobs.curDriver == this)
                {
                    // 工作正常完成，不做特别处理
                }
                else
                {
                    // 工作被中断
                    Log.Message($"DD_Flyover_CallJob_Interrupted".Translate(pawn.NameShortColored));
                }
            });
            
            return work;
        }
        
        /// <summary>
        /// 创建完成工作的Toil
        /// </summary>
        private Toil CreateFinishToil()
        {
            Toil finish = new Toil();
            finish.initAction = delegate
            {
                // 获取生成器组件
                var generator = GetFlyOverGenerator();
                if (generator == null)
                {
                    Log.Error("JobDriver_CallFlyOver: Cannot find flyover generator");
                    return;
                }

                // 获取组件
                var comp = generator.TryGetComp<CompFlyOverGenerator>();
                if (comp == null)
                {
                    Log.Error("JobDriver_CallFlyOver: Cannot find CompFlyOverGenerator");
                    return;
                }
                
                // 调用完成工作方法
                comp.CompleteCallJob();
                
                // 增加完成工作的技能经验
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Intellectual, 100f);
                }
            };
            
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            return finish;
        }
        
        /// <summary>
        /// 获取FlyOver生成器
        /// </summary>
        private Thing GetFlyOverGenerator()
        {
            LocalTargetInfo target = job.GetTarget(GeneratorIndex);
            return target.Thing;
        }
        
        /// <summary>
        /// 获取工作进度（用于UI显示）
        /// </summary>
        public float WorkProgress
        {
            get
            {
                if (workStarted && CurToil != null)
                {
                    int ticksSoFar = CurToil.defaultDuration - CurToil.actor.jobs.curDriver.ticksLeftThisToil;
                    return (float)ticksSoFar / workDurationTicks;
                }
                return 0f;
            }
        }
        
        /// <summary>
        /// 获取工作剩余时间（秒）
        /// </summary>
        public string WorkRemainingTime
        {
            get
            {
                if (workStarted && CurToil != null)
                {
                    int ticksLeft = CurToil.actor.jobs.curDriver.ticksLeftThisToil;
                    if (ticksLeft > 0)
                    {
                        float secondsLeft = ticksLeft / 60f;
                        return secondsLeft.ToString("F1") + "s";
                    }
                }
                return "0s";
            }
        }
        
        /// <summary>
        /// 获取剩余ticks数
        /// </summary>
        public int WorkTicksRemaining
        {
            get
            {
                if (workStarted && CurToil != null)
                {
                    return CurToil.actor.jobs.curDriver.ticksLeftThisToil;
                }
                return 0;
            }
        }
    }
}
