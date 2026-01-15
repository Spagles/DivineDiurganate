using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 扇形射击Verb - 弹丸均匀分布在扇形区域内
    /// </summary>
    public class Verb_ShootShotgunFan : Verb_LaunchProjectile
    {
        protected override int ShotsPerBurst => this.verbProps.burstShotCount;

        public override void WarmupComplete()
        {
            base.WarmupComplete();
            Pawn pawn = this.currentTarget.Thing as Pawn;
            if (pawn != null && !pawn.Downed && this.CasterIsPawn && this.CasterPawn.skills != null)
            {
                float num = pawn.HostileTo(this.caster) ? 170f : 20f;
                float num2 = this.verbProps.AdjustedFullCycleTime(this, this.CasterPawn);
                this.CasterPawn.skills.Learn(SkillDefOf.Shooting, num * num2, false, false);
            }
        }

        protected override bool TryCastShot()
        {
            if (this.currentTarget.HasThing && this.currentTarget.Thing.Map != this.caster.Map)
            {
                return false;
            }

            ThingDef projectileDef = this.Projectile;
            if (projectileDef == null)
            {
                return false;
            }

            // 获取配置
            ShotgunFanExtension fanExt = ShotgunFanExtension.Get(projectileDef);
            ShotgunExtension shotgunExt = ShotgunExtension.Get(projectileDef);

            int totalPellets = shotgunExt.pelletCount;
            if (totalPellets <= 0) totalPellets = 1;

            // 计算基准角度（从射手到目标）
            Vector3 casterPos = this.caster.DrawPos;
            Vector3 targetPos = this.currentTarget.CenterVector3;
            float baseAngle = (targetPos - casterPos).AngleFlat();
            float range = this.verbProps.range;
            Map map = this.caster.Map;

            // 发射所有弹丸
            bool anySuccess = false;
            for (int i = 0; i < totalPellets; i++)
            {
                // 计算这颗弹丸的偏移角度
                float offsetAngle = CalculatePelletAngle(i, totalPellets, fanExt.fanAngle);
                float finalAngle = baseAngle + offsetAngle;

                // 根据角度计算新目标点
                Vector3 direction = new Vector3(
                    Mathf.Sin(finalAngle * Mathf.Deg2Rad),
                    0f,
                    Mathf.Cos(finalAngle * Mathf.Deg2Rad)
                );
                Vector3 newTargetPos = casterPos + direction * range;
                IntVec3 newTargetCell = newTargetPos.ToIntVec3();

                // 确保目标在地图内
                if (!newTargetCell.InBounds(map))
                {
                    newTargetCell = newTargetCell.ClampInsideMap(map);
                }

                // 创建弹丸
                Projectile projectile = (Projectile)GenSpawn.Spawn(projectileDef, this.caster.Position, map);

                // 发射弹丸
                if (projectile != null)
                {
                    projectile.Launch(
                        this.caster,
                        casterPos,
                        new LocalTargetInfo(newTargetCell),
                        this.currentTarget,  // 保留原始目标作为intended target
                        ProjectileHitFlags.All,
                        preventFriendlyFire: false,
                        this.EquipmentSource
                    );
                    anySuccess = true;
                }
            }

            // 记录射击次数
            if (anySuccess && this.CasterIsPawn)
            {
                this.CasterPawn.records.Increment(RecordDefOf.ShotsFired);
            }

            // 播放射击音效
            if (this.verbProps.soundCast != null)
            {
                this.verbProps.soundCast.PlayOneShot(new TargetInfo(this.caster.Position, map));
            }

            // 播放枪口闪光
            if (this.verbProps.muzzleFlashScale > 0.01f)
            {
                FleckMaker.Static(this.caster.Position, map, FleckDefOf.ShotFlash, this.verbProps.muzzleFlashScale);
            }

            return anySuccess;
        }

        /// <summary>
        /// 计算弹丸在扇形中的角度偏移
        /// </summary>
        private float CalculatePelletAngle(int pelletIndex, int totalPellets, float fanAngle)
        {
            if (totalPellets <= 1)
            {
                return 0f;
            }

            // 均匀分布在扇形范围内
            // 例如：6颗弹丸，30度扇形
            // 角度分布：-15, -9, -3, 3, 9, 15
            float halfAngle = fanAngle / 2f;
            float step = fanAngle / (totalPellets - 1);
            float offset = -halfAngle + (pelletIndex * step);

            return offset;
        }
    }

    /// <summary>
    /// 扇形射击配置扩展
    /// </summary>
    public class ShotgunFanExtension : DefModExtension
    {
        /// <summary>
        /// 扇形展开角度（度）
        /// </summary>
        public float fanAngle = 30f;

        public static ShotgunFanExtension Get(Def def)
        {
            return def.GetModExtension<ShotgunFanExtension>() ?? defaultValues;
        }

        private static readonly ShotgunFanExtension defaultValues = new ShotgunFanExtension();
    }
}
