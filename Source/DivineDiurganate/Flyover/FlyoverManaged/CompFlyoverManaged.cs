using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 受管理的战机组件
    /// </summary>
    public class CompFlyoverManaged : ThingComp
    {
        private string flyoverDataGuid;
        private FlyoverData cachedFlyoverData;
        private bool hasRegistered = false;
        
        // 新增：标记是否正在重新入场流程中
        private bool isReEntering = false;

        // 缓存的技能列表
        private List<CompFlyOverSkillBase> cachedSkills = new List<CompFlyOverSkillBase>();
        private bool skillsScanned = false;

        /// <summary>
        /// 关联的战机数据
        /// </summary>
        public FlyoverData FlyoverData
        {
            get
            {
                if (cachedFlyoverData == null && !string.IsNullOrEmpty(flyoverDataGuid))
                {
                    var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
                    if (manager != null)
                    {
                        cachedFlyoverData = manager.GetFlyoverData(flyoverDataGuid);
                    }
                }
                return cachedFlyoverData;
            }
        }

        /// <summary>
        /// 获取配置属性
        /// </summary>
        public CompProperties_FlyoverManaged Config
        {
            get
            {
                return props as CompProperties_FlyoverManaged;
            }
        }

        /// <summary>
        /// 获取所有技能组件
        /// </summary>
        public List<CompFlyOverSkillBase> SkillComps
        {
            get
            {
                if (!skillsScanned)
                {
                    ScanSkills();
                }
                return cachedSkills;
            }
        }

        /// <summary>
        /// 设置FlyoverData GUID（用于重新入场时重用现有数据）
        /// </summary>
        public void SetFlyoverDataGuid(string guid, bool isReEnter = false)
        {
            if (string.IsNullOrEmpty(guid))
            {
                Log.Error("CompFlyoverManaged.SetFlyoverDataGuid: guid不能为空");
                return;
            }
            
            Log.Message($"CompFlyoverManaged.SetFlyoverDataGuid: 设置guid={guid}, isReEnter={isReEnter}");
            flyoverDataGuid = guid;
            isReEntering = isReEnter; // 标记为重新入场流程
            hasRegistered = true; // 标记为已注册，避免重复注册
            
            // 立即获取并关联FlyoverData
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null)
            {
                cachedFlyoverData = manager.GetFlyoverData(guid);
                if (cachedFlyoverData != null)
                {
                    Log.Message($"CompFlyoverManaged.SetFlyoverDataGuid: 成功关联到FlyoverData {cachedFlyoverData.DisplayName}");
                    
                    // 如果是重新入场，立即更新FlyoverData的链接
                    if (isReEnter && parent is FlyOver flyover)
                    {
                        cachedFlyoverData.linkedFlyover = flyover;
                        cachedFlyoverData.UpdateFromFlyover(flyover);
                    }
                }
            }
        }

        /// <summary>
        /// 根据存储的航线信息重新部署战机
        /// </summary>
        public bool TryRedeploy(Map map)
        {
            if (FlyoverData == null)
            {
                Log.Warning("No flyover data found for redeployment");
                return false;
            }

            if (map == null)
            {
                Log.Warning("Map is null for redeployment");
                return false;
            }

            // 验证战机状态
            if (FlyoverData.status == FlyoverStatus.Destroyed)
            {
                Log.Warning("Cannot redeploy destroyed aircraft");
                return false;
            }

            // 获取航线信息
            var pathInfo = FlyoverData.flightPathInfo;
            if (pathInfo == null)
            {
                Log.Warning("No flight path info stored for redeployment");
                return false;
            }

            try
            {
                // 使用航线信息创建新的FlyOver
                FlyOver newFlyOver = FlyOver.MakeFlyOverWithPathInfo(
                    FlyoverData.flyoverDef,
                    pathInfo,
                    map,
                    FlyoverData.flightSpeed,
                    FlyoverData.altitude,
                    null, // 内容物，可以根据需要调整
                    casterPawn: null
                );

                if (newFlyOver != null)
                {
                    // 更新FlyoverData
                    FlyoverData.linkedFlyover = newFlyOver;
                    FlyoverData.status = FlyoverStatus.OnMap;
                    FlyoverData.currentMapIndex = map.Index;
                    FlyoverData.currentPosition = newFlyOver.Position;
                    FlyoverData.startPosition = newFlyOver.startPosition;
                    FlyoverData.endPosition = newFlyOver.endPosition;
                    FlyoverData.flightProgress = 0f;

                    Log.Message($"Successfully redeployed {FlyoverData.DisplayName}");
                    return true;
                }
                else
                {
                    Log.Warning("Failed to create flyover for redeployment");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error during redeployment: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 扫描战机上的所有技能组件
        /// </summary>
        private void ScanSkills()
        {
            cachedSkills.Clear();

            if (parent == null || parent.AllComps == null)
                return;

            foreach (var comp in parent.AllComps)
            {
                if (comp is CompFlyOverSkillBase skillComp)
                {
                    cachedSkills.Add(skillComp);
                }
            }

            // 更新技能到FlyoverData中
            UpdateSkillsToFlyoverData();

            skillsScanned = true;
        }

        /// <summary>
        /// 更新技能信息到FlyoverData
        /// </summary>
        private void UpdateSkillsToFlyoverData()
        {
            var flyoverData = FlyoverData;
            if (flyoverData == null || flyoverData.skillSlots == null)
                return;

            // 清空技能槽
            foreach (var slot in flyoverData.skillSlots)
            {
                slot.isEmpty = true;
                slot.skillDefName = null;
            }

            // 重新填充技能槽
            foreach (var skillComp in cachedSkills)
            {
                var skillProps = skillComp.SkillProps;
                if (skillProps != null)
                {
                    int slotIndex = skillProps.slotIndex;

                    if (slotIndex >= 0 && slotIndex < flyoverData.skillSlots.Count)
                    {
                        var slot = flyoverData.skillSlots[slotIndex];
                        slot.isEmpty = false;
                        slot.skillDefName = skillProps.skillName;

                        // 更新技能状态
                        slot.UpdateFromSkillComp(skillComp);
                    }
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            Log.Message($"CompFlyoverManaged.PostSpawnSetup: called, isReEntering={isReEntering}, hasRegistered={hasRegistered}, flyoverDataGuid={flyoverDataGuid}");

            // 如果是重新入场流程，并且已经有flyoverDataGuid，则跳过注册
            if (isReEntering && !string.IsNullOrEmpty(flyoverDataGuid) && hasRegistered)
            {
                Log.Message($"CompFlyoverManaged.PostSpawnSetup: 重新入场流程，跳过注册，直接关联现有FlyoverData");
                ReassociateWithManager();
                ScanSkills();
                isReEntering = false; // 重置标志
                return;
            }

            if (!respawningAfterLoad)
            {
                RegisterWithManager();
                ScanSkills();
            }
            else
            {
                ReassociateWithManager();
                ScanSkills();
            }
        }

        /// <summary>
        /// 注册到管理器
        /// </summary>
        private void RegisterWithManager()
        {
            // 如果已经注册过，则跳过
            if (hasRegistered && !string.IsNullOrEmpty(flyoverDataGuid))
            {
                Log.Message($"CompFlyoverManaged.RegisterWithManager: 已经注册过，跳过");
                return;
            }
            
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null && parent is FlyOver flyover)
            {
                // 检查是否已存在关联此Flyover的FlyoverData
                var existingData = manager.AllFlyoverData.FirstOrDefault(d => d.linkedFlyover == flyover);
                if (existingData != null)
                {
                    Log.Message($"CompFlyoverManaged.RegisterWithManager: 找到已存在的FlyoverData {existingData.DisplayName}，重用");
                    flyoverDataGuid = existingData.guid;
                    cachedFlyoverData = existingData;
                    hasRegistered = true;
                    ScanSkills();
                    return;
                }

                if (string.IsNullOrEmpty(flyoverDataGuid))
                {
                    var data = manager.RegisterFlyover(flyover, Config);
                    if (data != null)
                    {
                        flyoverDataGuid = data.guid;
                        cachedFlyoverData = data;
                        hasRegistered = true;

                        // 扫描技能并更新数据
                        ScanSkills();
                        Log.Message($"CompFlyoverManaged.RegisterWithManager: 注册新的FlyoverData {data.DisplayName}");
                    }
                }
                else
                {
                    var data = manager.GetFlyoverData(flyoverDataGuid);
                    if (data != null)
                    {
                        data.UpdateFromFlyover(flyover);
                        cachedFlyoverData = data;
                        hasRegistered = true;

                        // 扫描技能并更新数据
                        ScanSkills();
                        Log.Message($"CompFlyoverManaged.RegisterWithManager: 更新现有FlyoverData {data.DisplayName}");
                    }
                    else
                    {
                        Log.Warning($"CompFlyoverManaged.RegisterWithManager: 找不到flyoverDataGuid={flyoverDataGuid}对应的FlyoverData");
                        flyoverDataGuid = null;
                        RegisterWithManager();
                    }
                }
            }
        }

        /// <summary>
        /// 重新关联到管理器
        /// </summary>
        private void ReassociateWithManager()
        {
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null && parent is FlyOver flyover && !string.IsNullOrEmpty(flyoverDataGuid))
            {
                var data = manager.GetFlyoverData(flyoverDataGuid);
                if (data != null)
                {
                    data.linkedFlyover = flyover;
                    data.UpdateFromFlyover(flyover);
                    cachedFlyoverData = data;
                    hasRegistered = true;

                    // 扫描技能并更新数据
                    ScanSkills();
                    Log.Message($"CompFlyoverManaged.ReassociateWithManager: 重新关联到FlyoverData {data.DisplayName}");
                }
                else
                {
                    Log.Warning($"CompFlyoverManaged.ReassociateWithManager: 找不到flyoverDataGuid={flyoverDataGuid}对应的FlyoverData");
                    flyoverDataGuid = null;
                    hasRegistered = false;
                    RegisterWithManager();
                }
            }
            else if (manager != null && parent is FlyOver flyover2)
            {
                // 如果没有guid，尝试注册
                RegisterWithManager();
            }
        }

        public void PostDeSpawn(Map map)
        {
            Log.Message($"CompFlyoverManaged.PostDeSpawn: called, parent.Destroyed={parent.Destroyed}");
            
            if (FlyoverData != null && parent is FlyOver)
            {
                if (!parent.Destroyed)
                {
                    FlyoverData.status = FlyoverStatus.Standby;
                    FlyoverData.linkedFlyover = null;
                    Log.Message($"CompFlyoverManaged.PostDeSpawn: {FlyoverData.DisplayName} 转为待命状态");
                }
                else
                {
                    HandleFlyoverDestroyed();
                }
            }
        }

        /// <summary>
        /// 处理战机销毁
        /// </summary>
        private void HandleFlyoverDestroyed()
        {
            if (FlyoverData == null) return;

            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();

            if (Config != null && !Config.destroyDataWithFlyover)
            {
                // 配置为不销毁数据，转为待命状态
                FlyoverData.status = FlyoverStatus.Standby;
                FlyoverData.linkedFlyover = null;
                Log.Message($"CompFlyoverManaged.HandleFlyoverDestroyed: {FlyoverData.DisplayName} 转为待命状态");
            }
            else
            {
                // 配置为销毁数据，或没有配置时默认销毁
                FlyoverData.status = FlyoverStatus.Destroyed;
                FlyoverData.linkedFlyover = null;

                manager?.MarkFlyoverAsDestroyed(flyoverDataGuid);
                Log.Message($"CompFlyoverManaged.HandleFlyoverDestroyed: {FlyoverData.DisplayName} 标记为已销毁");
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            HandleFlyoverDestroyed();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref flyoverDataGuid, "flyoverDataGuid");
            Scribe_Values.Look(ref hasRegistered, "hasRegistered", false);
            Scribe_Values.Look(ref skillsScanned, "skillsScanned", false);
            Scribe_Values.Look(ref isReEntering, "isReEntering", false);
        }
    }
}
