using RimWorld;
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
            
            Scribe_Collections.Look(ref skillSlots, "skillSlots", LookMode.Deep);
        }
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
                skillDefName = props.skillName; // 或者使用defName
            }

            linkedSkillComp = skillComp;
        }
    }
}
