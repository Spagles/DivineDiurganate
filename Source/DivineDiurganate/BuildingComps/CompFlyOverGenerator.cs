using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DivineDiurganate
{
    /// <summary>
    /// 飞跃物体生成器Comp
    /// </summary>
    public class CompFlyOverGenerator : ThingComp
    {
        public CompProperties_FlyOverGenerator Props => 
            (CompProperties_FlyOverGenerator)props;
        
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
        
        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            useCount = 0;
        }
        
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 基类Gizmo
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 添加生成飞跃物体的Gizmo
            yield return CreateFlyOverGizmo();
        }
        
        /// <summary>
        /// 创建飞跃物体生成Gizmo
        /// </summary>
        private Gizmo CreateFlyOverGizmo()
        {
            Command_Action gizmo = new Command_Action
            {
                defaultLabel = Props.label.Translate(),
                defaultDesc = Props.description.Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.iconPath, false),
                action = () => StartSelectionProcess()
            };
            return gizmo;
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
            Messages.Message("Select first point", MessageTypeDefOf.SilentInput);
        }
        
        /// <summary>
        /// 第一个点选择回调
        /// </summary>
        private void OnFirstPointSelected(IntVec3 cell)
        {
            if (!cell.InBounds(parent.Map))
            {
                ResetSelection();
                Messages.Message("Point must be within map bounds.", MessageTypeDefOf.RejectInput);
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
            
            Messages.Message("Select second point", MessageTypeDefOf.SilentInput);
        }
        
        /// <summary>
        /// 第二个点选择回调
        /// </summary>
        private void OnSecondPointSelected(IntVec3 cell)
        {
            if (!cell.InBounds(parent.Map))
            {
                ResetSelection();
                Messages.Message("Point must be within map bounds.", MessageTypeDefOf.RejectInput);
                return;
            }
            
            if (cell == firstPoint)
            {
                ResetSelection();
                Messages.Message("Second point must be different from first point.", 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            secondPoint = cell;
            
            // 计算延长线与地图边界的交点
            CalculateAndCreateFlyOver();
        }
        
        /// <summary>
        /// 计算延长线并与地图边界相交，然后创建飞跃物体
        /// </summary>
        private void CalculateAndCreateFlyOver()
        {
            Map map = parent.Map;
            
            // 计算延长线与地图边界的交点
            IntVec3 entryPoint, exitPoint;
            
            if (!CalculateMapIntersections(firstPoint, secondPoint, map, out entryPoint, out exitPoint))
            {
                ResetSelection();
                Messages.Message("Failed to calculate flight path. Try different points.", 
                    MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 确定起始点和终点（更靠近第一个点的为起始点）
            float distance1 = firstPoint.DistanceTo(entryPoint);
            float distance2 = firstPoint.DistanceTo(exitPoint);
            
            IntVec3 startPoint = distance1 < distance2 ? entryPoint : exitPoint;
            IntVec3 endPoint = distance1 < distance2 ? exitPoint : entryPoint;
            
            // 创建内容物容器（如果需要）
            ThingOwner<Thing> contents = null;
            if (Props.spawnContents && Props.contentThingDef != null && Props.contentCount > 0)
            {
                contents = new ThingOwner<Thing>();
                for (int i = 0; i < Props.contentCount; i++)
                {
                    Thing thing = ThingMaker.MakeThing(Props.contentThingDef);
                    thing.stackCount = Mathf.Min(thing.def.stackLimit, Props.contentCount);
                    contents.TryAdd(thing);
                }
            }
            
            // 创建飞跃物体
            FlyOver flyOver = FlyOver.MakeFlyOver(
                Props.flyOverDef,
                startPoint,
                endPoint,
                map,
                Props.defaultSpeed,
                Props.defaultAltitude,
                contents,
                casterPawn: null  // 如果有施法者可以传入
            );
            
            if (flyOver != null)
            {
                // 记录使用
                RecordUse();
            }
            
            ResetSelection();
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
        
        /// <summary>
        /// 检查是否可以激活
        /// </summary>
        private bool CanActivateNow(out string reason)
        {
            reason = null;
            
            // 检查冷却时间
            if (Find.TickManager.TicksGame < lastUseTick + Props.cooldownTicks)
            {
                int remainingTicks = lastUseTick + Props.cooldownTicks - Find.TickManager.TicksGame;
                reason = $"On cooldown for {remainingTicks.ToStringSecondsFromTicks()}";
                return false;
            }
            
            // 检查使用次数限制
            if (Props.useLimit > 0 && useCount >= Props.useLimit)
            {
                reason = $"Use limit reached ({useCount}/{Props.useLimit})";
                return false;
            }
            
            // 检查能量（如果有能量Comp）
            var powerComp = parent.GetComp<CompPowerTrader>();
            if (powerComp != null && !powerComp.PowerOn)
            {
                reason = "No power";
                return false;
            }
            
            // 检查是否需要施法者（如果有相关要求）
            // 这里可以根据需要添加更多检查
            
            return true;
        }
        
        /// <summary>
        /// 记录使用
        /// </summary>
        private void RecordUse()
        {
            lastUseTick = Find.TickManager.TicksGame;
            useCount++;
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
        
        public override void PostDraw()
        {
            base.PostDraw();
            
            // 如果正在选择，绘制视觉指示器
            if (currentState != SelectionState.Idle)
            {
                DrawSelectionState();
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
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastUseTick, "lastUseTick", -99999);
            Scribe_Values.Look(ref useCount, "useCount", 0);
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
    }
}
