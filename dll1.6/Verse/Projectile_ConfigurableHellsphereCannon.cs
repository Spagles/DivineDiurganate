using Verse;
using RimWorld;

namespace ConfigurableHellsphereCannon
{
    public class Projectile_ConfigurableHellsphereCannon : Projectile
    {
        private const float OriginalExtraExplosionRadius = 4.9f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = base.Map;
            base.Impact(hitThing, blockedByShield);

            ExplosionParameters customParams = def.GetModExtension<ExplosionParameters>();

            if (customParams != null)
            {
                GenExplosion.DoExplosion(
                    center: base.Position,
                    map: map,
                    radius: customParams.explosionRadius,
                    damType: base.DamageDef,
                    instigator: launcher,
                    damAmount: DamageAmount,
                    armorPenetration: ArmorPenetration,
                    explosionSound: customParams.explosionSound,
                    weapon: equipmentDef,
                    projectile: def,
                    intendedTarget: intendedTarget.Thing,
                    postExplosionSpawnThingDef: customParams.postExplosionSpawnThingDef,
                    postExplosionSpawnChance: customParams.postExplosionSpawnChance,
                    postExplosionSpawnThingCount: customParams.postExplosionSpawnThingCount,
                    postExplosionGasType: customParams.postExplosionGasType,
                    applyDamageToExplosionCellsNeighbors: customParams.applyDamageToExplosionCellsNeighbors,
                    preExplosionSpawnThingDef: customParams.preExplosionSpawnThingDef,
                    preExplosionSpawnChance: customParams.preExplosionSpawnChance,
                    preExplosionSpawnThingCount: customParams.preExplosionSpawnThingCount,
                    chanceToStartFire: def.projectile.explosionChanceToStartFire,
                    damageFalloff: customParams.damageFalloff,
                    doVisualEffects: customParams.doVisualEffects,
                    propagationSpeed: base.DamageDef.expolosionPropagationSpeed,
                    screenShakeFactor: customParams.screenShakeFactor,
                    doSoundEffects: customParams.doSoundEffects,
                    doHitEffects: customParams.doHitEffects,
                    filth: customParams.filth
                );
            }
            else
            {
                GenExplosion.DoExplosion(
                    center: base.Position,
                    map: map,
                    radius: OriginalExtraExplosionRadius,
                    damType: base.DamageDef,
                    instigator: launcher,
                    damAmount: DamageAmount,
                    armorPenetration: ArmorPenetration,
                    weapon: equipmentDef,
                    projectile: def,
                    intendedTarget: intendedTarget.Thing,
                    chanceToStartFire: def.projectile.explosionChanceToStartFire,
                    damageFalloff: false,
                    applyDamageToExplosionCellsNeighbors: false,
                    postExplosionSpawnThingDef: null,
                    postExplosionSpawnChance: 0f,
                    postExplosionSpawnThingCount: 1,
                    preExplosionSpawnThingDef: null,
                    preExplosionSpawnChance: 0f,
                    preExplosionSpawnThingCount: 1,
                    doVisualEffects: true,
                    propagationSpeed: base.DamageDef.expolosionPropagationSpeed
                );
            }
        }
    }
}