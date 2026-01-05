using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 再次入场技能 - 召唤战机沿新路径飞行
    /// </summary>
    public class CompFlyOverSkill_Reenter : CompFlyOverSkillBase
    {
        /// <summary>
        /// 获取技能属性
        /// </summary>
        public CompProperties_FlyOverSkill_Reenter ReenterProps
        {
            get
            {
                return props as CompProperties_FlyOverSkill_Reenter;
            }
        }
        
        /// <summary>
        /// 检查是否可以使用技能
        /// </summary>
        public override bool CanUseNow(out string reason)
        {
            // 首先调用基类检查
            if (!base.CanUseNow(out reason))
            {
                return false;
            }
            
            // 额外的检查：战机不能已经在飞行中
            var flyoverData = LinkedFlyoverData;
            if (flyoverData != null && flyoverData.status == FlyoverStatus.OnMap)
            {
                var linkedFlyover = flyoverData.linkedFlyover;
                if (linkedFlyover != null && linkedFlyover.Spawned)
                {
                    reason = "Aircraft is already on map and cannot re-enter";
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 双点选择完成回调 - 重新实现
        /// </summary>
        protected override void OnTwoPointsSelected(IntVec3 point1, IntVec3 point2)
        {
            // 验证选择的点
            if (!ValidatePoints(point1, point2, out string error))
            {
                Messages.Message(error, MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 计算飞行路径
            if (!CalculateFlightPath(point1, point2, out IntVec3 startPoint, out IntVec3 endPoint))
            {
                Messages.Message("Failed to calculate flight path", MessageTypeDefOf.RejectInput);
                return;
            }
            
            // 执行技能
            ExecuteReenter(startPoint, endPoint);
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
                error = "No map available";
                return false;
            }
            
            if (!point1.InBounds(map) || !point2.InBounds(map))
            {
                error = "Points must be within map bounds";
                return false;
            }
            
            // 检查是否相同点
            if (point1 == point2)
            {
                error = "Points must be different";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 计算飞行路径（与地图边界相交）
        /// </summary>
        private bool CalculateFlightPath(IntVec3 point1, IntVec3 point2, out IntVec3 startPoint, out IntVec3 endPoint)
        {
            startPoint = IntVec3.Invalid;
            endPoint = IntVec3.Invalid;
            
            Map map = Find.CurrentMap;
            if (map == null) return false;
            
            // 计算与地图边界的交点（使用与CompFlyOverGenerator相同的逻辑）
            IntVec3 entryPoint, exitPoint;
            
            if (!CalculateMapIntersections(point1, point2, map, out entryPoint, out exitPoint))
            {
                return false;
            }
            
            // 确定哪个交点离point1更近，作为起点
            float distToEntry = point1.DistanceTo(entryPoint);
            float distToExit = point1.DistanceTo(exitPoint);
            
            if (distToEntry < distToExit)
            {
                startPoint = entryPoint;
                endPoint = exitPoint;
            }
            else
            {
                startPoint = exitPoint;
                endPoint = entryPoint;
            }
            
            return true;
        }
        
        /// <summary>
        /// 计算与地图边界的交点（从CompFlyOverGenerator复制）
        /// </summary>
        private bool CalculateMapIntersections(IntVec3 point1, IntVec3 point2, Map map, 
            out IntVec3 intersection1, out IntVec3 intersection2)
        {
            intersection1 = IntVec3.Invalid;
            intersection2 = IntVec3.Invalid;
            
            Vector3 p1 = point1.ToVector3();
            Vector3 p2 = point2.ToVector3();
            
            Vector3 dir = (p2 - p1).normalized;
            
            float minX = 0f;
            float maxX = map.Size.x - 1;
            float minZ = 0f;
            float maxZ = map.Size.z - 1;
            
            List<Vector3> intersections = new List<Vector3>();
            
            // 计算与四条边界的交点
            if (Mathf.Abs(dir.x) > 0.001f)
            {
                float tLeft = (minX - p1.x) / dir.x;
                Vector3 intersectLeft = p1 + dir * tLeft;
                if (intersectLeft.z >= minZ && intersectLeft.z <= maxZ)
                    intersections.Add(intersectLeft);
                    
                float tRight = (maxX - p1.x) / dir.x;
                Vector3 intersectRight = p1 + dir * tRight;
                if (intersectRight.z >= minZ && intersectRight.z <= maxZ)
                    intersections.Add(intersectRight);
            }
            
            if (Mathf.Abs(dir.z) > 0.001f)
            {
                float tBottom = (minZ - p1.z) / dir.z;
                Vector3 intersectBottom = p1 + dir * tBottom;
                if (intersectBottom.x >= minX && intersectBottom.x <= maxX)
                    intersections.Add(intersectBottom);
                    
                float tTop = (maxZ - p1.z) / dir.z;
                Vector3 intersectTop = p1 + dir * tTop;
                if (intersectTop.x >= minX && intersectTop.x <= maxX)
                    intersections.Add(intersectTop);
            }
            
            if (intersections.Count < 2) return false;
            
            // 找到与p1距离最近的两个交点
            intersections.Sort((a, b) => 
                (a - p1).sqrMagnitude.CompareTo((b - p1).sqrMagnitude));
            
            intersection1 = intersections[0].ToIntVec3();
            intersection2 = intersections[1].ToIntVec3();
            
            return true;
        }
        
        /// <summary>
        /// 执行再次入场
        /// </summary>
        private void ExecuteReenter(IntVec3 startPoint, IntVec3 endPoint)
        {
            var flyoverData = LinkedFlyoverData;
            if (flyoverData == null || flyoverData.flyoverDef == null)
            {
                Messages.Message("Aircraft data not found", MessageTypeDefOf.RejectInput);
                return;
            }
            
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("No map available", MessageTypeDefOf.RejectInput);
                return;
            }
            
            try
            {
                // 创建内容物容器（如果需要）
                ThingOwner contents = null;
                if (ReenterProps.spawnContentsOnImpact && 
                    ReenterProps.contentThingDef != null && 
                    ReenterProps.contentCount > 0)
                {
                    contents = new ThingOwner<Thing>();
                    for (int i = 0; i < ReenterProps.contentCount; i++)
                    {
                        Thing thing = ThingMaker.MakeThing(ReenterProps.contentThingDef);
                        thing.stackCount = Mathf.Min(thing.def.stackLimit, ReenterProps.contentCount);
                        contents.TryAdd(thing);
                    }
                }
                
                // 创建新的FlyOver
                FlyOver flyOver = FlyOver.MakeFlyOver(
                    flyoverData.flyoverDef,
                    startPoint,
                    endPoint,
                    map,
                    ReenterProps.defaultSpeed,
                    ReenterProps.defaultAltitude,
                    contents,
                    fadeInDuration: 1.5f,
                    defaultFadeOutDuration: 1.5f,
                    casterPawn: null
                );
                
                if (flyOver != null)
                {
                    // 更新战机数据
                    flyoverData.linkedFlyover = flyOver;
                    flyoverData.status = FlyoverStatus.OnMap;
                    flyoverData.currentMapIndex = map.Index;
                    flyoverData.currentPosition = startPoint;
                    flyoverData.startPosition = startPoint;
                    flyoverData.endPosition = endPoint;
                    flyoverData.flightProgress = 0f;
                    flyoverData.flightSpeed = ReenterProps.defaultSpeed;
                    flyoverData.altitude = ReenterProps.defaultAltitude;
                    
                    // 记录技能使用
                    base.Execute();
                    
                    // 显示成功消息
                    Messages.Message($"{SkillProps.skillName} activated - {flyoverData.DisplayName} is entering the battlefield",
                        MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message("Failed to create flight path", MessageTypeDefOf.RejectInput);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error executing Reenter skill: {ex}");
                Messages.Message("Failed to activate skill", MessageTypeDefOf.RejectInput);
            }
        }
        
        /// <summary>
        /// 获取技能描述（重写）
        /// </summary>
        public override string GetStatusDescription()
        {
            var baseDesc = base.GetStatusDescription();
            
            if (baseDesc != "Ready")
                return baseDesc;
            
            // 添加技能特定信息
            var flyoverData = LinkedFlyoverData;
            if (flyoverData != null)
            {
                return $"Summon {flyoverData.DisplayName} to fly over selected path";
            }
            
            return "Ready to summon aircraft";
        }
    }
}
