using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机数据
    /// </summary>
    public class FlyoverData : IExposable
    {
        public string guid;
        public ThingDef flyoverDef;
        public string customName;

        public FlyoverStatus status = FlyoverStatus.OnMap;
        public int currentMapIndex = -1;
        public IntVec3 currentPosition;
        public IntVec3 startPosition;
        public IntVec3 endPosition;

        // 新增：航线生成信息
        public FlightPathInfo flightPathInfo;

        public float flightProgress = 0f;
        public float flightSpeed = 1f;
        public float altitude = 10f;

        public int spawnTick = -1;
        public int lastUpdateTick = -1;

        public FlyOver linkedFlyover;

        // 新增：图标路径
        public string iconPath;

        // 技能槽
        public List<SkillSlot> skillSlots = new List<SkillSlot>();

        public FlyoverData()
        {
            guid = System.Guid.NewGuid().ToString();

            for (int i = 0; i < 4; i++)
            {
                skillSlots.Add(new SkillSlot { slotIndex = i, isEmpty = true });
            }

            // 修改：不要初始化抽象类，设为null
            // flightPathInfo会在需要时创建
        }

        public FlyoverData(FlyOver flyover, CompProperties_FlyoverManaged config = null) : this()
        {
            UpdateFromFlyover(flyover);

            // 从配置获取图标路径
            if (config != null && !string.IsNullOrEmpty(config.iconPath))
            {
                iconPath = config.iconPath;
            }
        }

        public void UpdateFromFlyover(FlyOver flyover)
        {
            if (flyover == null) return;

            linkedFlyover = flyover;
            flyoverDef = flyover.def;

            if (flyover.Spawned)
            {
                status = FlyoverStatus.OnMap;
                currentMapIndex = flyover.Map?.Index ?? -1;
                currentPosition = flyover.Position;
            }
            else if (!flyover.Destroyed)
            {
                status = FlyoverStatus.Standby;
            }

            startPosition = flyover.startPosition;
            endPosition = flyover.endPosition;
            flightProgress = flyover.currentProgress;
            flightSpeed = flyover.flightSpeed;
            altitude = flyover.altitude;

            lastUpdateTick = Find.TickManager.TicksGame;
        }

        public void Tick()
        {
            if (linkedFlyover != null && !linkedFlyover.Destroyed)
            {
                UpdateFromFlyover(linkedFlyover);
            }
            else if (linkedFlyover != null && linkedFlyover.Destroyed)
            {
                // 战机已销毁，清空链接
                linkedFlyover = null;
            }
        }

        public bool CanDeploy()
        {
            return (status == FlyoverStatus.OnMap || status == FlyoverStatus.Standby) &&
                   (linkedFlyover == null || !linkedFlyover.Destroyed);
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(customName))
                    return customName;

                return flyoverDef?.LabelCap ?? "Unknown Aircraft";
            }
        }

        public string StatusDescription
        {
            get
            {
                switch (status)
                {
                    case FlyoverStatus.OnMap:
                        return "In Air";
                    case FlyoverStatus.Standby:
                        return "Standby";
                    case FlyoverStatus.Deploying:
                        return "Deploying";
                    case FlyoverStatus.Destroyed:
                        return "Destroyed";
                    default:
                        return "Unknown";
                }
            }
        }

        /// <summary>
        /// 获取显示图标（优先使用配置的图标路径）
        /// </summary>
        public Texture2D DisplayIcon
        {
            get
            {
                // 如果有配置的图标路径，尝试加载
                if (!string.IsNullOrEmpty(iconPath))
                {
                    try
                    {
                        var icon = ContentFinder<Texture2D>.Get(iconPath, false);
                        if (icon != null)
                            return icon;
                    }
                    catch
                    {
                        // 如果加载失败，使用默认图标
                    }
                }

                // 使用默认图标
                return flyoverDef?.uiIcon ?? BaseContent.BadTex;
            }
        }

        /// <summary>
        /// 根据存储的航线信息重新生成航线
        /// </summary>
        public bool TryGenerateFlightPath(Map map, out IntVec3 startPoint, out IntVec3 endPoint)
        {
            startPoint = IntVec3.Invalid;
            endPoint = IntVec3.Invalid;

            if (flightPathInfo == null || map == null)
                return false;

            return flightPathInfo.GeneratePath(map, out startPoint, out endPoint);
        }

        /// <summary>
        /// 设置航线生成信息
        /// </summary>
        public void SetFlightPathInfo(FlightPathInfo info)
        {
            flightPathInfo = info;
        }

        /// <summary>
        /// 创建默认的两点延长线航线信息
        /// </summary>
        public void CreateDefaultPathInfo(IntVec3 point1, IntVec3 point2)
        {
            flightPathInfo = new TwoPointsExtendedPathInfo(point1, point2);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref guid, "guid");
            Scribe_Defs.Look(ref flyoverDef, "flyoverDef");
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Values.Look(ref iconPath, "iconPath");
            Scribe_Values.Look(ref status, "status", FlyoverStatus.OnMap);
            Scribe_Values.Look(ref currentMapIndex, "currentMapIndex", -1);
            Scribe_Values.Look(ref currentPosition, "currentPosition");
            Scribe_Values.Look(ref startPosition, "startPosition");
            Scribe_Values.Look(ref endPosition, "endPosition");
            Scribe_Values.Look(ref flightProgress, "flightProgress", 0f);
            Scribe_Values.Look(ref flightSpeed, "flightSpeed", 1f);
            Scribe_Values.Look(ref altitude, "altitude", 10f);
            Scribe_Values.Look(ref spawnTick, "spawnTick", -1);
            Scribe_Values.Look(ref lastUpdateTick, "lastUpdateTick", -1);

            // 修改：使用自定义序列化方法处理抽象类
            ScribeFlightPathInfo(ref flightPathInfo);
            Scribe_Collections.Look(ref skillSlots, "skillSlots", LookMode.Deep);
        }

        /// <summary>
        /// 自定义序列化方法处理FlightPathInfo抽象类
        /// </summary>
        private void ScribeFlightPathInfo(ref FlightPathInfo flightPathInfo)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (flightPathInfo == null)
                {
                    Scribe_Values.Look(ref dummy, "flightPathInfoType", "None");
                }
                else if (flightPathInfo is TwoPointsExtendedPathInfo twoPointsPath)
                {
                    Scribe_Values.Look(ref dummy, "flightPathInfoType", "TwoPoints");
                    Scribe_Deep.Look(ref twoPointsPath, "flightPathInfo");
                }
                else if (flightPathInfo is DirectPathInfo directPath)
                {
                    Scribe_Values.Look(ref dummy, "flightPathInfoType", "Direct");
                    Scribe_Deep.Look(ref directPath, "flightPathInfo");
                }
                else
                {
                    Scribe_Values.Look(ref dummy, "flightPathInfoType", "Unknown");
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                string typeName = "None";
                Scribe_Values.Look(ref typeName, "flightPathInfoType", "None");

                switch (typeName)
                {
                    case "TwoPoints":
                        TwoPointsExtendedPathInfo twoPointsPath = new TwoPointsExtendedPathInfo();
                        Scribe_Deep.Look(ref twoPointsPath, "flightPathInfo");
                        flightPathInfo = twoPointsPath;
                        break;
                    case "Direct":
                        DirectPathInfo directPath = new DirectPathInfo();
                        Scribe_Deep.Look(ref directPath, "flightPathInfo");
                        flightPathInfo = directPath;
                        break;
                    default:
                        flightPathInfo = null;
                        break;
                }
            }
        }

        // 用于序列化的临时变量
        private string dummy;
    }

    public enum FlyoverStatus
    {
        OnMap,
        Standby,
        Deploying,
        Destroyed
    }

    public class SkillSlot : IExposable
    {
        public int slotIndex;
        public bool isEmpty = true;
        public string skillDefName;

        // 新增：技能状态信息
        public float cooldownPercent = 0f;
        public bool isAvailable = true;
        public string skillName;
        public string skillDescription;

        // 新增：临时存储技能组件引用（不会被序列化）
        [System.NonSerialized]
        public CompFlyOverSkillBase linkedSkillComp;

        public void ExposeData()
        {
            Scribe_Values.Look(ref slotIndex, "slotIndex");
            Scribe_Values.Look(ref isEmpty, "isEmpty", true);
            Scribe_Values.Look(ref skillDefName, "skillDefName");

            // 保存技能状态
            Scribe_Values.Look(ref cooldownPercent, "cooldownPercent", 0f);
            Scribe_Values.Look(ref isAvailable, "isAvailable", true);
            Scribe_Values.Look(ref skillName, "skillName");
            Scribe_Values.Look(ref skillDescription, "skillDescription");
        }

        /// <summary>
        /// 更新技能状态
        /// </summary>
        public void UpdateFromSkillComp(CompFlyOverSkillBase skillComp)
        {
            if (skillComp == null)
            {
                isEmpty = true;
                return;
            }

            isEmpty = false;
            cooldownPercent = skillComp.CooldownPercent;
            isAvailable = skillComp.isAvailable;

            var props = skillComp.SkillProps;
            if (props != null)
            {
                skillName = props.skillName;
                skillDescription = props.description;
                skillDefName = props.skillName;
            }

            linkedSkillComp = skillComp;
        }
    }

    /// <summary>
    /// 航线信息基类
    /// </summary>
    public abstract class FlightPathInfo : IExposable
    {
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        /// <summary>
        /// 生成飞行路径
        /// </summary>
        public abstract bool GeneratePath(Map map, out IntVec3 startPoint, out IntVec3 endPoint);

        /// <summary>
        /// 验证路径参数
        /// </summary>
        public abstract bool Validate(out string error);

        public abstract void ExposeData();
    }

    /// <summary>
    /// 两点延长线航线信息
    /// </summary>
    public class TwoPointsExtendedPathInfo : FlightPathInfo
    {
        public IntVec3 point1;
        public IntVec3 point2;

        public override string DisplayName => "Two Points Extended Path";
        public override string Description => "Creates a flight path by extending two selected points to map boundaries";

        public TwoPointsExtendedPathInfo() { }

        public TwoPointsExtendedPathInfo(IntVec3 p1, IntVec3 p2)
        {
            point1 = p1;
            point2 = p2;
        }

        public override bool GeneratePath(Map map, out IntVec3 startPoint, out IntVec3 endPoint)
        {
            return FlyOver.CalculateMapIntersections(point1, point2, map, out startPoint, out endPoint);
        }

        public override bool Validate(out string error)
        {
            error = null;

            if (point1 == point2)
            {
                error = "Points must be different";
                return false;
            }

            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref point1, "point1");
            Scribe_Values.Look(ref point2, "point2");
        }
    }

    /// <summary>
    /// 直接指定起点和终点的航线信息
    /// </summary>
    public class DirectPathInfo : FlightPathInfo
    {
        public IntVec3 startPoint;
        public IntVec3 endPoint;

        public override string DisplayName => "Direct Path";
        public override string Description => "Directly specifies start and end points";

        public DirectPathInfo() { }

        public DirectPathInfo(IntVec3 start, IntVec3 end)
        {
            startPoint = start;
            endPoint = end;
        }

        public override bool GeneratePath(Map map, out IntVec3 start, out IntVec3 end)
        {
            start = startPoint;
            end = endPoint;

            // 验证点是否在地图内
            if (!start.InBounds(map) || !end.InBounds(map))
            {
                return false;
            }

            return true;
        }

        public override bool Validate(out string error)
        {
            error = null;

            if (startPoint == endPoint)
            {
                error = "Start and end points must be different";
                return false;
            }

            return true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref startPoint, "startPoint");
            Scribe_Values.Look(ref endPoint, "endPoint");
        }
    }
}
