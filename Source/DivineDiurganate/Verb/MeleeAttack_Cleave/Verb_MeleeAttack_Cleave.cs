using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DivineDiurganate
{
    public class Verb_MeleeAttack_Cleave : Verb_MeleeAttack
    {
        private CompCleave Comp
        {
            get
            {
                return this.EquipmentSource?.GetComp<CompCleave>();
            }
        }

        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            if (this.Comp == null)
            {
                // This verb should only be used with a weapon that has CompCleave
                return new DamageWorker.DamageResult();
            }

            DamageWorker.DamageResult result = new DamageWorker.DamageResult();

            // 播放攻击特效
            PlayAttackEffecter(target);

            // 1. 对主目标造成伤害
            DamageInfo dinfo = new DamageInfo(
                this.verbProps.meleeDamageDef,
                this.verbProps.AdjustedMeleeDamageAmount(this, this.CasterPawn),
                this.verbProps.AdjustedArmorPenetration(this, this.CasterPawn),
                -1f,
                this.CasterPawn,
                null,
                this.EquipmentSource?.def
            );
            dinfo.SetTool(this.tool);
            
            if (target.HasThing)
            {
                result = target.Thing.TakeDamage(dinfo);
            }

            // 2. 执行溅射伤害
            Pawn casterPawn = this.CasterPawn;
            if (casterPawn == null || !target.HasThing)
            {
                return result;
            }

            Thing mainTarget = target.Thing;
            Vector3 attackDirection = (mainTarget.Position - casterPawn.Position).ToVector3().normalized;
            bool mainTargetIsHostile = mainTarget.HostileTo(casterPawn);

            // 查找施法者周围的潜在目标
            IEnumerable<Thing> potentialTargets = GenRadial.RadialDistinctThingsAround(casterPawn.Position, casterPawn.Map, this.Comp.Props.cleaveRange, useCenter: true);

            foreach (Thing thing in potentialTargets)
            {
                // 跳过主目标、自己和非生物
                if (thing == mainTarget || thing == casterPawn || !(thing is Pawn secondaryTargetPawn))
                {
                    continue;
                }

                // 根据XML配置决定是否跳过倒地的生物
                if (!this.Comp.Props.damageDowned && secondaryTargetPawn.Downed)
                {
                    continue;
                }
                
                // 智能溅射：次要目标的敌对状态必须与主目标一致
                if (secondaryTargetPawn.Faction == casterPawn.Faction)
                {
                    continue;
                }

                // 检查目标是否在攻击扇形范围内
                Vector3 directionToTarget = (thing.Position - casterPawn.Position).ToVector3().normalized;
                float angle = Vector3.Angle(attackDirection, directionToTarget);

                if (angle <= this.Comp.Props.cleaveAngle / 2f)
                {
                    // 对次要目标造成伤害
                    DamageInfo cleaveDinfo = new DamageInfo(
                        this.verbProps.meleeDamageDef,
                        this.verbProps.AdjustedMeleeDamageAmount(this, casterPawn) * this.Comp.Props.cleaveDamageFactor,
                        this.verbProps.AdjustedArmorPenetration(this, casterPawn) * this.Comp.Props.cleaveDamageFactor,
                        -1f,
                        casterPawn,
                        null,
                        this.EquipmentSource?.def
                    );
                    cleaveDinfo.SetTool(this.tool);
                    secondaryTargetPawn.TakeDamage(cleaveDinfo);
                }
            }
            return result;
        }

        /// <summary>
        /// 播放攻击特效
        /// </summary>
        private void PlayAttackEffecter(LocalTargetInfo target)
        {
            if (this.CasterPawn == null || this.CasterPawn.Map == null)
                return;

            // 播放攻击特效
            if (this.Comp.Props.attackEffecter != null)
            {
                Effecter attackEffect = this.Comp.Props.attackEffecter.Spawn();
                attackEffect.Trigger(new TargetInfo(this.CasterPawn.Position, this.CasterPawn.Map), target.ToTargetInfo(this.CasterPawn.Map));
                attackEffect.Cleanup();
            }

            // 播放溅射特效
            if (this.Comp.Props.cleaveEffecter != null && target.HasThing)
            {
                PlayCleaveEffecter(target.Thing);
            }
        }

        /// <summary>
        /// 播放溅射特效
        /// </summary>
        private void PlayCleaveEffecter(Thing mainTarget)
        {
            if (this.CasterPawn == null || this.CasterPawn.Map == null || mainTarget == null)
                return;

            Pawn casterPawn = this.CasterPawn;

            // 播放主要的溅射特效
            Effecter cleaveEffect = this.Comp.Props.cleaveEffecter.Spawn();
            cleaveEffect.Trigger(new TargetInfo(casterPawn.Position, casterPawn.Map), new TargetInfo(mainTarget.Position, casterPawn.Map));
            cleaveEffect.Cleanup();
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);

            if (target.IsValid && CasterPawn != null && this.Comp != null)
            {
                GenDraw.DrawFieldEdges(GetCleaveCells(target.Cell));
            }
        }

        private List<IntVec3> GetCleaveCells(IntVec3 center)
        {
            if (this.Comp == null)
            {
                return new List<IntVec3>();
            }

            IntVec3 casterPos = this.CasterPawn.Position;
            Map map = this.CasterPawn.Map;
            Vector3 attackDirection = (center - casterPos).ToVector3().normalized;

            return GenRadial.RadialCellsAround(casterPos, this.Comp.Props.cleaveRange, useCenter: true)
                .Where(cell => {
                    if (!cell.InBounds(map)) return false;
                    Vector3 directionToCell = (cell - casterPos).ToVector3();
                    if (directionToCell.sqrMagnitude <= 0.001f) return false; // Exclude caster's cell
                    return Vector3.Angle(attackDirection, directionToCell) <= this.Comp.Props.cleaveAngle / 2f;
                }).ToList();
        }
    }
}
