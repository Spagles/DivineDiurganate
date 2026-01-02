using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimWorld.PsychicRitualRoleDef;

namespace DivineDiurganate
{
    // 扩展定义：整合了穿透和爆炸参数
    public class PenetratingBullet_Extension : DefModExtension
    {
        // 穿透相关参数
        public int maxHits = 3;
        public float damageFalloff = 0.25f;
        public bool preventFriendlyFire = false;
        public FleckDef tailFleckDef;
        public int fleckDelayTicks = 10;
        public EffecterDef impactEffecter; // 击中时的特效
        
        // 爆炸相关参数（新增）
        public bool enableExplosion = false; // 是否启用爆炸系统
        public bool explodeOnGroundHit = true; // 击中地面时是否爆炸
        public bool explodeOnMaxPenetration = true; // 达到最大穿透次数时是否爆炸
        public bool explodeOnFirstImpact = false; // 击中第一个目标时是否爆炸（简化版）
    }
    
    // 主穿透子弹类（包含可选的爆炸功能）
    public class Projectile_PenetratingBullet : Bullet
    {
        // 穿透相关字段
        public int hitCounter = 0;
        public List<Thing> alreadyDamaged = new List<Thing>();
        private Vector3 lastTickPosition;
        private int Fleck_MakeFleckTick;
        public int Fleck_MakeFleckTickMax = 1;
        public IntRange Fleck_MakeFleckNum = new IntRange(1, 1);
        public FloatRange Fleck_Angle = new FloatRange(-180f, 180f);
        public FloatRange Fleck_Scale = new FloatRange(1f, 1f);
        public FloatRange Fleck_Speed = new FloatRange(0f, 0f);
        public FloatRange Fleck_Rotation = new FloatRange(-180f, 180f);
        
        // 爆炸相关字段（参考原版爆炸子弹）
        private int ticksToDetonation;
        private bool detonated = false;
        
        // 获取扩展属性
        private PenetratingBullet_Extension Props => def?.GetModExtension<PenetratingBullet_Extension>();
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hitCounter, "hitCounter", 0);
            Scribe_Collections.Look(ref alreadyDamaged, "alreadyDamaged", LookMode.Reference);
            Scribe_Values.Look(ref lastTickPosition, "lastTickPosition");
            Scribe_Values.Look(ref ticksToDetonation, "ticksToDetonation", 0);
            Scribe_Values.Look(ref detonated, "detonated", false);
            
