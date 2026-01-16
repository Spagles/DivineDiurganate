using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 扫射子弹扩展配置
    /// </summary>
    public class SweepingBullet_Extension : DefModExtension
    {
        /// <summary>
        /// 是否阻止友方伤害
        /// </summary>
        public bool preventFriendlyFire = true;

        /// <summary>
        /// 伤害衰减（每次穿透后的伤害乘数衰减）
        /// 0 = 无衰减，0.25 = 每次穿透损失25%伤害
        /// </summary>
        public float damageFalloff = 0f;

        /// <summary>
        /// 尾迹特效
        /// </summary>
        public FleckDef tailFleckDef;

        /// <summary>
        /// 特效延迟Tick数
        /// </summary>
        public int fleckDelayTicks = 5;

        /// <summary>
        /// 击中特效
        /// </summary>
        public EffecterDef impactEffecter;

        /// <summary>
        /// 是否穿透建筑物（如果为false，撞到建筑物会停止）
        /// </summary>
        public bool penetrateBuildings = false;
    }

    /// <summary>
    /// 扫射子弹 - 无限穿透Pawn，只有撞墙或到达最大射程才会消失
    /// </summary>
    public class Projectile_SweepingBullet : Bullet
    {
        // 已经伤害过的目标列表（防止重复伤害）
        private List<Thing> alreadyDamaged = new List<Thing>();

        // 穿透计数（用于伤害衰减计算）
        private int hitCounter = 0;

        // 上一Tick的位置（用于路径检测）
        private Vector3 lastTickPosition;

        // 特效相关
        private int fleckTickCounter = 0;

        // 获取扩展配置
        private SweepingBullet_Extension Props => def?.GetModExtension<SweepingBullet_Extension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hitCounter, "hitCounter", 0);
            Scribe_Collections.Look(ref alreadyDamaged, "alreadyDamaged", LookMode.Reference);
            Scribe_Values.Look(ref lastTickPosition, "lastTickPosition");

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

            // 合并友军伤害设置
            this.preventFriendlyFire = preventFriendlyFire || (Props?.preventFriendlyFire ?? true);
        }

        protected override void TickInterval(int delta)
        {
            // 保存移动前的位置
            Vector3 startPos = this.ExactPosition;

            // 调用原版逻辑（会移动弹丸，可能触发Impact）
            base.TickInterval(delta);

            if (this.Destroyed) return;

            // 获取移动后的位置
            Vector3 endPos = this.ExactPosition;

            // 检查路径上的所有目标并造成伤害
            CheckPathForDamage(startPos, endPos);

            // 检查是否撞到墙壁
            if (CheckWallCollision(endPos))
            {
                this.Impact(null);
                return;
            }

            // 处理尾迹特效
            HandleFleckEffects();

            this.lastTickPosition = endPos;
        }

        /// <summary>
        /// 处理尾迹特效
        /// </summary>
        private void HandleFleckEffects()
        {
            if (Props?.tailFleckDef == null) return;

            fleckTickCounter++;
            if (fleckTickCounter >= Props.fleckDelayTicks)
            {
                fleckTickCounter = 0;

                Map map = base.Map;
                if (map != null)
                {
                    FleckCreationData dataStatic = FleckMaker.GetDataStatic(
                        this.ExactPosition,
                        map,
                        Props.tailFleckDef,
                        1f
                    );
                    dataStatic.rotation = this.ExactRotation.eulerAngles.y;
                    map.flecks.CreateFleck(dataStatic);
                }
            }
        }

        /// <summary>
        /// 检查是否撞到墙壁
        /// </summary>
        private bool CheckWallCollision(Vector3 position)
        {
            Map map = this.Map;
            if (map == null) return true;

            IntVec3 cell = position.ToIntVec3();
            if (!cell.InBounds(map)) return true;

            // 检查是否有墙壁或不可通过的建筑
            Building building = cell.GetEdifice(map);
            if (building != null)
            {
                // 如果不允许穿透建筑，且建筑会阻挡
                if (Props?.penetrateBuildings != true)
                {
                    // 检查是否是实体墙壁或门
                    if (building.def.fillPercent > 0.5f || building.def.blockLight)
                    {
                        return true;
                    }
                }
            }

            // 检查地形是否可通过
            if (!cell.Standable(map) && cell.GetTerrain(map).passability == Traversability.Impassable)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查路径上的所有Pawn并造成伤害
        /// </summary>
        private void CheckPathForDamage(Vector3 startPos, Vector3 endPos)
        {
            if (startPos == endPos) return;

            Map map = this.Map;
            if (map == null) return;

            float distance = Vector3.Distance(startPos, endPos);
            Vector3 direction = (endPos - startPos).normalized;

            // 沿路径每0.5格检测一次（更精确的检测）
            for (float i = 0; i < distance; i += 0.5f)
            {
                Vector3 checkPos = startPos + direction * i;
                IntVec3 cell = checkPos.ToIntVec3();

                if (!cell.InBounds(map)) continue;

                // 创建列表副本，避免在遍历时集合被修改（TakeDamage可能导致Thing被移除）
                List<Thing> thingsInCell = map.thingGrid.ThingsListAt(cell);
                for (int j = thingsInCell.Count - 1; j >= 0; j--)
                {
                    if (j >= thingsInCell.Count) continue; // 安全检查
                    Thing thing = thingsInCell[j];
                    if (thing is Pawn pawn && pawn != this.launcher && !alreadyDamaged.Contains(pawn))
                    {
                        bool shouldDamage = ShouldDamagePawn(pawn);
                        if (shouldDamage)
                        {
                            ApplyDamageToPawn(pawn);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 判断是否应该对该Pawn造成伤害
        /// </summary>
        private bool ShouldDamagePawn(Pawn pawn)
        {
            // 目标必打
            if (this.intendedTarget.Thing == pawn)
            {
                return true;
            }

            // 敌人必打
            if (pawn.HostileTo(this.launcher))
            {
                return true;
            }

            // 没开防友伤才打队友
            if (!this.preventFriendlyFire)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 对Pawn造成伤害
        /// </summary>
        private void ApplyDamageToPawn(Pawn pawn)
        {
            float falloff = Props?.damageFalloff ?? 0f;
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
                this.intendedTarget.Thing
            );

            pawn.TakeDamage(dinfo);
            alreadyDamaged.Add(pawn);
            hitCounter++;

            // 触发击中特效
            if (Props?.impactEffecter != null && launcher?.Map != null)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(pawn), this.launcher);
                effecter.Cleanup();
            }
        }

        /// <summary>
        /// 重写Impact - Pawn时造成伤害但继续飞行，墙壁时销毁
        /// </summary>
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // 如果是被护盾阻挡，正常处理
            if (blockedByShield)
            {
                base.Impact(hitThing, blockedByShield);
                return;
            }

            // 如果hitThing是Pawn，造成伤害但不销毁子弹
            if (hitThing is Pawn pawn)
            {
                // 检查是否已经伤害过这个目标
                if (!alreadyDamaged.Contains(pawn) && ShouldDamagePawn(pawn))
                {
                    ApplyDamageToPawn(pawn);
                }
                // 不调用base.Impact，让子弹继续飞
                return;
            }

            // 如果是建筑物且允许穿透建筑
            if (hitThing is Building && Props?.penetrateBuildings == true)
            {
                return;
            }

            // 其他情况（撞墙、到达最大射程等），正常销毁
            base.Impact(hitThing, blockedByShield);
        }
    }
}
