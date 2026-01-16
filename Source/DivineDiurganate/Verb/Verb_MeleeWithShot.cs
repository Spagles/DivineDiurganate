using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 近战攻击时自动触发射击的Verb
    /// 继承自Verb_MeleeAttackDamage，在近战命中后自动发射霰弹
    /// </summary>
    public class Verb_MeleeWithShot : Verb_MeleeAttackDamage
    {
        /// <summary>
        /// 重写TryCastShot，在近战攻击后触发射击
        /// </summary>
        protected override bool TryCastShot()
        {
            // 先执行原版近战攻击
            bool meleeResult = base.TryCastShot();

            // 如果近战成功，触发射击
            if (meleeResult && this.currentTarget.Thing != null)
            {
                TriggerRangedShot(this.currentTarget);
            }

            return meleeResult;
        }

        /// <summary>
        /// 触发远程射击
        /// </summary>
        private void TriggerRangedShot(LocalTargetInfo target)
        {
            if (this.EquipmentSource == null) return;

            // 获取武器的射击配置
            MeleeWithShotExtension ext = MeleeWithShotExtension.Get(this.EquipmentSource.def);
            if (ext == null || ext.projectileDef == null) return;

            Pawn casterPawn = this.CasterPawn;
            if (casterPawn == null || casterPawn.Map == null) return;

            Map map = casterPawn.Map;
            ThingDef projectileDef = ext.projectileDef;

            // 获取扇形和弹丸配置
            ShotgunFanExtension fanExt = ShotgunFanExtension.Get(projectileDef);
            ShotgunExtension shotgunExt = ShotgunExtension.Get(projectileDef);

            int totalPellets = ext.pelletCount > 0 ? ext.pelletCount : shotgunExt.pelletCount;
            if (totalPellets <= 0) totalPellets = 1;

            float fanAngle = ext.fanAngle > 0 ? ext.fanAngle : fanExt.fanAngle;

            // 计算基准角度（从射手到目标）
            Vector3 casterPos = casterPawn.DrawPos;
            Vector3 targetPos = target.CenterVector3;
            float baseAngle = (targetPos - casterPos).AngleFlat();
            float range = ext.range > 0 ? ext.range : 10f;

            // 发射所有弹丸
            for (int i = 0; i < totalPellets; i++)
            {
                // 计算这颗弹丸的偏移角度
                float offsetAngle = CalculatePelletAngle(i, totalPellets, fanAngle);
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
                Projectile projectile = (Projectile)GenSpawn.Spawn(projectileDef, casterPawn.Position, map);

                // 发射弹丸
                if (projectile != null)
                {
                    projectile.Launch(
                        casterPawn,
                        casterPos,
                        new LocalTargetInfo(newTargetCell),
                        target,
                        ProjectileHitFlags.All,
                        preventFriendlyFire: ext.preventFriendlyFire,
                        this.EquipmentSource
                    );
                }
            }

            // 播放射击音效
            if (ext.soundCast != null)
            {
                ext.soundCast.PlayOneShot(new TargetInfo(casterPawn.Position, map));
            }

            // 播放枪口闪光
            if (ext.muzzleFlashScale > 0.01f)
            {
                FleckMaker.Static(casterPawn.Position, map, FleckDefOf.ShotFlash, ext.muzzleFlashScale);
            }
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

            float halfAngle = fanAngle / 2f;
            float step = fanAngle / (totalPellets - 1);
            float offset = -halfAngle + (pelletIndex * step);

            return offset;
        }
    }

    /// <summary>
    /// 近战射击配置扩展
    /// </summary>
    public class MeleeWithShotExtension : DefModExtension
    {
        /// <summary>
        /// 射击使用的弹丸Def
        /// </summary>
        public ThingDef projectileDef;

        /// <summary>
        /// 弹丸数量（0则使用projectileDef的配置）
        /// </summary>
        public int pelletCount = 0;

        /// <summary>
        /// 扇形角度（0则使用projectileDef的配置）
        /// </summary>
        public float fanAngle = 0f;

        /// <summary>
        /// 射程
        /// </summary>
        public float range = 10f;

        /// <summary>
        /// 是否防止友伤
        /// </summary>
        public bool preventFriendlyFire = true;

        /// <summary>
        /// 射击音效
        /// </summary>
        public SoundDef soundCast;

        /// <summary>
        /// 枪口闪光大小
        /// </summary>
        public float muzzleFlashScale = 0f;

        public static MeleeWithShotExtension Get(Def def)
        {
            return def.GetModExtension<MeleeWithShotExtension>();
        }
    }
}
