using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using static RimWorld.MechClusterSketch;

namespace DivineDiurganate
{
    /// <summary>
    /// 飞跃物体生成器Comp
    /// </summary>
    public class CompFlyOverGenerator : ThingComp
    {
        public CompProperties_FlyOverGenerator Props => 
            (CompProperties_FlyOverGenerator)props;

        // 在 CompFlyOverGenerator 类中添加以下字段和方法
        // 在类顶部添加字段
        // 在 Initialize 方法中添加
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            useCount = 0;
        }
        /// 修改 CanActivateNow 方法，添加 OberonAirShip 检查
        /// </summary>
        private bool CanActivateNow(out string reason)
        {
            reason = null;
            if (Find.TickManager.TicksGame < lastUseTick + Props.cooldownTicks)
            {
                int remainingTicks = lastUseTick + Props.cooldownTicks - Find.TickManager.TicksGame;
                reason = $"DD_Flyover_OnCooldown".Translate(remainingTicks.ToStringSecondsFromTicks());
                return false;
            }

            if (Props.useLimit > 0 && useCount >= Props.useLimit)
            {
                reason = $"DD_Flyover_UseLimitReached".Translate(useCount, Props.useLimit);
                return false;
            }

            var powerComp = parent.GetComp<CompPowerTrader>();
            if (powerComp != null && !powerComp.PowerOn)
            {
                reason = "DD_Flyover_NoPower".Translate();
                return false;
            }

            if (callJobState != CallJobState.None && callJobState != CallJobState.Completed && callJobState != CallJobState.Failed)
            {
                reason = "DD_Flyover_JobInProgress".Translate();
                return false;
            }

            return true;
        }
        /// <summary>
        /// 修改 CompTick 方法，定期检查 OberonAirShip 状态
        /// </summary>
        public override void CompTick()
        {
            base.CompTick();
            if (callJobState == CallJobState.InProgress)
            {
                UpdateCallJob();
            }

            if (callJobState == CallJobState.WaitingForPawn && Find.TickManager.TicksGame % 120 == 0)
            {
                FindPawnForCallJob();
            }
        }
        /// <summary>
        /// 修改 RecordUse 方法，考虑 OberonAirShip 的特殊逻辑
        /// </summary>
        private void RecordUse()
        {
            lastUseTick = Find.TickManager.TicksGame;

            useCount++;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastUseTick, "lastUseTick", -99999);
            Scribe_Values.Look(ref useCount, "useCount", 0);
            Scribe_Values.Look(ref callJobState, "callJobState", CallJobState.None);
        }
        /// <summary>
        /// 修改 Gizmo 描述，显示 OberonAirShip 状态
        /// </summary>
        private Gizmo CreateFlyOverGizmo()
        {
            Command_Action gizmo = new Command_Action
            {
                defaultLabel = Props.label,
                defaultDesc = GetGizmoDescription(),
                icon = ContentFinder<Texture2D>.Get(Props.iconPath, false),
                action = () => StartSelectionProcess()
            };

            if (!CanActivateNow(out string reason))
            {
                gizmo.Disable(reason);
            }

            return gizmo;
        }
        /// <summary>
        /// 获取 Gizmo 描述
        /// </summary>
        private string GetGizmoDescription()
        {
            string desc = Props.description;

            if (Props.useLimit > 0)
            {
                desc += $"\n{"DD_Flyover_Uses".Translate()}: {useCount}/{Props.useLimit}";
            }

            return desc;
        }

        // 状态跟踪
        private enum SelectionState
        {
            Idle,
            SelectingFirstPoint,
            SelectingSecondPoint
        }
        
        private SelectionState currentState = SelectionState.Idle;
        private IntVec3 firstPoint;
        private IntVec3 secondPoint;
        
        // 冷却和限制
        private int lastUseTick = -99999;
        private int useCount = 0;
        
        // 新增：呼叫任务状态
        private CallJobState callJobState = CallJobState.None;
        private IntVec3 storedEntryPoint;
        private IntVec3 storedExitPoint;
        private IntVec3 storedStartPoint;
        private IntVec3 storedEndPoint;
        private Pawn assignedPawn;
        private int workStartTick = -1;
        private int workTicksRemaining = 0;
        
        // 新增：呼叫任务状态枚举
        private enum CallJobState
        {
            None,                   // 无任务
            CalculatingPath,        // 计算路径中
            WaitingForPawn,         // 等待殖民者
            InProgress,             // 工作中
            Completed,              // 已完成
            Failed                  // 失败
        }
        
        // 新增：工作配置
        private const int WorkDurationTicks = 600; // 10秒工作时间
        private const float MaxPawnDistance = 30f; // 最大殖民者距离
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 基类Gizmo
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 添加生成飞跃物体的Gizmo
            yield return CreateFlyOverGizmo();
            
            // 新增：取消当前任务的Gizmo（如果有任务在进行）
            if (callJobState != CallJobState.None && callJobState != CallJobState.Completed && callJobState != CallJobState.Failed)
            {
                yield return CreateCancelJobGizmo();
            }
        }
        
        /// <summary>
        /// 创建取消任务Gizmo
        /// </summary>
        private Gizmo CreateCancelJobGizmo()
        {
            Command_Action gizmo = new Command_Action
            {
                defaultLabel = "DD_Flyover_CancelCallJob".Translate(),
                defaultDesc = "DD_Flyover_CancelCallJobDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
                action = () => CancelCurrentJob()
            };
            return gizmo;
        }
        
        /// <summary>
        /// 取消当前任务
        /// </summary>
        private void CancelCurrentJob()
        {
            if (callJobState != CallJobState.None)
            {
                if (assignedPawn != null)
                {
                    // 取消殖民者的工作
                    assignedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    assignedPawn = null;
                }
                
                ResetCallJob();
            }
        }
        
        /// <summary>
        /// 开始选择过程
        /// </summary>
        private void StartSelectionProcess()
        {
            if (!CanActivateNow(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 如果有任务正在进行，不能开始新任务
            if (callJobState != CallJobState.None && callJobState != CallJobState.Completed && callJobState != CallJobState.Failed)
            {
                Messages.Message("DD_Flyover_JobInProgress".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 开始选择第一个点
            currentState = SelectionState.SelectingFirstPoint;
            
            // 设置目标选择器
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate(LocalTargetInfo target)
                {
                    OnFirstPointSelected(target.Cell);
                },
                highlightAction: null,
                null,
                null
            );
            
            // 显示提示
            Messages.Message("DD_Flyover_SelectFirstPoint".Translate(), 
                MessageTypeDefOf.SilentInput);
        }

        /// <summary>
        /// 重置选择状态
        /// </summary>
        private void ResetSelection()
        {
            currentState = SelectionState.Idle;
            firstPoint = IntVec3.Invalid;
            secondPoint = IntVec3.Invalid;
        }

        /// <summary>
        /// 第一个点选择回调
        /// </summary>
        private void OnFirstPointSelected(IntVec3 cell)
        {
            if (!cell.InBounds(parent.Map))
            {
                ResetSelection();
                Messages.Message("DD_Flyover_PointOutOfBounds".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            firstPoint = cell;
            currentState = SelectionState.SelectingSecondPoint;
            
            // 开始选择第二个点
            Find.Targeter.BeginTargeting(
                new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetItems = false,
                    canTargetBuildings = false,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                delegate(LocalTargetInfo target)
                {
                    OnSecondPointSelected(target.Cell);
                },
                highlightAction: null,
                null,
                null
            );
            
            Messages.Message("DD_Flyover_SelectSecondPoint".Translate(), 
                MessageTypeDefOf.SilentInput);
        }
        
        /// <summary>
        /// 第二个点选择回调
        /// </summary>
        private void OnSecondPointSelected(IntVec3 cell)
        {
            if (!cell.InBounds(parent.Map))
            {
                ResetSelection();
                Messages.Message("DD_Flyover_PointOutOfBounds".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (cell == firstPoint)
            {
                ResetSelection();
                Messages.Message("DD_Flyover_PointsSame".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            secondPoint = cell;
            
            // 计算延长线与地图边界的交点
            CalculateAndStoreFlightPath();
        }
        
        /// <summary>
        /// 计算延长线并与地图边界相交，然后存储飞行路径
        /// </summary>
        private void CalculateAndStoreFlightPath()
        {
            Map map = parent.Map;
            
            // 计算延长线与地图边界的交点
            if (!CalculateMapIntersections(firstPoint, secondPoint, map, out storedEntryPoint, out storedExitPoint))
            {
                ResetSelection();
                Messages.Message("DD_Flyover_FailedCalculatePath".Translate(), 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 确定起始点和终点（更靠近第一个点的为起始点）
            float distance1 = firstPoint.DistanceTo(storedEntryPoint);
            float distance2 = firstPoint.DistanceTo(storedExitPoint);
            
            storedStartPoint = distance1 < distance2 ? storedEntryPoint : storedExitPoint;
            storedEndPoint = distance1 < distance2 ? storedExitPoint : storedEntryPoint;
            
            // 状态更新为计算完成，等待殖民者
            callJobState = CallJobState.CalculatingPath;
            
            // 开始寻找殖民者进行呼叫工作
            FindPawnForCallJob();
            
            ResetSelection();
        }
        
        /// <summary>
        /// 寻找殖民者进行呼叫工作
        /// </summary>
        private void FindPawnForCallJob()
        {
            Map map = parent.Map;
            if (map == null)
            {
                callJobState = CallJobState.Failed;
                return;
            }
            
            // 寻找最近的可用殖民者
            Pawn bestPawn = null;
            float bestDistance = float.MaxValue;
            
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                // 检查殖民者是否可用（没有重要工作，不是伤员等）
                if (pawn.Downed || pawn.Dead || pawn.InMentalState || pawn.IsPrisoner)
                {
                    continue;
                }
                
                // 计算距离
                float distance = pawn.Position.DistanceTo(parent.Position);
                if (distance < bestDistance && distance <= MaxPawnDistance)
                {
                    bestDistance = distance;
                    bestPawn = pawn;
                }
            }
            
            if (bestPawn != null)
            {
                AssignPawnToJob(bestPawn);
            }
            else
            {
                // 没有找到合适的殖民者
                callJobState = CallJobState.WaitingForPawn;
                Messages.Message("DD_Flyover_NoAvailablePawn".Translate(), 
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// 分配殖民者到呼叫工作
        /// </summary>
        private void AssignPawnToJob(Pawn pawn)
        {
            assignedPawn = pawn;
            callJobState = CallJobState.InProgress;
            workStartTick = Find.TickManager.TicksGame;
            workTicksRemaining = 180; // 3秒 = 180ticks

            try
            {
                // 创建飞机呼叫工作
                Job job = JobMaker.MakeJob(
                    DD_JobDefOf.DD_CallFlyOver,
                    parent
                );

                // 设置工作参数
                job.expiryInterval = workTicksRemaining + 120; // 额外2秒容错
                job.playerForced = true;

                // 分配工作给殖民者
                pawn.jobs.TryTakeOrderedJob(job);

                Messages.Message("DD_Flyover_PawnAssigned".Translate(pawn.NameShortColored),
                    MessageTypeDefOf.NeutralEvent);
            }
            catch (System.Exception ex)
            {
                Log.Error($"Failed to assign call job to pawn {pawn}: {ex}");
                callJobState = CallJobState.Failed;
                Messages.Message("DD_Flyover_FailedAssignJob".Translate(),
                    MessageTypeDefOf.NegativeEvent);
            }
        }

        /// <summary>
        /// 更新呼叫工作状态
        /// </summary>
        private void UpdateCallJob()
        {
            if (callJobState != CallJobState.InProgress)
                return;
            
            // 检查殖民者状态
            if (assignedPawn == null || assignedPawn.Dead || assignedPawn.Downed || 
                assignedPawn.Map != parent.Map || !assignedPawn.Spawned)
            {
                callJobState = CallJobState.Failed;
                Messages.Message("DD_Flyover_PawnUnavailable".Translate(), 
                    MessageTypeDefOf.NegativeEvent);
                return;
            }
            
            // 检查殖民者是否在附近
            float distance = assignedPawn.Position.DistanceTo(parent.Position);
            if (distance > MaxPawnDistance)
            {
                callJobState = CallJobState.Failed;
                Messages.Message("DD_Flyover_PawnTooFar".Translate(assignedPawn.NameShortColored), 
                    MessageTypeDefOf.NegativeEvent);
                return;
            }
            
            // 更新工作时间
            workTicksRemaining--;
            
            // 工作完成
            if (workTicksRemaining <= 0)
            {
                CompleteCallJob();
            }
        }
        
        /// <summary>
        /// 完成呼叫工作并生成飞船
        /// </summary>
        public void CompleteCallJob()
        {
            try
            {
                Map map = parent.Map;
                
                // 创建内容物容器（如果需要）
                ThingOwner<Thing> contents = null;
                if (Props.spawnContents && Props.contentThingDef != null && Props.contentCount > 0)
                {
                    contents = new ThingOwner<Thing>();
                    Thing thing = ThingMaker.MakeThing(Props.contentThingDef);
                    thing.stackCount = Mathf.Min(thing.def.stackLimit, Props.contentCount);
                    contents.TryAdd(thing);
                }
                
                // 创建飞跃物体
                FlyOver flyOver = FlyOver.MakeFlyOver(
                    Props.flyOverDef,
                    storedStartPoint,
                    storedEndPoint,
                    map,
                    Props.defaultSpeed,
                    Props.defaultAltitude,
                    contents,
                    casterPawn: assignedPawn  // 使用执行工作的殖民者作为施法者
                );
                
                if (flyOver != null)
                {
                    // 记录使用
                    RecordUse();
                    
                    callJobState = CallJobState.Completed;
                    
                    // 清理工作状态
                    assignedPawn = null;
                    workStartTick = -1;
                    workTicksRemaining = 0;
                }
                else
                {
                    callJobState = CallJobState.Failed;
                    Messages.Message("DD_Flyover_FailedCreate".Translate(), 
                        MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error creating flyover: {ex}");
                callJobState = CallJobState.Failed;
                Messages.Message("DD_Flyover_ErrorCreating".Translate(), 
                    MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// 重置呼叫任务状态
        /// </summary>
        private void ResetCallJob()
        {
            callJobState = CallJobState.None;
            assignedPawn = null;
            workStartTick = -1;
            workTicksRemaining = 0;
            storedEntryPoint = IntVec3.Invalid;
            storedExitPoint = IntVec3.Invalid;
            storedStartPoint = IntVec3.Invalid;
            storedEndPoint = IntVec3.Invalid;
        }
        
        /// <summary>
        /// 计算直线与地图边界的交点
        /// </summary>
        private bool CalculateMapIntersections(IntVec3 point1, IntVec3 point2, Map map, 
            out IntVec3 intersection1, out IntVec3 intersection2)
        {
            intersection1 = IntVec3.Invalid;
            intersection2 = IntVec3.Invalid;
            
            // 将点转换为Vector3以便计算
            Vector3 p1 = point1.ToVector3();
            Vector3 p2 = point2.ToVector3();
            
            // 计算方向向量
            Vector3 dir = (p2 - p1).normalized;
            
            // 地图边界
            float minX = 0f;
            float maxX = map.Size.x - 1;
            float minZ = 0f;
            float maxZ = map.Size.z - 1;
            
            List<Vector3> intersections = new List<Vector3>();
            
            // 计算与四条边界的交点
            // 1. 左边界 (x = minX)
            if (Mathf.Abs(dir.x) > 0.001f)
            {
                float tLeft = (minX - p1.x) / dir.x;
                Vector3 intersectLeft = p1 + dir * tLeft;
                if (intersectLeft.z >= minZ && intersectLeft.z <= maxZ)
                    intersections.Add(intersectLeft);
            }
            
            // 2. 右边界 (x = maxX)
            if (Mathf.Abs(dir.x) > 0.001f)
            {
                float tRight = (maxX - p1.x) / dir.x;
                Vector3 intersectRight = p1 + dir * tRight;
                if (intersectRight.z >= minZ && intersectRight.z <= maxZ)
                    intersections.Add(intersectRight);
            }
            
            // 3. 下边界 (z = minZ)
            if (Mathf.Abs(dir.z) > 0.001f)
            {
                float tBottom = (minZ - p1.z) / dir.z;
                Vector3 intersectBottom = p1 + dir * tBottom;
                if (intersectBottom.x >= minX && intersectBottom.x <= maxX)
                    intersections.Add(intersectBottom);
            }
            
            // 4. 上边界 (z = maxZ)
            if (Mathf.Abs(dir.z) > 0.001f)
            {
                float tTop = (maxZ - p1.z) / dir.z;
                Vector3 intersectTop = p1 + dir * tTop;
                if (intersectTop.x >= minX && intersectTop.x <= maxX)
                    intersections.Add(intersectTop);
            }
            
            // 我们需要两个交点（一个在p1之前，一个在p1之后）
            if (intersections.Count < 2)
                return false;
            
            // 计算每个交点到p1的距离（带符号，表示方向）
            List<(Vector3 point, float distance)> signedDistances = new List<(Vector3, float)>();
            foreach (Vector3 intersection in intersections)
            {
                Vector3 toIntersection = intersection - p1;
                float distance = toIntersection.magnitude;
                
                // 判断方向：如果点积为正，则是相同方向；如果为负，则是相反方向
                float dot = Vector3.Dot(toIntersection.normalized, dir);
                float signedDistance = dot > 0 ? distance : -distance;
                
                signedDistances.Add((intersection, signedDistance));
            }
            
            // 排序并找到最远的负距离和最远的正距离
            signedDistances.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            // 找到最远的负距离（p1之前最远的点）
            Vector3? farNegative = null;
            foreach (var (point, distance) in signedDistances)
            {
                if (distance < 0)
                {
                    farNegative = point;
                    break;
                }
            }
            
            // 找到最远的正距离（p1之后最远的点）
            Vector3? farPositive = null;
            for (int i = signedDistances.Count - 1; i >= 0; i--)
            {
                if (signedDistances[i].distance > 0)
                {
                    farPositive = signedDistances[i].point;
                    break;
                }
            }
            
            if (!farNegative.HasValue || !farPositive.HasValue)
                return false;
            
            intersection1 = farNegative.Value.ToIntVec3();
            intersection2 = farPositive.Value.ToIntVec3();
            
            return true;
        }
        public override void PostDraw()
        {
            base.PostDraw();
            
            // 如果正在选择，绘制视觉指示器
            if (currentState != SelectionState.Idle)
            {
                DrawSelectionState();
            }
            
            // 如果有任务在进行，绘制工作状态
            if (callJobState == CallJobState.InProgress && workTicksRemaining > 0)
            {
                DrawWorkProgress();
            }
        }
        
        /// <summary>
        /// 绘制选择状态
        /// </summary>
        private void DrawSelectionState()
        {
            // 绘制建筑周围的指示器
            Vector3 drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            
            // 绘制一个旋转的指示器
            float rotation = (float)Find.TickManager.TicksGame % 360f;
            Matrix4x4 matrix = default;
            matrix.SetTRS(drawPos, Quaternion.Euler(0f, rotation, 0f), 
                new Vector3(2f, 1f, 2f));
            
            Graphics.DrawMesh(MeshPool.plane10, matrix, 
                MaterialPool.MatFrom("UI/Overlays/TargetingCircle", 
                    ShaderDatabase.Transparent, Color.yellow), 0);
        }
        
        /// <summary>
        /// 绘制工作进度
        /// </summary>
        private void DrawWorkProgress()
        {
            // 在建筑上方绘制进度条
            Vector3 drawPos = parent.DrawPos;
            drawPos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.1f;
            
            float progress = 1f - (float)workTicksRemaining / WorkDurationTicks;
            
            // 绘制进度条背景
            Vector3 progressBarPos = drawPos + new Vector3(0, 0, 0.5f);
            float barWidth = 1.5f;
            float barHeight = 0.1f;
            
            // 绘制进度条填充
            Vector3 fillPos = progressBarPos + new Vector3(-barWidth/2 + progress * barWidth/2, 0, 0);
            float fillWidth = barWidth * progress;
            
            // 使用简单的材质绘制进度条
            Material progressMaterial = SolidColorMaterials.SimpleSolidColorMaterial(
                Color.Lerp(Color.red, Color.green, progress));
            
            Matrix4x4 fillMatrix = Matrix4x4.TRS(fillPos, Quaternion.identity, 
                new Vector3(fillWidth, 1f, barHeight));
            Graphics.DrawMesh(MeshPool.plane10, fillMatrix, progressMaterial, 0);
        }
        
        /// <summary>
        /// 获取剩余冷却时间（秒）
        /// </summary>
        public float RemainingCooldownSeconds
        {
            get
            {
                if (lastUseTick <= 0) return 0f;
                int remainingTicks = lastUseTick + Props.cooldownTicks - Find.TickManager.TicksGame;
                return Mathf.Max(0f, remainingTicks / 60f);
            }
        }
        
        /// <summary>
        /// 获取当前工作状态描述
        /// </summary>
        public string GetWorkStatusDescription()
        {
            switch (callJobState)
            {
                case CallJobState.None:
                    return "DD_Flyover_JobState_None".Translate();
                case CallJobState.CalculatingPath:
                    return "DD_Flyover_JobState_Calculating".Translate();
                case CallJobState.WaitingForPawn:
                    return "DD_Flyover_JobState_WaitingForPawn".Translate();
                case CallJobState.InProgress:
                    return "DD_Flyover_JobState_InProgress".Translate(
                        assignedPawn?.NameShortColored ?? "Unknown", 
                        workTicksRemaining.ToStringSecondsFromTicks());
                case CallJobState.Completed:
                    return "DD_Flyover_JobState_Completed".Translate();
                case CallJobState.Failed:
                    return "DD_Flyover_JobState_Failed".Translate();
                default:
                    return "DD_Flyover_JobState_Unknown".Translate();
            }
        }
        
        /// <summary>
        /// 获取存储的航道信息
        /// </summary>
        public string GetStoredPathInfo()
        {
            if (storedStartPoint == IntVec3.Invalid || storedEndPoint == IntVec3.Invalid)
                return "DD_Flyover_NoPathStored".Translate();
            
            return $"DD_Flyover_PathInfo".Translate(
                storedStartPoint.ToString(), storedEndPoint.ToString());
        }
    }
}