            if (alreadyDamaged == null)
            {
                alreadyDamaged = new List<Thing>();
            }
        }
        
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, 
                                    LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, 
                                    bool preventFriendlyFire = false, Thing equipment = null, 
                                    ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, 
                        preventFriendlyFire, equipment, targetCoverDef);
            
            this.lastTickPosition = origin;
            this.alreadyDamaged.Clear();
            this.hitCounter = 0;
            this.ticksToDetonation = 0;
            this.detonated = false;
            
            // 合并友军伤害设置
            this.preventFriendlyFire = preventFriendlyFire || (Props?.preventFriendlyFire ?? false);
        }
        
        protected override void Tick()
        {
            // 处理爆炸倒计时（参考原版爆炸子弹）
            if (ticksToDetonation > 0)
            {
                ticksToDetonation--;
                if (ticksToDetonation <= 0)
                {
                    Explode();
                    return;
                }
            }
            
            Vector3 startPos = this.lastTickPosition;
            base.Tick();

            if (this.Destroyed || detonated) return;
            
            // 处理飞粒效果
            if (Props != null)
            {
                this.Fleck_MakeFleckTick++;
                if (this.Fleck_MakeFleckTick >= Props.fleckDelayTicks)
                {
                    if (this.Fleck_MakeFleckTick >= (Props.fleckDelayTicks + this.Fleck_MakeFleckTickMax))
                    {
                        this.Fleck_MakeFleckTick = Props.fleckDelayTicks;
                    }
                    
                    Map map = base.Map;
                    if (map != null)
                    {
                        int randomInRange = this.Fleck_MakeFleckNum.RandomInRange;
                        Vector3 currentPosition = this.ExactPosition;
                        for (int i = 0; i < randomInRange; i++)
                        {
                            float currentBulletAngle = ExactRotation.eulerAngles.y;
                            float fleckRotationAngle = currentBulletAngle;
                            float velocityAngle = this.Fleck_Angle.RandomInRange + currentBulletAngle;
                            float randomInRange2 = this.Fleck_Scale.RandomInRange;
                            float randomInRange3 = this.Fleck_Speed.RandomInRange;

                            if (Props?.tailFleckDef != null)
                            {
                                FleckCreationData dataStatic = FleckMaker.GetDataStatic(currentPosition, map, Props.tailFleckDef, randomInRange2);
                                dataStatic.rotation = fleckRotationAngle;
                                dataStatic.rotationRate = this.Fleck_Rotation.RandomInRange;
                                dataStatic.velocityAngle = velocityAngle;
                                dataStatic.velocitySpeed = randomInRange3;
                                map.flecks.CreateFleck(dataStatic);
                            }
                        }
                    }
                }
            }
            
            // 检查爆炸条件（在飞行中达到最大穿透次数）
            if (Props?.enableExplosion == true && Props?.explodeOnMaxPenetration == true && !detonated)
            {
                int maxHits = Props.maxHits;
                bool infinitePenetration = maxHits < 0;
                
                if (!infinitePenetration && hitCounter >= maxHits)
                {
                    // 立即爆炸
                    Explode();
                    return;
                }
            }
            
            if (this.Destroyed || detonated) return;
            Vector3 endPos = this.ExactPosition;

            CheckPathForDamage(startPos, endPos);
            this.lastTickPosition = endPos;
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 原有的穿透检测
            CheckPathForDamage(lastTickPosition, this.ExactPosition);
            
            // 如果已经爆炸过，直接返回
            if (detonated) return;
            
            // 检查是否需要爆炸（参考原版爆炸子弹逻辑）
            if (Props?.enableExplosion == true)
            {
                // 如果被护盾阻挡或爆炸延迟为0，立即爆炸
                if (blockedByShield || def.projectile.explosionDelay == 0)
                {
                    Explode();
                    return;
                }
                
                // 对于爆炸类子弹，简化逻辑：只允许命中第一个穿透的目标
                if (Props.explodeOnFirstImpact && hitCounter >= 1)
                {
                    Explode();
                    return;
                }
                
                // 如果是击中地面且允许地面爆炸
                if (hitThing == null && Props.explodeOnGroundHit)
                {
                    Explode();
                    return;
                }
                
                // 如果已经达到最大穿透次数
                if (Props.explodeOnMaxPenetration && hitCounter >= Props.maxHits)
                {
                    Explode();
                    return;
                }
            }
            
            // 对于非爆炸类子弹，或者没有触发爆炸的情况，使用原有的穿透逻辑
            if (hitThing != null && alreadyDamaged.Contains(hitThing))
            {
                base.Impact(null, blockedByShield);
            }
            else
            {
                base.Impact(hitThing, blockedByShield);
            }

            // 新增：触发击中特效
            if (Props?.impactEffecter != null && launcher?.Map != null)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(this.ExactPosition.ToIntVec3(), this.launcher.Map, false), this.launcher);
            }
        }
        
        private void CheckPathForDamage(Vector3 startPos, Vector3 endPos)
        {
            if (startPos == endPos) return;
            
            if (Props == null) return;
            
            // 对于爆炸类子弹，简化逻辑：只穿透一个目标
            int maxHits = Props.maxHits;
            if (Props.enableExplosion)
            {
                maxHits = Mathf.Min(1, Props.maxHits);
            }
            
            bool infinitePenetration = maxHits < 0;
            if (!infinitePenetration && hitCounter >= maxHits) return;
            
            Map map = this.Map;
            if (map == null) return;
            
            float distance = Vector3.Distance(startPos, endPos);
            Vector3 direction = (endPos - startPos).normalized;
            for (float i = 0; i < distance; i += 0.8f)
            {
                if (!infinitePenetration && hitCounter >= maxHits) break;
                Vector3 checkPos = startPos + direction * i;
                var thingsInCell = new HashSet<Thing>(map.thingGrid.ThingsListAt(checkPos.ToIntVec3()));
                foreach (Thing thing in thingsInCell)
                {
                    if (thing is Pawn pawn && pawn != this.launcher && !alreadyDamaged.Contains(pawn))
                    {
                        bool shouldDamage = false;
                        if (this.intendedTarget.Thing == pawn)
                        {
                            shouldDamage = true;
                        }
                        else if (pawn.HostileTo(this.launcher))
                        {
                            shouldDamage = true;
                        }
                        else if (!this.preventFriendlyFire)
                        {
                            shouldDamage = true;
                        }
                        if (shouldDamage)
                        {
                            ApplyPathDamage(pawn);
                            if (!infinitePenetration && hitCounter >= maxHits) break;
                        }
                    }
                }
            }
        }
        
        private void ApplyPathDamage(Pawn pawn)
        {
            float falloff = Props?.damageFalloff ?? 0.25f;

            float damageMultiplier = Mathf.Pow(1f - falloff, hitCounter);

            int damageAmount = (int)(this.DamageAmount * damageMultiplier);
            if (damageAmount <= 0) return;
            
            var dinfo = new DamageInfo(
                this.def.projectile.damageDef,
                damageAmount,
                this.ArmorPenetration * damageMultiplier,
                this.ExactRotation.eulerAngles.y,
                this.launcher,
                null,
                this.equipmentDef,
                DamageInfo.SourceCategory.ThingOrUnknown,
                this.intendedTarget.Thing);

            pawn.TakeDamage(dinfo);
            alreadyDamaged.Add(pawn);
            hitCounter++;
        }
        
        // 爆炸方法（参考原版爆炸子弹）
        protected virtual void Explode()
        {
            // 防止多次爆炸
            if (detonated || Destroyed) return;
            
            detonated = true;
            
            Map map = base.Map;
            
            // 如果子弹已经销毁，不需要再销毁
            if (!Destroyed)
            {
                Destroy();
            }
            
            if (map == null) return;
            
            // 触发爆炸特效
            if (def.projectile.explosionEffect != null)
            {
                Effecter effecter = def.projectile.explosionEffect.Spawn();
                if (def.projectile.explosionEffectLifetimeTicks != 0)
                {
                    map.effecterMaintainer.AddEffecterToMaintain(effecter, base.Position.ToVector3().ToIntVec3(), def.projectile.explosionEffectLifetimeTicks);
                }
                else
                {
                    effecter.Trigger(new TargetInfo(base.Position, map), new TargetInfo(base.Position, map));
                    effecter.Cleanup();
                }
            }

            IntVec3 position = base.Position;
            float explosionRadius = def.projectile.explosionRadius;
            DamageDef damageDef = base.DamageDef;
            Thing instigator = launcher;
            int damageAmount = DamageAmount;
            float armorPenetration = ArmorPenetration;
            SoundDef soundExplode = def.projectile.soundExplode;
            ThingDef weapon = equipmentDef;
            ThingDef projectile = def;
            Thing thing = intendedTarget.Thing;
            ThingDef postExplosionSpawnThingDef = def.projectile.postExplosionSpawnThingDef ?? (def.projectile.explosionSpawnsSingleFilth ? null : def.projectile.filth);
            ThingDef postExplosionSpawnThingDefWater = def.projectile.postExplosionSpawnThingDefWater;
            float postExplosionSpawnChance = def.projectile.postExplosionSpawnChance;
            int postExplosionSpawnThingCount = def.projectile.postExplosionSpawnThingCount;
            GasType? postExplosionGasType = def.projectile.postExplosionGasType;
            ThingDef preExplosionSpawnThingDef = def.projectile.preExplosionSpawnThingDef;
            float preExplosionSpawnChance = def.projectile.preExplosionSpawnChance;
            int preExplosionSpawnThingCount = def.projectile.preExplosionSpawnThingCount;
            bool applyDamageToExplosionCellsNeighbors = def.projectile.applyDamageToExplosionCellsNeighbors;
            float explosionChanceToStartFire = def.projectile.explosionChanceToStartFire;
            bool explosionDamageFalloff = def.projectile.explosionDamageFalloff;
            float? direction = origin.AngleToFlat(destination);
            float expolosionPropagationSpeed = base.DamageDef.expolosionPropagationSpeed;
            float screenShakeFactor = def.projectile.screenShakeFactor;
            bool doExplosionVFX = def.projectile.doExplosionVFX;
            ThingDef preExplosionSpawnSingleThingDef = def.projectile.preExplosionSpawnSingleThingDef;
            ThingDef postExplosionSpawnSingleThingDef = def.projectile.postExplosionSpawnSingleThingDef;

            GenExplosion.DoExplosion(position, map, explosionRadius, damageDef, instigator, damageAmount, armorPenetration, soundExplode, weapon, projectile, thing, postExplosionSpawnThingDef, postExplosionSpawnChance, postExplosionSpawnThingCount, postExplosionGasType, null, 255, applyDamageToExplosionCellsNeighbors, preExplosionSpawnThingDef, preExplosionSpawnChance, preExplosionSpawnThingCount, explosionChanceToStartFire, explosionDamageFalloff, direction, null, null, doExplosionVFX, expolosionPropagationSpeed, 0f, doSoundEffects: true, postExplosionSpawnThingDefWater, screenShakeFactor, null, null, postExplosionSpawnSingleThingDef, preExplosionSpawnSingleThingDef);

            // 单次污垢生成
            if (def.projectile.explosionSpawnsSingleFilth && 
                def.projectile.filth != null && 
                def.projectile.filthCount.TrueMax > 0 && 
                Rand.Chance(def.projectile.filthChance) && 
                !base.Position.Filled(map))
            {
                FilthMaker.TryMakeFilth(base.Position, map, def.projectile.filth, def.projectile.filthCount.RandomInRange);
            }
        }
    }
}
