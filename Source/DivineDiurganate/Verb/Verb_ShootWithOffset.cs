using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    // 射击偏移模式（简化版）
    public enum OffsetMode
    {
        Cyclic,     // 循环模式：按offsets列表循环
        Random      // 随机模式：每次随机选择偏移
    }
    
    // 射击偏移模组扩展
    public class ModExtension_ShootWithOffset : DefModExtension
    {
        public List<Vector2> offsets = new List<Vector2>();
        public OffsetMode offsetMode = OffsetMode.Cyclic; // 默认循环模式
        
        public Vector2 GetOffsetFor(int index)
        {
            if (this.offsets.NullOrEmpty())
            {
                return Vector2.zero;
            }
            
            switch (offsetMode)
            {
                case OffsetMode.Random:
                    return offsets[Rand.Range(0, offsets.Count)];
                    
                default: // Cyclic
                    int index2 = index % this.offsets.Count;
                    return this.offsets[index2];
            }
        }
    }
    
    // 射击偏移计数器组件
    public class CompShootOffsetCounter : ThingComp
    {
        private int shotCount = 0;
        private int lastShotTick = -1;
        
        public int ShotCount
        {
            get => shotCount;
            set => shotCount = value;
        }
        
        public int LastShotTick
        {
            get => lastShotTick;
            set => lastShotTick = value;
        }
        
        // 增加射击计数
        public void IncrementShotCount()
        {
            shotCount++;
            lastShotTick = Find.TickManager.TicksGame;
        }
        
        // 重置射击计数
        public void ResetShotCount()
        {
            shotCount = 0;
            lastShotTick = -1;
        }
        
        // 检查是否需要重置（比如距离上次射击太久）
        public bool ShouldReset()
        {
            if (lastShotTick < 0)
                return false;
                
            int currentTick = Find.TickManager.TicksGame;
            int ticksSinceLastShot = currentTick - lastShotTick;
            
            // 如果距离上次射击超过5秒（300 ticks），重置计数
            return ticksSinceLastShot > 300;
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shotCount, "shotCount", 0);
            Scribe_Values.Look(ref lastShotTick, "lastShotTick", -1);
        }
        
        public override string CompInspectStringExtra()
        {
            if (Prefs.DevMode)
            {
                return $"射击计数: {shotCount}";
            }
            return null;
        }
    }
    
    public class CompProperties_ShootOffsetCounter : CompProperties
    {
        public CompProperties_ShootOffsetCounter()
        {
            this.compClass = typeof(CompShootOffsetCounter);
        }
    }
    
    // 主射击类
    public class Verb_ShootWithOffset : Verb_Shoot
    {  
        protected override bool TryCastShot()
        {
            bool num = BaseTryCastShot();
            if (num && CasterIsPawn)
            {
                CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            }

            return num;
        }
        
        protected bool BaseTryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }

            ThingDef projectile = Projectile;
            if (projectile == null)
            {
                return false;
            }

            ShootLine resultingLine;
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }

            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
            }

            lastShotTick = Find.TickManager.TicksGame;
            Thing manningPawn = caster;
            Thing equipmentSource = base.EquipmentSource;
            CompMannable compMannable = caster.TryGetComp<CompMannable>();
            if (compMannable?.ManningPawn != null)
            {
                manningPawn = compMannable.ManningPawn;
                equipmentSource = caster;
            }

            Vector3 drawPos = caster.DrawPos;
            PrepareShotCounter(equipmentSource);
            drawPos = ApplyProjectileOffset(drawPos, equipmentSource);
            IncrementShotCounter(equipmentSource);
            
            Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, resultingLine.Source, caster.Map);
            if (equipmentSource.TryGetComp(out CompUniqueWeapon comp))
            {
                foreach (WeaponTraitDef item in comp.TraitsListForReading)
                {
                    if (item.damageDefOverride != null)
                    {
                        projectile2.damageDefOverride = item.damageDefOverride;
                    }

                    if (!item.extraDamages.NullOrEmpty())
                    {
                        Projectile projectile3 = projectile2;
                        if (projectile3.extraDamages == null)
                        {
                            projectile3.extraDamages = new List<ExtraDamage>();
                        }

                        projectile2.extraDamages.AddRange(item.extraDamages);
                    }
                }
            }

            if (verbProps.ForcedMissRadius > 0.5f)
            {
                float num = verbProps.ForcedMissRadius;
                if (manningPawn is Pawn pawn)
                {
                    num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
                }

                float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.Cell - caster.Position);
                if (num2 > 0.5f)
                {
                    IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
                    if (forcedMissTarget != currentTarget.Cell)
                    {
                        ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
                        if (Rand.Chance(0.5f))
                        {
                            projectileHitFlags = ProjectileHitFlags.All;
                        }

                        if (!canHitNonTargetPawnsNow)
                        {
                            projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
                        }

                        projectile2.Launch(manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
                        return true;
                    }
                }
            }

            ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
            Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
            ThingDef targetCoverDef = randomCoverToMissInto?.def;
            if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
            {
                bool flyOverhead = projectile2?.def?.projectile != null && projectile2.def.projectile.flyOverhead;
                resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, flyOverhead, caster.Map);
                ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
                if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
                {
                    projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
                }

                projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }

            if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
            {
                ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
                if (canHitNonTargetPawnsNow)
                {
                    projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
                }

                projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
                return true;
            }

            ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
            if (canHitNonTargetPawnsNow)
            {
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
            }

            if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full)
            {
                projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
            }
            if (currentTarget.Thing != null)
            {
                projectile2.Launch(manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
            }
            else
            {
                projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
            }
            return true;
        }

        private void PrepareShotCounter(Thing equipmentSource)
        {
            if (equipmentSource == null) return;
            
            var counter = equipmentSource.TryGetComp<CompShootOffsetCounter>();
            if (counter != null)
            {
                // 检查是否需要重置
                if (counter.ShouldReset())
                {
                    counter.ResetShotCount();
                }
            }
        }

        private void IncrementShotCounter(Thing equipmentSource)
        {
            equipmentSource?.TryGetComp<CompShootOffsetCounter>()?.IncrementShotCount();
        }

        private Vector3 ApplyProjectileOffset(Vector3 originalDrawPos, Thing equipmentSource)
        {
            if (equipmentSource != null)
            {
                // 获取投射物偏移的模组扩展
                ModExtension_ShootWithOffset offsetExtension =
                    equipmentSource.def.GetModExtension<ModExtension_ShootWithOffset>();

                if (offsetExtension != null && offsetExtension.offsets != null && offsetExtension.offsets.Count > 0)
                {
                    // 获取射击计数
                    int shotIndex = GetShotIndex(equipmentSource);
                    
                    // 计算从发射者到目标的角度
                    Vector3 targetPos = currentTarget.CenterVector3;
                    Vector3 casterPos = caster.DrawPos;
                    float rimworldAngle = targetPos.AngleToFlat(casterPos);

                    // 将RimWorld角度转换为适合偏移计算的角度
                    float correctedAngle = ConvertRimWorldAngleToOffsetAngle(rimworldAngle);

                    // 获取偏移并旋转到正确方向
                    Vector2 offset = offsetExtension.GetOffsetFor(shotIndex);
                    Vector2 rotatedOffset = offset.RotatedBy(correctedAngle);

                    // 将2D偏移转换为3D并应用到绘制位置
                    originalDrawPos += new Vector3(rotatedOffset.x, 0f, rotatedOffset.y);
                }
            }

            return originalDrawPos;
        }
        
        // 获取射击索引
        private int GetShotIndex(Thing equipmentSource)
        {
            // 优先使用组件的计数
            var counter = equipmentSource.TryGetComp<CompShootOffsetCounter>();
            if (counter != null)
            {
                return counter.ShotCount;
            }
            
            // 如果没有组件，回退到连发计数（非连发武器默认为0）
            return GetBurstShotsLeft();
        }

        /// <summary>
        /// 获取当前连发射击剩余次数
        /// </summary>
        /// <returns>连发射击剩余次数</returns>
        private int GetBurstShotsLeft()
        {
            if (burstShotsLeft >= 0)
            {
                return (int)burstShotsLeft;
            }
            return 0;
        }

        /// <summary>
        /// 将RimWorld角度转换为偏移计算用的角度
        /// RimWorld使用顺时针角度系统，需要转换为标准的数学角度系统
        /// </summary>
        /// <param name="rimworldAngle">RimWorld角度</param>
        /// <returns>转换后的角度</returns>
        private float ConvertRimWorldAngleToOffsetAngle(float rimworldAngle)
        {
            // RimWorld角度：0°=东，90°=北，180°=西，270°=南
            // 转换为：0°=东，90°=南，180°=西，270°=北
            return -rimworldAngle - 90f;
        }
    }
}
