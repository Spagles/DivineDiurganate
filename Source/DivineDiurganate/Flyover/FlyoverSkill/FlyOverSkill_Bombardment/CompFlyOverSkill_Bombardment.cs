using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 轰炸技能 - 选择两个点，从起始点到终点按次序呼叫 Skyfaller
    /// </summary>
    public class CompFlyOverSkill_Bombardment : CompFlyOverSkillBase
    {
        // 轰炸状态
        private BombardmentState currentState = BombardmentState.Idle;
        private List<IntVec3> targetCells = new List<IntVec3>();
        private List<BombardmentRow> bombardmentRows = new List<BombardmentRow>();
        private IntVec3 bombardmentCenter;
        private Vector3 bombardmentDirection; // 轰炸前进方向
        private int currentRowIndex = 0;
        private int currentCellIndex = 0;
        private int warmupTicksRemaining = 0;
        private int nextBombardmentTick = 0;
        
        // 视觉效果
        private Effecter areaEffecter;
        
        // 预览状态
        private List<IntVec3> currentPreviewCells = new List<IntVec3>();

        /// <summary>
        /// 获取技能属性
        /// </summary>
        public CompProperties_FlyOverSkill_Bombardment BombardmentProps
        {
            get
            {
                return props as CompProperties_FlyOverSkill_Bombardment;
            }
        }

        /// <summary>
        /// 激活技能（开始目标选择）
        /// </summary>
        public override void Activate()
        {
            if (!CanUseNow(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return;
            }

            // 开始双点选择
            StartTwoPointsSelection();
        }

        /// <summary>
        /// 双点选择完成回调
        /// </summary>
        protected override void OnTwoPointsSelected(IntVec3 point1, IntVec3 point2)
        {
            // 验证选择的点
            if (!ValidatePoints(point1, point2, out string error))
            {
                Messages.Message(error, MessageTypeDefOf.RejectInput);
                return;
            }

            try
            {
                // 计算轰炸区域和方向（基于两个目标点）
                CalculateBombardmentArea(point1, point2);
                
                // 选择目标格子
                SelectTargetCells();
                
                // 组织成排
                OrganizeTargetCellsIntoRows();
                
                // 开始前摇
                StartWarmup();
                
                // 记录技能使用
                base.Execute();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FlyOver Bombardment] Error starting bombardment: {ex}");
            }
        }

        /// <summary>
        /// 验证选择的点
        /// </summary>
        private bool ValidatePoints(IntVec3 point1, IntVec3 point2, out string error)
        {
            error = null;
            
            // 检查是否在地图内
            Map map = Find.CurrentMap;
            if (map == null)
            {
                error = "DD_FlyoverSkillNoMap".Translate();
                return false;
            }
            
            if (!point1.InBounds(map) || !point2.InBounds(map))
            {
                error = "DD_FlyoverSkilPointNotInBounds".Translate();
                return false;
            }
            
            // 检查是否相同点
            if (point1 == point2)
            {
                error = "DD_FlyoverSkilPointsDifferent".Translate();
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 计算轰炸区域和方向（基于两个目标点）
        /// </summary>
        private void CalculateBombardmentArea(IntVec3 startCell, IntVec3 directionCell)
        {
            bombardmentCenter = startCell;
            
            // 计算轰炸方向（从起点指向方向点）
            Vector3 direction = (directionCell.ToVector3() - startCell.ToVector3()).normalized;
            
            // 如果方向为零向量，使用默认方向
            if (direction == Vector3.zero)
            {
                direction = Vector3.forward;
            }
            
            bombardmentDirection = direction;
        }

        /// <summary>
        /// 选择目标格子
        /// </summary>
        private void SelectTargetCells()
        {
            // 计算轰炸区域的所有单元格
            var areaCells = CalculateBombardmentAreaCells(bombardmentCenter, bombardmentDirection);
            
            var selectedCells = new List<IntVec3>();
            var missedCells = new List<IntVec3>();
            
            // 根据概率选择目标格子
            foreach (var cell in areaCells)
            {
                if (Rand.Value <= BombardmentProps.targetSelectionChance)
                {
                    selectedCells.Add(cell);
                }
                else
                {
                    missedCells.Add(cell);
                }
            }
            
            // 应用最小/最大限制
            if (selectedCells.Count < BombardmentProps.minTargetCells)
            {
                // 补充不足的格子
                int needed = BombardmentProps.minTargetCells - selectedCells.Count;
                if (missedCells.Count > 0)
                {
                    selectedCells.AddRange(missedCells.InRandomOrder().Take(Mathf.Min(needed, missedCells.Count)));
                }
            }
            else if (selectedCells.Count > BombardmentProps.maxTargetCells)
            {
                // 随机移除多余的格子
                selectedCells = selectedCells.InRandomOrder().Take(BombardmentProps.maxTargetCells).ToList();
            }
            
            targetCells = selectedCells;
        }

        /// <summary>
        /// 计算轰炸区域的所有单元格（基于起点和方向）
        /// </summary>
        private List<IntVec3> CalculateBombardmentAreaCells(IntVec3 startCell, Vector3 direction)
        {
            var areaCells = new List<IntVec3>();
            Map map = Find.CurrentMap;
            
            Vector3 start = startCell.ToVector3();
            
            // 计算垂直于轰炸方向的方向（宽度方向）
            Vector3 perpendicularDirection = new Vector3(-direction.z, 0, direction.x).normalized;
            
            float halfWidth = BombardmentProps.bombardmentWidth * 0.5f;
            float totalLength = BombardmentProps.bombardmentLength;
            
            // 使用浮点步进计算所有单元格
            int widthSteps = Mathf.Max(1, BombardmentProps.bombardmentWidth);
            int lengthSteps = Mathf.Max(1, BombardmentProps.bombardmentLength);
            
            for (int l = 0; l <= lengthSteps; l++)
            {
                float lengthProgress = (float)l / lengthSteps;
                float lengthOffset = Mathf.Lerp(0, totalLength, lengthProgress);
                
                for (int w = 0; w <= widthSteps; w++)
                {
                    float widthProgress = (float)w / widthSteps;
                    float widthOffset = Mathf.Lerp(-halfWidth, halfWidth, widthProgress);
                    
                    // 计算单元格位置
                    Vector3 cellPos = start + direction * lengthOffset + perpendicularDirection * widthOffset;
                    
                    IntVec3 cell = new IntVec3(
                        Mathf.RoundToInt(cellPos.x),
                        Mathf.RoundToInt(cellPos.y),
                        Mathf.RoundToInt(cellPos.z)
                    );
                    
                    if (cell.InBounds(map) && !areaCells.Contains(cell))
                    {
                        areaCells.Add(cell);
                    }
                }
            }
            
            return areaCells;
        }

        /// <summary>
        /// 重新组织目标格子成排，确保正确的渐进顺序
        /// </summary>
        private void OrganizeTargetCellsIntoRows()
        {
            bombardmentRows.Clear();
            
            // 计算垂直于轰炸方向的方向（宽度方向）
            Vector3 perpendicularDirection = new Vector3(-bombardmentDirection.z, 0, bombardmentDirection.x).normalized;
            
            // 根据轰炸前进方向将格子分组到不同的排
            var rows = new Dictionary<int, List<IntVec3>>();
            
            foreach (var cell in targetCells)
            {
                // 计算格子相对于轰炸起点的"行索引"（在轰炸前进方向上的投影）
                Vector3 cellVector = cell.ToVector3() - bombardmentCenter.ToVector3();
                float dotProduct = Vector3.Dot(cellVector, bombardmentDirection);
                int rowIndex = Mathf.RoundToInt(dotProduct);
                
                if (!rows.ContainsKey(rowIndex))
                {
                    rows[rowIndex] = new List<IntVec3>();
                }
                rows[rowIndex].Add(cell);
            }
            
            // 按照轰炸方向正确排序行索引
            // 从起点（行索引=0）开始，向轰炸方向前进
            var sortedRowIndices = rows.Keys.OrderBy(x => x).ToList();
            
            foreach (var rowIndex in sortedRowIndices)
            {
                // 在每排内按照宽度方向正确排序
                // 从轰炸区域的一侧到另一侧
                var sortedCells = rows[rowIndex].OrderBy(cell => 
                {
                    Vector3 cellVector = cell.ToVector3() - bombardmentCenter.ToVector3();
                    return Vector3.Dot(cellVector, perpendicularDirection);
                }).ToList();
                
                bombardmentRows.Add(new BombardmentRow
                {
                    rowIndex = rowIndex,
                    cells = sortedCells
                });
            }
        }

        /// <summary>
        /// 开始前摇
        /// </summary>
        private void StartWarmup()
        {
            currentState = BombardmentState.Warmup;
            warmupTicksRemaining = BombardmentProps.warmupTicks;
            currentRowIndex = 0;
            currentCellIndex = 0;
        }

        /// <summary>
        /// 更新前摇
        /// </summary>
        private void UpdateWarmup()
        {
            warmupTicksRemaining--;
            
            if (warmupTicksRemaining <= 0)
            {
                // 前摇结束，开始轰炸
                currentState = BombardmentState.Bombarding;
                nextBombardmentTick = Find.TickManager.TicksGame;
            }
        }

        /// <summary>
        /// 更新轰炸
        /// </summary>
        private void UpdateBombardment()
        {
            if (Find.TickManager.TicksGame < nextBombardmentTick)
                return;
            
            if (currentRowIndex >= bombardmentRows.Count)
            {
                // 所有排都轰炸完毕
                currentState = BombardmentState.Completed;
                return;
            }
            
            var currentRow = bombardmentRows[currentRowIndex];
            
            if (currentCellIndex >= currentRow.cells.Count)
            {
                // 当前排轰炸完毕，移动到下一排
                currentRowIndex++;
                currentCellIndex = 0;
                nextBombardmentTick = Find.TickManager.TicksGame + BombardmentProps.rowDelayTicks;
                return;
            }
            
            // 轰炸当前格子
            var targetCell = currentRow.cells[currentCellIndex];
            LaunchBombardment(targetCell);
            
            currentCellIndex++;
            nextBombardmentTick = Find.TickManager.TicksGame + BombardmentProps.impactDelayTicks;
        }

        /// <summary>
        /// 发起轰炸
        /// </summary>
        private void LaunchBombardment(IntVec3 targetCell)
        {
            try
            {
                Map map = Find.CurrentMap;
                
                if (BombardmentProps.skyfallerDef != null)
                {
                    // 使用 Skyfaller
                    Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(BombardmentProps.skyfallerDef);
                    
                    // 设置适当的起始位置（从空中落下）
                    IntVec3 spawnCell = new IntVec3(targetCell.x, 0, targetCell.z);
                    
                    // 生成 Skyfaller
                    GenSpawn.Spawn(skyfaller, spawnCell, map);
                }
                else if (BombardmentProps.projectileDef != null)
                {
                    // 使用抛射体作为备用
                    LaunchProjectileAt(targetCell);
                }
                else
                {
                    Log.Warning($"[FlyOver Bombardment] No skyfaller or projectile defined for bombardment");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[FlyOver Bombardment] Error launching bombardment at {targetCell}: {ex}");
            }
        }

        /// <summary>
        /// 发射抛射体
        /// </summary>
        private void LaunchProjectileAt(IntVec3 targetCell)
        {
            Map map = Find.CurrentMap;
            
            // 从上方发射抛射体
            IntVec3 spawnCell = new IntVec3(targetCell.x, 0, targetCell.z);
            Vector3 spawnPos = spawnCell.ToVector3() + new Vector3(0, 20f, 0); // 从高空发射
            
            Projectile projectile = (Projectile)GenSpawn.Spawn(BombardmentProps.projectileDef, spawnCell, map);
            if (projectile != null)
            {
                projectile.Launch(
                    parent, // 使用战机组作为发射者
                    spawnPos,
                    new LocalTargetInfo(targetCell),
                    new LocalTargetInfo(targetCell),
                    ProjectileHitFlags.All,
                    false
                );
            }
        }

        /// <summary>
        /// 清理
        /// </summary>
        private void Cleanup()
        {
            // 清理效果器
            areaEffecter?.Cleanup();
            areaEffecter = null;
            
            // 重置状态
            currentState = BombardmentState.Idle;
            targetCells.Clear();
            bombardmentRows.Clear();
            currentPreviewCells.Clear();
        }

        /// <summary>
        /// 确保地图位置安全
        /// </summary>
        private IntVec3 GetSafeMapPosition(IntVec3 pos, Map map)
        {
            if (map == null) return pos;
            
            pos.x = Mathf.Clamp(pos.x, 0, map.Size.x - 1);
            pos.z = Mathf.Clamp(pos.z, 0, map.Size.z - 1);
            
            return pos;
        }

        /// <summary>
        /// 组件每帧更新
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            
            if (currentState == BombardmentState.Idle)
                return;

            switch (currentState)
            {
                case BombardmentState.Warmup:
                    UpdateWarmup();
                    break;
                    
                case BombardmentState.Bombarding:
                    UpdateBombardment();
                    break;
                    
                case BombardmentState.Completed:
                    Cleanup();
                    break;
            }
        }

        /// <summary>
        /// 绘制技能效果预览（当选择目标时）
        /// </summary>
        public void DrawEffectPreview(LocalTargetInfo target)
        {
            if (firstTargetPoint == IntVec3.Invalid)
                return;

            try
            {
                // 如果正在选择第二个目标（方向点），则显示轰炸区域预览
                if (firstTargetPoint.IsValid && target.IsValid)
                {
                    // 动态计算轰炸区域（基于第一个目标和当前鼠标位置）
                    CalculateDynamicBombardmentArea(firstTargetPoint, target.Cell);
                }
            }
            catch (System.Exception)
            {
                // 忽略预览绘制错误
            }
        }

        /// <summary>
        /// 基于两个目标点动态计算轰炸区域
        /// </summary>
        private void CalculateDynamicBombardmentArea(IntVec3 startCell, IntVec3 directionCell)
        {
            Map map = Find.CurrentMap;
            
            // 计算轰炸方向（从起点指向方向点）
            Vector3 direction = (directionCell.ToVector3() - startCell.ToVector3()).normalized;
            
            // 如果方向为零向量，使用默认方向
            if (direction == Vector3.zero)
            {
                direction = Vector3.forward;
            }
            
            // 计算轰炸区域的所有单元格
            currentPreviewCells = CalculateBombardmentAreaCells(startCell, direction);
        }

        /// <summary>
        /// 获取技能状态描述
        /// </summary>
        public override string GetStatusDescription()
        {
            var baseDesc = base.GetStatusDescription();
            
            if (baseDesc != "Ready")
                return baseDesc;
            
            return "Select two points to define bombardment area and direction";
        }

        /// <summary>
        /// 获取技能描述（包含轰炸参数）
        /// </summary>
        public override string GetCooldownDescription()
        {
            string desc = base.GetCooldownDescription();
            
            // 添加轰炸参数信息
            desc += $"\nArea: {BombardmentProps.bombardmentWidth}x{BombardmentProps.bombardmentLength} cells";
            desc += $"\nTargets: {BombardmentProps.minTargetCells}-{BombardmentProps.maxTargetCells} cells";
            
            return desc;
        }

        /// <summary>
        /// 序列化数据
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref currentState, "currentState", BombardmentState.Idle);
            Scribe_Collections.Look(ref targetCells, "targetCells", LookMode.Value);
            Scribe_Values.Look(ref currentRowIndex, "currentRowIndex", 0);
            Scribe_Values.Look(ref currentCellIndex, "currentCellIndex", 0);
            Scribe_Values.Look(ref warmupTicksRemaining, "warmupTicksRemaining", 0);
            Scribe_Values.Look(ref nextBombardmentTick, "nextBombardmentTick", 0);
        }
    }

    /// <summary>
    /// 轰炸状态枚举
    /// </summary>
    public enum BombardmentState
    {
        Idle,
        Warmup,
        Bombarding,
        Completed
    }

    /// <summary>
    /// 轰炸排数据结构
    /// </summary>
    public struct BombardmentRow
    {
        public int rowIndex;
        public List<IntVec3> cells;
    }
}
