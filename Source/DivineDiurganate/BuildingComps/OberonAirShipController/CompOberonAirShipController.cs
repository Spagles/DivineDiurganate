using RimWorld;
using System;
using Verse;
using System.Collections.Generic;

namespace DivineDiurganate
{
    /// <summary>
    /// OberonAirShip 控制器组件 - 控制 OberonAirShip 的时间窗口
    /// </summary>
    public class CompOberonAirShipController : ThingComp
    {
        public CompProperties_OberonAirShipController Props =>
            (CompProperties_OberonAirShipController)props;

        // 是否已初始化（只有第一个建造的建筑会初始化）
        private bool isInitialized = false;
        
        // 当前状态
        private AirShipState currentState = AirShipState.Absent;
        
        // 时间追踪
        private int nextArrivalTick = -1;      // 下次抵达时间
        private int departureTick = -1;        // 离开时间
        private int lastLetterTick = -1;       // 上次发送信件时间
        private int stayDurationDays = 0;      // 当前停留天数
        private int intervalDays = 0;          // 当前间隔天数
        
        // 信件内容缓存
        private Letter cachedLetter;

        public override void PostPostMake()
        {
            base.PostPostMake();
            
            // 只有第一个建造的建筑会初始化
            TryInitializeController();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            if (!respawningAfterLoad)
            {
                // 非加载时的初始化
                TryInitializeController();
            }
        }

        /// <summary>
        /// 尝试初始化控制器
        /// </summary>
        private void TryInitializeController()
        {
            if (isInitialized) return;
            
            // 获取全局管理器
            var manager = Find.World.GetComponent<WorldComp_OberonAirShipManager>();
            if (manager == null)
            {
                Log.Error("Cannot find WorldComp_OberonAirShipManager");
                return;
            }
            
            // 只有第一个控制器可以初始化
            if (manager.TryInitializeController(this))
            {
                isInitialized = true;
                // 传递配置给管理器
                manager.SetControllerConfig(
                    Props.stayDurationRange,
                    Props.intervalRange,
                    Props.letterLabel,
                    Props.letterText
                );
            }
        }

        /// <summary>
        /// 更新状态（由管理器调用）
        /// </summary>
        public void UpdateState(AirShipState newState, int newNextArrivalTick, int newDepartureTick)
        {
            currentState = newState;
            nextArrivalTick = newNextArrivalTick;
            departureTick = newDepartureTick;
            
            // 记录日志
            if (newState == AirShipState.Nearby)
            {
                // 发送信件通知
                if (ShouldSendLetter())
                {
                    SendArrivalLetter();
                }
            }
            else if (newState == AirShipState.Absent)
            {
                // 计算下一次抵达时间
                stayDurationDays = Props.stayDurationRange.RandomInRange;
                intervalDays = Props.intervalRange.RandomInRange;
            }
        }

        /// <summary>
        /// 是否应该发送信件
        /// </summary>
        private bool ShouldSendLetter()
        {
            if (Props.letterLabel.NullOrEmpty() || Props.letterText.NullOrEmpty())
                return false;
            
            // 避免短时间内重复发送信件
            if (lastLetterTick >= 0 && Find.TickManager.TicksGame - lastLetterTick < 60000) // 一天内不重复
                return false;
                
            return true;
        }

