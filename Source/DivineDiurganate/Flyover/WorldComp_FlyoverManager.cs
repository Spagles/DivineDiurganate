using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace DivineDiurganate
{
    /// <summary>
    /// 战机管理器
    /// </summary>
    public class WorldComp_FlyoverManager : WorldComponent
    {
        private List<FlyoverData> allFlyoverData = new List<FlyoverData>();
        private Window_FlyoverUI flyoverWindow;
        private bool uiIsOpen = false;
        
        public WorldComp_FlyoverManager(World world) : base(world) { }
        
        public List<FlyoverData> AllFlyoverData => allFlyoverData;
        
        public List<FlyoverData> ActiveFlyoverData => 
            allFlyoverData.Where(d => d.status != FlyoverStatus.Destroyed).ToList();
        
        /// <summary>
        /// 获取在UI中可见的战机数据
        /// </summary>
        public List<FlyoverData> UIVisibleFlyoverData => 
            allFlyoverData.Where(d => d.status != FlyoverStatus.Destroyed).ToList();
        
        public int ActiveFlyoverCount => ActiveFlyoverData.Count;
        
        /// <summary>
        /// 注册新战机
        /// </summary>
        public FlyoverData RegisterFlyover(FlyOver flyover, CompProperties_FlyoverManaged config = null)
        {
            // 检查是否已注册
            var existingData = allFlyoverData.FirstOrDefault(d => 
                d.linkedFlyover == flyover || 
                (d.linkedFlyover != null && d.linkedFlyover == flyover));
            
            if (existingData != null)
            {
                existingData.UpdateFromFlyover(flyover);
                return existingData;
            }
            
            // 创建新数据
            var newData = new FlyoverData(flyover, config)
            {
                spawnTick = Find.TickManager.TicksGame
            };
            
            allFlyoverData.Add(newData);
            
            // 检查是否应该打开UI
            CheckAndUpdateUIState();
            
            return newData;
        }
        
        public FlyoverData GetFlyoverData(string guid)
        {
            return allFlyoverData.FirstOrDefault(d => d.guid == guid);
        }
        
        public bool RemoveFlyoverData(string guid)
        {
            var data = GetFlyoverData(guid);
            if (data != null)
            {
                allFlyoverData.Remove(data);
                CheckAndUpdateUIState();
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 标记战机为销毁
        /// </summary>
        public void MarkFlyoverAsDestroyed(string guid)
        {
            var data = GetFlyoverData(guid);
            if (data != null)
            {
                data.status = FlyoverStatus.Destroyed;
                data.linkedFlyover = null;
                CheckAndUpdateUIState();
            }
        }
        
        /// <summary>
        /// 检查并更新UI状态
        /// </summary>
        private void CheckAndUpdateUIState()
        {
            int activeCount = ActiveFlyoverCount;
            
            if (activeCount > 0)
            {
                // 有活跃战机，打开UI
                if (!uiIsOpen)
                {
                    OpenUI();
                }
            }
            else
            {
                // 没有活跃战机，关闭UI
                if (uiIsOpen)
                {
                    CloseUI();
                }
            }
        }
        
        /// <summary>
        /// 打开UI
        /// </summary>
        private void OpenUI()
        {
            if (uiIsOpen) return;
            
            if (flyoverWindow == null)
            {
                flyoverWindow = new Window_FlyoverUI(this);
            }
            
            if (!Find.WindowStack.IsOpen(typeof(Window_FlyoverUI)))
            {
                Find.WindowStack.Add(flyoverWindow);
                uiIsOpen = true;
            }
            else
            {
                uiIsOpen = true;
            }
        }
        
        /// <summary>
        /// 关闭UI
        /// </summary>
        private void CloseUI()
        {
            if (!uiIsOpen) return;
            
            if (flyoverWindow != null && flyoverWindow.IsOpen)
            {
                flyoverWindow.Close();
            }
            
            uiIsOpen = false;
        }
        
        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                foreach (var data in allFlyoverData)
                {
                    data.Tick();
                }
                
                CheckAndUpdateUIState();
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref allFlyoverData, "allFlyoverData", LookMode.Deep);
            Scribe_Values.Look(ref uiIsOpen, "uiIsOpen", false);
        }
        
        public void FinalizeInit()
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                CheckAndUpdateUIState();
            });
        }
    }
}
