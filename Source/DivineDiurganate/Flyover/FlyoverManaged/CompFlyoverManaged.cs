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
                        
                        // 可以在SkillSlot中存储更多信息
                        // slot.skillComp = skillComp; // 如果需要的话
                    }
                }
            }
        }
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
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
            var manager = Find.World.GetComponent<WorldComp_FlyoverManager>();
            if (manager != null && parent is FlyOver flyover && !hasRegistered)
            {
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
                }
                else
                {
                    flyoverDataGuid = null;
                    hasRegistered = false;
                    RegisterWithManager();
                }
            }
        }
        
        public void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            
            if (FlyoverData != null && parent is FlyOver)
            {
                if (!parent.Destroyed)
                {
                    FlyoverData.status = FlyoverStatus.Standby;
                }
                else
                {
                    HandleFlyoverDestroyed();
                }
                
                FlyoverData.linkedFlyover = null;
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
            }
            else
            {
                // 配置为销毁数据，或没有配置时默认销毁
                FlyoverData.status = FlyoverStatus.Destroyed;
                FlyoverData.linkedFlyover = null;
                
                manager?.MarkFlyoverAsDestroyed(flyoverDataGuid);
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
        }
    }
}
