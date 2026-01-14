using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    // 地形覆盖配置的 ModExtension
    public class DamageDefExtension_TerrainCover : DefModExtension
    {
        // 要生成的地形定义
        public TerrainDef terrainToSpawn;

        // 生成概率（0-1）
        public float terrainChance = 1f;

        // 检查特定单元格是否允许生成地形
        public bool CanAffectCell(IntVec3 cell, Map map, out string reason)
        {
            reason = null;

            if (!cell.InBounds(map))
            {
                reason = "Cell out of bounds";
                return false;
            }

            // 检查地形类型
            TerrainDef currentTerrain = cell.GetTerrain(map);

            // 检查是否可以构建地形
            if (!GenConstruct.CanBuildOnTerrain(terrainToSpawn, cell, map, Rot4.North))
            {
                reason = "Cannot build on terrain";
                return false;
            }

            return true;
        }
    }

    public class DamageWorker_ExplosionWithTerrain : DamageWorker_AddInjury
    {
        // 获取地形覆盖扩展
        private DamageDefExtension_TerrainCover GetTerrainCoverExtension(DamageDef damageDef)
        {
            if (damageDef?.modExtensions == null)
                return null;

            return damageDef.modExtensions
                .OfType<DamageDefExtension_TerrainCover>()
                .FirstOrDefault();
        }

        public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
        {
            base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes);
            var terrainCover = GetTerrainCoverExtension(explosion.damType);
            // 处理地形覆盖
            ProcessTerrainCover(explosion, c, terrainCover);
        }

        // 处理地形覆盖
        private void ProcessTerrainCover(Explosion explosion, IntVec3 cellsToAffect, DamageDefExtension_TerrainCover terrainCover)
        {
            if (explosion.Map == null)
                return;

            Map map = explosion.Map;

            ApplyTerrainCoverToCell(cellsToAffect, map, terrainCover, explosion);
        }

        // 对单个单元格应用地形覆盖
        public void ApplyTerrainCoverToCell(IntVec3 cell, Map map,
            DamageDefExtension_TerrainCover terrainCover, Explosion explosion = null)
        {
            // 检查单元格是否可影响
            if (!terrainCover.CanAffectCell(cell, map, out string reason))
            {
                return;
            }

            TerrainDef currentTerrain = cell.GetTerrain(map);

            // 检查框架
            Frame frame = cell.GetFirstThing<Frame>(map);

            // 检查植物（特别是大型树木）
            Plant plant = cell.GetPlant(map);
            if (plant != null && plant.def.plant.treeCategory == TreeCategory.Super)
            {
                return;
            }

            // 尝试设置地形
            if (GenConstruct.CanBuildOnTerrain(terrainCover.terrainToSpawn, cell, map, Rot4.North))
            {
                // 销毁框架（如果需要）
                if (frame != null)
                {
                    frame.Destroy(DestroyMode.Vanish);
                }

                // 保存原始地形（用于临时覆盖）
                TerrainDef originalTerrain = map.terrainGrid.TerrainAt(cell);

                // 设置新地形
                map.terrainGrid.SetTerrain(cell, terrainCover.terrainToSpawn);
            }
        }
    }
}
