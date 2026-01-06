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
        /// 为重新入场设置FlyoverData GUID（在Spawn之前调用）
        /// </summary>
        public void SetFlyoverDataGuidForReEnter(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                Log.Error("CompFlyoverManaged.SetFlyoverDataGuidForReEnter: guid不能为空");
                return;
            }
            
            flyoverDataGuid = guid;
            isReEntering = true; // 标记为重新入场流程
            hasRegistered = true; // 标记为已注册
            
            // 立即获取FlyoverData但不更新链接（因为FlyOver还未Spawn）
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null)
            {
                cachedFlyoverData = manager.GetFlyoverData(guid);
                if (cachedFlyoverData == null)
                {
                    Log.Error($"CompFlyoverManaged.SetFlyoverDataGuidForReEnter: 找不到guid={guid}对应的FlyoverData");
                }
            }
        }

        /// <summary>
        /// 设置FlyoverData GUID（在Spawn之后调用）
        /// </summary>
        public void SetFlyoverDataGuid(string guid, bool isReEnter = false)
        {
            if (string.IsNullOrEmpty(guid))
            {
                Log.Error("CompFlyoverManaged.SetFlyoverDataGuid: guid不能为空");
                return;
            }
            
            flyoverDataGuid = guid;
            isReEntering = isReEnter;
            hasRegistered = true;
            
            // 获取并关联FlyoverData
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null)
            {
                cachedFlyoverData = manager.GetFlyoverData(guid);
                if (cachedFlyoverData != null && parent is FlyOver flyover)
                {
                    cachedFlyoverData.linkedFlyover = flyover;
                    cachedFlyoverData.UpdateFromFlyover(flyover);
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // 如果是重新入场流程，并且已经设置过guid，跳过注册
            if (isReEntering && !string.IsNullOrEmpty(flyoverDataGuid) && hasRegistered)
            {
                Log.Message($"CompFlyoverManaged.PostSpawnSetup: 重新入场流程，跳过注册，直接关联现有FlyoverData {flyoverDataGuid}");
                
                // 直接关联现有的FlyoverData
                ReassociateWithManagerForReEnter();
                ScanSkills();
                
                // 重置标志
                isReEntering = false;
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
        /// 重新入场时关联管理器
        /// </summary>
        private void ReassociateWithManagerForReEnter()
        {
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null && parent is FlyOver flyover && !string.IsNullOrEmpty(flyoverDataGuid))
            {
                var data = manager.GetFlyoverData(flyoverDataGuid);
                if (data != null)
                {
                    // 关键：更新现有FlyoverData的链接
                    data.linkedFlyover = flyover;
                    data.UpdateFromFlyover(flyover);
                    cachedFlyoverData = data;
                    hasRegistered = true;

                    Log.Message($"CompFlyoverManaged.ReassociateWithManagerForReEnter: 成功关联到FlyoverData {data.DisplayName}");
                }
                else
                {
                    Log.Error($"CompFlyoverManaged.ReassociateWithManagerForReEnter: 找不到flyoverDataGuid={flyoverDataGuid}对应的FlyoverData");
                }
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
                return;
            }
            
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null && parent is FlyOver flyover)
            {
                // 检查是否已存在关联此Flyover的FlyoverData
                var existingData = manager.AllFlyoverData.FirstOrDefault(d => d.linkedFlyover == flyover);
                if (existingData != null)
                {
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
                        ScanSkills();
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
                        ScanSkills();
                    }
                    else
                    {
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
                    ScanSkills();
                }
                else
                {
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
            if (FlyoverData != null && parent is FlyOver)
            {
                if (!parent.Destroyed)
                {
                    FlyoverData.status = FlyoverStatus.Standby;
                    FlyoverData.linkedFlyover = null;
                }
                else
                {
                    HandleFlyoverDestroyed();
                }
            }
        }
        /// <summary>
        /// 战机销毁时的处理
        /// </summary>
        public void HandleFlyoverDestroyed()
        {
            try
            {
                if (!flyoverDataGuid.NullOrEmpty())
                {
                    // 延迟通知管理器，避免立即修改集合
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        try
                        {
                            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
                            if (manager != null)
                            {
                                manager.MarkFlyoverAsDestroyed(flyoverDataGuid);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"Error handling flyover destroyed: {ex}");
                        }
                    }, "HandleFlyoverDestroyed", false, null);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in HandleFlyoverDestroyed: {ex}");
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
