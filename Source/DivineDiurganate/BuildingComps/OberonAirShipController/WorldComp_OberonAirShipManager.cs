using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld.Planet;

namespace DivineDiurganate
{
    /// <summary>
    /// 全局 OberonAirShip 管理器
    /// </summary>
    public class WorldComp_OberonAirShipManager : WorldComponent
    {
        // 单例访问
        private static WorldComp_OberonAirShipManager instance;
        public static WorldComp_OberonAirShipManager Instance => instance;

        // 配置（从控制器获取）
        private IntRange stayDurationRange = new IntRange(2, 5);
        private IntRange intervalRange = new IntRange(7, 14);
        private string letterLabel;
        private string letterText;

        // 状态
        private bool isControllerInitialized = false;
        private AirShipState currentState = AirShipState.Absent;
        private int nextArrivalTick = -1;
        private int departureTick = -1;
        private int stayDurationDays = 0;
        private int intervalDays = 0;
        
        // 控制器引用
        private CompOberonAirShipController activeController;

        public WorldComp_OberonAirShipManager(World world) : base(world)
        {
            instance = this;
        }

        /// <summary>
        /// 尝试初始化控制器
        /// </summary>
        public bool TryInitializeController(CompOberonAirShipController controller)
        {
            if (isControllerInitialized)
            {
                // 已有控制器，拒绝初始化
                return false;
            }

            activeController = controller;
            isControllerInitialized = true;
            
            // 初始化时间
            intervalDays = intervalRange.RandomInRange;
            nextArrivalTick = Find.TickManager.TicksGame + intervalDays * 60000;
            
            return true;
        }

        /// <summary>
        /// 设置控制器配置
        /// </summary>
        public void SetControllerConfig(IntRange stayRange, IntRange intervalRange, 
            string label, string text)
        {
            this.stayDurationRange = stayRange;
            this.intervalRange = intervalRange;
            this.letterLabel = label;
            this.letterText = text;
        }

        /// <summary>
        /// 更新控制器状态
        /// </summary>
        public void UpdateControllerState(CompOberonAirShipController controller)
        {
            if (!isControllerInitialized || activeController != controller)
                return;

            int currentTick = Find.TickManager.TicksGame;

            switch (currentState)
            {
                case AirShipState.Absent:
                    // 检查是否应该抵达
                    if (currentTick >= nextArrivalTick)
                    {
                        Arrive();
                    }
                    break;

                case AirShipState.Nearby:
                    // 检查是否应该离开
                    if (currentTick >= departureTick)
                    {
                        Depart();
                    }
                    break;

                case AirShipState.Arriving:
                    // 抵达动画完成后进入Nearby状态
                    // 这里可以添加抵达动画的计时逻辑
                    currentState = AirShipState.Nearby;
                    UpdateController();
                    break;

                case AirShipState.Departing:
                    // 离开动画完成后进入Absent状态
                    // 这里可以添加离开动画的计时逻辑
                    currentState = AirShipState.Absent;
                    intervalDays = intervalRange.RandomInRange;
                    nextArrivalTick = currentTick + intervalDays * 60000;
                    UpdateController();
                    break;
            }
        }

        /// <summary>
        /// 抵达处理
        /// </summary>
        private void Arrive()
        {
            currentState = AirShipState.Arriving;
            stayDurationDays = stayDurationRange.RandomInRange;
            departureTick = Find.TickManager.TicksGame + stayDurationDays * 60000;
            
            // 更新所有相关的 FlyOverGenerator
            RefreshFlyOverGenerators();
            
            UpdateController();
        }

        /// <summary>
        /// 离开处理
        /// </summary>
        private void Depart()
        {
            currentState = AirShipState.Departing;
            
            // 更新所有相关的 FlyOverGenerator
            RefreshFlyOverGenerators();
            
            UpdateController();
        }

        /// <summary>
        /// 刷新所有 FlyOverGenerator
        /// </summary>
        private void RefreshFlyOverGenerators()
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDef.Named("DD_OberonAirshipGenerator")))
                {
                    var comp = thing.TryGetComp<CompFlyOverGenerator>();
                    if (comp != null)
                    {
                        comp.NotifyOberonAirShipStateChanged(currentState == AirShipState.Nearby);
                    }
                }
            }
        }

        /// <summary>
        /// 更新控制器状态
        /// </summary>
        private void UpdateController()
        {
            if (activeController != null)
            {
                activeController.UpdateState(currentState, nextArrivalTick, departureTick);
            }
        }

        /// <summary>
        /// OberonAirShip 是否在附近
        /// </summary>
        public bool IsOberonAirShipNearby => currentState == AirShipState.Nearby;

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public AirShipState CurrentState => currentState;

        /// <summary>
        /// 获取下次抵达时间（天）
        /// </summary>
        public float NextArrivalInDays
        {
            get
            {
                if (currentState != AirShipState.Absent)
                    return 0f;
                    
                int ticksRemaining = nextArrivalTick - Find.TickManager.TicksGame;
                return ticksRemaining / 60000f;
            }
        }

        /// <summary>
        /// 获取剩余停留时间（天）
        /// </summary>
        public float RemainingStayDays
        {
            get
            {
                if (currentState != AirShipState.Nearby)
                    return 0f;
                    
                int ticksRemaining = departureTick - Find.TickManager.TicksGame;
                return ticksRemaining / 60000f;
            }
        }

        /// <summary>
        /// 强制抵达（开发模式用）
        /// </summary>
        public void DebugTriggerArrival()
        {
            if (currentState == AirShipState.Absent)
            {
                Arrive();
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            
            // 每游戏小时检查一次
            if (Find.TickManager.TicksGame % 2500 == 0 && isControllerInitialized)
            {
                UpdateControllerState(activeController);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref isControllerInitialized, "isControllerInitialized", false);
            Scribe_Values.Look(ref currentState, "currentState", AirShipState.Absent);
            Scribe_Values.Look(ref nextArrivalTick, "nextArrivalTick", -1);
            Scribe_Values.Look(ref departureTick, "departureTick", -1);
            Scribe_Values.Look(ref stayDurationDays, "stayDurationDays", 0);
            Scribe_Values.Look(ref intervalDays, "intervalDays", 0);
            
            // 保存配置
            Scribe_Values.Look(ref stayDurationRange, "stayDurationRange", new IntRange(2, 5));
            Scribe_Values.Look(ref intervalRange, "intervalRange", new IntRange(7, 14));
            Scribe_Values.Look(ref letterLabel, "letterLabel");
            Scribe_Values.Look(ref letterText, "letterText");
        }

        /// <summary>
        /// 获取状态描述（用于UI显示）
        /// </summary>
        public string GetStatusDescription()
        {
            switch (currentState)
            {
                case AirShipState.Nearby:
                    return $"DD_OberonAirShip_Status_Nearby".Translate(RemainingStayDays.ToString("F1"));
                    
                case AirShipState.Absent:
                    return $"DD_OberonAirShip_Status_Absent".Translate(NextArrivalInDays.ToString("F1"));
                    
                case AirShipState.Arriving:
                    return "DD_OberonAirShip_Status_Arriving".Translate();
                    
                case AirShipState.Departing:
                    return "DD_OberonAirShip_Status_Departing".Translate();
                    
                default:
                    return "DD_OberonAirShip_Status_Unknown".Translate();
            }
        }
    }
}
