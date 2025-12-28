using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic; // Added for List and HashSet

namespace DivineDiurganate
{
    public class Verb_ShootFireSpew : Verb
    {
        protected override int ShotsPerBurst => base.BurstShotCount;

        public override void WarmupComplete()
        {
            base.WarmupComplete();
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            if (this.verbProps is not VerbProperties_FireSpew verbPropsFireSpew)
            {
                return false;
            }

            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
            }

            IntVec3 position = caster.Position;
            float num = Mathf.Atan2(-(currentTarget.Cell.z - position.z), currentTarget.Cell.x - position.x) * 57.29578f;
            
            float halfAngle = verbPropsFireSpew.degrees / 2f;
            FloatRange value = new FloatRange(num - halfAngle, num + halfAngle);

            IntVec3 center = position;
            Map mapHeld = caster.Map; // Use Caster.Map for consistency
            float effectiveRange = EffectiveRange;

            DamageDef damage = verbPropsFireSpew.damageDef ?? DamageDefOf.Flame;
            ThingDef filth = verbPropsFireSpew.filthDef;
            int damageAmount = verbPropsFireSpew.damageAmount > 0 ? verbPropsFireSpew.damageAmount : damage.defaultDamage;
            float armorPenetration = verbPropsFireSpew.armorPenetration >= 0 ? verbPropsFireSpew.armorPenetration : damage.defaultArmorPenetration;
            float propagationSpeed = verbPropsFireSpew.propagationSpeed >= 0 ? verbPropsFireSpew.propagationSpeed : 0.6f;
            float chanceToStartFire = verbPropsFireSpew.chanceToStartFire >= 0f ? verbPropsFireSpew.chanceToStartFire : 0f;

            List<Thing> ignoredThings = null;
            if (verbPropsFireSpew.avoidFriendlyFire)
            {
                ignoredThings = new List<Thing>();
                var affectedCells = new HashSet<IntVec3>(GenRadial.RadialCellsAround(center, effectiveRange, true));
                foreach (IntVec3 cell in affectedCells)
                {
                    if (cell.InBounds(mapHeld))
                    {
                        List<Thing> thingList = mapHeld.thingGrid.ThingsListAt(cell);
                        for (int i = 0; i < thingList.Count; i++) // Corrected .Count usage
                        {
                            Thing t = thingList[i];
                            if (!ignoredThings.Contains(t) && t.Faction != null && !t.HostileTo(caster))
                            {
                                ignoredThings.Add(t);
                            }
                        }
                    }
                }
            }

            FloatRange? affectedAngle = value;
            GenExplosion.DoExplosion(
                center: center,
                map: mapHeld,
                radius: effectiveRange,
                damType: damage,
                instigator: caster,
                damAmount: damageAmount,
                armorPenetration: armorPenetration,
                explosionSound: null,
                weapon: base.EquipmentSource?.def,
                projectile: this.verbProps.defaultProjectile,
                intendedTarget: null,
                postExplosionSpawnThingDef: filth,
                postExplosionSpawnChance: 1f,
                postExplosionSpawnThingCount: 1,
                applyDamageToExplosionCellsNeighbors: false,
                preExplosionSpawnThingDef: null,
                preExplosionSpawnChance: 0f,
                preExplosionSpawnThingCount: 1,
                chanceToStartFire: chanceToStartFire,
                damageFalloff: false,
                direction: null,
                ignoredThings: ignoredThings, // Correct position for ignoredThings
                affectedAngle: affectedAngle,
                doVisualEffects: false,
                propagationSpeed: propagationSpeed,
                screenShakeFactor: 0f,
                doSoundEffects: false
            );
            
            if (verbPropsFireSpew.effecterDef != null)
            {
                AddEffecterToMaintain(verbPropsFireSpew.effecterDef.Spawn(caster.Position, currentTarget.Cell, caster.Map), caster.Position, currentTarget.Cell, 14, caster.Map);
            }

            lastShotTick = Find.TickManager.TicksGame;
            return true;
        }

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }
            if (CasterIsPawn)
            {
                Pawn casterPawn = CasterPawn;
                if (casterPawn.Faction != Faction.OfPlayer && casterPawn.mindState.MeleeThreatStillThreat && casterPawn.mindState.meleeThreat.Position.AdjacentTo8WayOrInside(casterPawn.Position))
                {
                    return false;
                }
            }
            return true;
        }
    }
}