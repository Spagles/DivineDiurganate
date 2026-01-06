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
        /// 注册新战机 - 修改：确保配置被正确传递
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
            // 创建新数据 - 关键修改：确保配置被正确传递
            var newData = new FlyoverData(flyover, config)
            {
                spawnTick = Find.TickManager.TicksGame
            };
            allFlyoverData.Add(newData);
            // 检查是否应该打开UI
            CheckAndUpdateUIState();
            Log.Message($"WorldComp_FlyoverManager: 注册新战机 {newData.DisplayName}, guid={newData.guid}, 配置={config?.destroyDataWithFlyover}");
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

                // 延迟检查UI状态，避免在销毁过程中操作窗口
                LongEventHandler.QueueLongEvent(() =>
                {
                    CheckAndUpdateUIState();
                }, "UpdateFlyoverUI", false, null);

                Log.Message($"WorldComp_FlyoverManager: 标记战机为销毁 {data.DisplayName}, guid={guid}");
            }
        }

        /// <summary>
        /// 检查并更新UI状态
        /// </summary>
        private void CheckAndUpdateUIState()
        {
            try
            {
                int activeCount = ActiveFlyoverCount;

                if (activeCount > 0)
                {
                    // 有活跃战机，打开UI
                    if (!uiIsOpen)
                    {
                        OpenUI();
                    }
                    else
                    {
                        // UI已经打开，确保它是最新的
                        UpdateOpenUI();
                    }
                }
                //else
                //{
                //    // 没有活跃战机，关闭UI
                //    if (uiIsOpen)
                //    {
                //        CloseUI();
                //    }
                //}
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in CheckAndUpdateUIState: {ex}");
            }
        }

        /// <summary>
        /// 打开UI
        /// </summary>
        private void OpenUI()
        {
            try
            {
                if (uiIsOpen) return;

                // 检查是否已经有窗口打开
                if (Find.WindowStack.IsOpen(typeof(Window_FlyoverUI_Expanded)) ||
                    Find.WindowStack.IsOpen(typeof(Window_FlyoverUI_Minimized)))
                {
                    uiIsOpen = true;
                    return;
                }

                // 直接打开展开的窗口，不指定位置（使用默认或上次记录的位置）
                Window_FlyoverUI_Expanded expandedWindow = new Window_FlyoverUI_Expanded(this);
                Find.WindowStack.Add(expandedWindow);
                uiIsOpen = true;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in OpenUI: {ex}");
                uiIsOpen = false;
            }
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        private void CloseUI()
        {
            try
            {
                if (!uiIsOpen) return;

                // 使用列表副本，避免修改正在遍历的集合
                List<Window> windowsToClose = new List<Window>();

                // 收集需要关闭的窗口
                foreach (Window window in Find.WindowStack.Windows)
                {
                    if (window is Window_FlyoverUI_Expanded || window is Window_FlyoverUI_Minimized)
                    {
                        windowsToClose.Add(window);
                    }
                }

                // 关闭收集到的窗口
                foreach (Window window in windowsToClose)
                {
                    try
                    {
                        window.Close();
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"Error closing flyover window: {ex}");
                    }
                }

                uiIsOpen = false;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in CloseUI: {ex}");
                uiIsOpen = false;
            }
        }

        /// <summary>
        /// 更新已打开的UI
        /// </summary>
        private void UpdateOpenUI()
        {
            try
            {
                // 检查是否有展开的窗口，如果没有但UI状态为打开，则纠正状态
                bool hasExpandedWindow = false;
                bool hasMinimizedWindow = false;

                foreach (Window window in Find.WindowStack.Windows)
                {
                    if (window is Window_FlyoverUI_Expanded)
                        hasExpandedWindow = true;
                    if (window is Window_FlyoverUI_Minimized)
                        hasMinimizedWindow = true;
                }

                // 如果UI状态为打开但没有对应窗口，纠正状态
                if (uiIsOpen && !hasExpandedWindow && !hasMinimizedWindow)
                {
                    uiIsOpen = false;
                }
                // 如果UI状态为关闭但有窗口，纠正状态
                else if (!uiIsOpen && (hasExpandedWindow || hasMinimizedWindow))
                {
                    uiIsOpen = true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in UpdateOpenUI: {ex}");
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            // 每60ticks（1秒）更新一次
            if (Find.TickManager.TicksGame % 60 == 0)
            {
                try
                {
                    // 更新所有战机数据
                    foreach (var data in allFlyoverData)
                    {
                        try
                        {
                            data.Tick();
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error($"Error ticking flyover data {data.guid}: {ex}");
                        }
                    }

                    // 检查并更新UI状态
                    CheckAndUpdateUIState();
                }
                catch (System.Exception ex)
                {
                    Log.Error($"Error in WorldComponentTick: {ex}");
                }
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
                try
                {
                    CheckAndUpdateUIState();
                }
                catch (System.Exception ex)
                {
                    Log.Error($"Error in FinalizeInit: {ex}");
                }
            });
        }
    }
}