        /// <summary>
        /// 发送抵达信件
        /// </summary>
        private void SendArrivalLetter()
        {
            try
            {
                string label = Props.letterLabel.Formatted(
                    stayDurationDays,
                    intervalDays
                );
                
                string text = Props.letterText.Formatted(
                    stayDurationDays,
                    intervalDays
                );
                
                cachedLetter = LetterMaker.MakeLetter(
                    label,
                    text,
                    Props.letterDef ?? LetterDefOf.NeutralEvent
                );
                
                Find.LetterStack.ReceiveLetter(cachedLetter);
                lastLetterTick = Find.TickManager.TicksGame;
            }
            catch (Exception ex)
            {
                Log.Error($"发送 OberonAirShip 信件时出错: {ex}");
            }
        }

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string GetStateDescription()
        {
            switch (currentState)
            {
                case AirShipState.Nearby:
                    int ticksUntilDeparture = departureTick - Find.TickManager.TicksGame;
                    float daysUntilDeparture = ticksUntilDeparture / 60000f;
                    return $"DD_OberonAirShip_Nearby".Translate(daysUntilDeparture.ToString("F1"));
                    
                case AirShipState.Absent:
                    int ticksUntilArrival = nextArrivalTick - Find.TickManager.TicksGame;
                    float daysUntilArrival = ticksUntilArrival / 60000f;
                    return $"DD_OberonAirShip_Absent".Translate(daysUntilArrival.ToString("F1"));
                    
                case AirShipState.Arriving:
                    return "DD_OberonAirShip_Arriving".Translate();
                    
                case AirShipState.Departing:
                    return "DD_OberonAirShip_Departing".Translate();
                    
                default:
                    return "DD_OberonAirShip_Unknown".Translate();
            }
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public AirShipState CurrentState => currentState;

        /// <summary>
        /// 获取下次抵达时间（ticks）
        /// </summary>
        public int NextArrivalTick => nextArrivalTick;

        /// <summary>
        /// 获取离开时间（ticks）
        /// </summary>
        public int DepartureTick => departureTick;

        /// <summary>
        /// OberonAirShip 是否在附近
        /// </summary>
        public bool IsOberonAirShipNearby => currentState == AirShipState.Nearby;

        public override void CompTick()
        {
            base.CompTick();
            
            // 每游戏日检查一次
            if (Find.TickManager.TicksGame % 60000 == 0 && isInitialized)
            {
                // 同步管理器状态
                var manager = Find.World.GetComponent<WorldComp_OberonAirShipManager>();
                if (manager != null)
                {
                    manager.UpdateControllerState(this);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            
            Scribe_Values.Look(ref isInitialized, "isInitialized", false);
            Scribe_Values.Look(ref currentState, "currentState", AirShipState.Absent);
            Scribe_Values.Look(ref nextArrivalTick, "nextArrivalTick", -1);
            Scribe_Values.Look(ref departureTick, "departureTick", -1);
            Scribe_Values.Look(ref lastLetterTick, "lastLetterTick", -1);
            Scribe_Values.Look(ref stayDurationDays, "stayDurationDays", 0);
            Scribe_Values.Look(ref intervalDays, "intervalDays", 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            // 添加状态查看Gizmo
            if (Prefs.DevMode && isInitialized)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dev: 显示OberonAirShip状态",
                    defaultDesc = GetStateDescription(),
                    action = () =>
                    {
                        Messages.Message(GetStateDescription(), MessageTypeDefOf.SilentInput);
                    }
                };
            }
        }
    }

    /// <summary>
    /// OberonAirShip 状态枚举
    /// </summary>
    public enum AirShipState
    {
        Absent,     // 不在附近
        Arriving,   // 正在抵达
        Nearby,     // 在附近
        Departing   // 正在离开
    }

    /// <summary>
    /// CompProperties for CompOberonAirShipController
    /// </summary>
    public class CompProperties_OberonAirShipController : CompProperties
    {
        // 停留时间范围（天）
        public IntRange stayDurationRange = new IntRange(2, 5);
        
        // 间隔时间范围（天）
        public IntRange intervalRange = new IntRange(7, 14);
        
        // 信件内容
        public string letterLabel = "DD_OberonAirShip_ArrivalLetter_Label";
        public string letterText = "DD_OberonAirShip_ArrivalLetter_Text";
        public LetterDef letterDef = LetterDefOf.NeutralEvent;

        public CompProperties_OberonAirShipController()
        {
            compClass = typeof(CompOberonAirShipController);
        }
    }
}
