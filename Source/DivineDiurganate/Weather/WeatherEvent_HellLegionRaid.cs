using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 地狱军团袭击天气事件
    /// 触发时生成DD_Hell_Legion阵营的袭击，采用空投舱入场
    /// </summary>
    public class WeatherEvent_HellLegionRaid : WeatherEvent
    {
        // 事件是否已过期
        private bool expired = false;
        
        // 是否已触发袭击
        private bool raidTriggered = false;
        
        // 袭击参数
        private IncidentParms raidParms;
        
        // 触发位置（可选）
        private IntVec3? triggerCell = null;
        
        // 消息延迟（用于显示消息）
        private int messageDelay = 60; // 1秒后显示消息
        private bool messageShown = false;
        
        // 事件持续时间（如果袭击失败，事件也会过期）
        private int eventDurationTicks = 600; // 10秒
        private int ticksActive = 0;

        public override bool Expired => expired;

        public WeatherEvent_HellLegionRaid(Map map) : base(map)
        {
            // 初始化袭击参数
            InitializeRaidParams(map);
        }
        
        public WeatherEvent_HellLegionRaid(Map map, IntVec3 triggerCell) : base(map)
        {
            this.triggerCell = triggerCell;
            InitializeRaidParams(map);
        }

        /// <summary>
        /// 初始化袭击参数
        /// </summary>
        private void InitializeRaidParams(Map map)
        {
            raidParms = new IncidentParms();
            raidParms.target = map;
            raidParms.points = CalculateRaidPoints(map);
            raidParms.raidStrategy = GetHellLegionRaidStrategy();
            raidParms.raidArrivalMode = GetHellLegionArrivalMode();
            raidParms.faction = GetHellLegionFaction();
            
            // 设置生成位置
            if (triggerCell.HasValue && triggerCell.Value.IsValid && triggerCell.Value.InBounds(map))
            {
                raidParms.spawnCenter = triggerCell.Value;
            }
            else
            {
                raidParms.spawnCenter = GetDefaultSpawnCenter(map);
            }
            
            // 设置袭击方向
            raidParms.raidArrivalModeForQuickMilitaryAid = true;
            
            Log.Message($"[DivineDiurganate] WeatherEvent_HellLegionRaid initialized for map {map}");
            Log.Message($"[DivineDiurganate] Raid points: {raidParms.points}");
        }

        /// <summary>
        /// 计算袭击点数
        /// </summary>
        private float CalculateRaidPoints(Map map)
        {
            // 基于地图财富值和难度计算袭击点数
            float finalPoints = StorytellerUtility.DefaultThreatPointsNow(map);
            
            // 确保最小点数
            float minPoints = 500f;
            float maxPoints = 10000f;
            
            return Mathf.Clamp(finalPoints, minPoints, maxPoints);
        }

        /// <summary>
        /// 获取地狱军团袭击策略
        /// </summary>
        private RaidStrategyDef GetHellLegionRaidStrategy()
        {
            // 尝试使用地狱军团的专用策略
            RaidStrategyDef hellStrategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail("DD_HellLegion_Strategy");
            if (hellStrategy != null)
            {
                return hellStrategy;
            }
            
            // 备选：使用立即空投策略
            RaidStrategyDef immediateDrop = DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateDrop");
            if (immediateDrop != null)
            {
                return immediateDrop;
            }
            
            // 默认：使用攻击性最强的策略
            return RaidStrategyDefOf.ImmediateAttack;
        }

        /// <summary>
        /// 获取地狱军团入场方式
        /// </summary>
        private PawnsArrivalModeDef GetHellLegionArrivalMode()
        {
            // 使用空投舱入场
            PawnsArrivalModeDef dropPod = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("DropPod");
            if (dropPod != null)
            {
                return dropPod;
            }
            
            // 备选：使用中心空投
            PawnsArrivalModeDef centerDrop = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("CenterDrop");
            if (centerDrop != null)
            {
                return centerDrop;
            }
            
            // 默认：边缘攻击
            return PawnsArrivalModeDefOf.EdgeWalkIn;
        }

        /// <summary>
        /// 获取地狱军团阵营
        /// </summary>
        private Faction GetHellLegionFaction()
        {
            // 获取DD_Hell_Legion阵营
            Faction hellFaction = Find.FactionManager.FirstFactionOfDef(FactionDef.Named("DD_Hell_Legion"));
            
            return hellFaction;
        }
       
        /// <summary>
        /// 获取默认生成中心
        /// </summary>
        private IntVec3 GetDefaultSpawnCenter(Map map)
        {
            // 尝试在地图边缘找到一个合适的生成点
            if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 result, map, CellFinder.EdgeRoadChance_Hostile))
            {
                return result;
            }
            
            // 如果找不到，返回地图中心
            return map.Center;
        }

        /// <summary>
        /// 触发天气事件
        /// </summary>
        public override void FireEvent()
        {
            Log.Message("[DivineDiurganate] WeatherEvent_HellLegionRaid fired!");
            
            // 如果已触发，则跳过
            if (raidTriggered)
                return;
                
            // 触发袭击
            TriggerHellLegionRaid();
            
            // 标记为已触发
            raidTriggered = true;
        }

        /// <summary>
        /// 触发地狱军团袭击
        /// </summary>
        private void TriggerHellLegionRaid()
        {
            try
            {
                // 确保参数有效
                if (raidParms.faction == null)
                {
                    Log.Error("[DivineDiurganate] Cannot trigger raid: Faction is null");
                    expired = true;
                    return;
                }
                
                if (raidParms.target == null)
                {
                    Log.Error("[DivineDiurganate] Cannot trigger raid: Target map is null");
                    expired = true;
                    return;
                }
                
                // 获取袭击事件定义
                IncidentDef raidIncident = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy");
                if (raidIncident == null)
                {
                    raidIncident = IncidentDefOf.RaidEnemy;
                }
                
                // 执行袭击事件
                bool raidSuccess = raidIncident.Worker.TryExecute(raidParms);
                
                if (raidSuccess)
                {
                    Log.Message($"[DivineDiurganate] Hell Legion raid triggered successfully with {raidParms.points} points");
                }
                else
                {
                    Log.Error("[DivineDiurganate] Failed to trigger Hell Legion raid");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] Error triggering Hell Legion raid: {ex}");
            }
        }

        /// <summary>
        /// 天气事件更新
        /// </summary>
        public override void WeatherEventTick()
        {
            ticksActive++;
            
            // 延迟显示消息
            if (!messageShown && ticksActive >= messageDelay)
            {
                ShowRaidWarningMessage();
                messageShown = true;
            }
            
            // 检查事件是否应该过期
            if (ticksActive >= eventDurationTicks)
            {
                expired = true;
                Log.Message("[DivineDiurganate] WeatherEvent_HellLegionRaid expired");
            }
        }

        /// <summary>
        /// 显示袭击警告消息
        /// </summary>
        private void ShowRaidWarningMessage()
        {
            try
            {
                // 创建消息文本
                string messageText;
                
                if (raidParms.faction != null)
                {
                    string factionName = raidParms.faction.Name;
                    messageText = $"地狱军团({factionName})从天空降临，准备发动袭击!";
                }
                else
                {
                    messageText = "未知的敌人从天空降临，准备发动袭击!";
                }
                
                // 发送消息
                Messages.Message(messageText, MessageTypeDefOf.ThreatBig);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] Error showing raid warning message: {ex}");
            }
        }

        /// <summary>
        /// 天气事件绘制（可选）
        /// </summary>
        public override void WeatherEventDraw()
        {
            // 可以在事件期间绘制一些特效
            if (ticksActive < eventDurationTicks && ticksActive % 20 == 0)
            {
                // 随机在地图边缘绘制警告闪光
                IntVec3 edgeCell = GetRandomEdgeCell();
                if (edgeCell.InBounds(map))
                {
                    // 绘制闪光效果
                    Vector3 drawPos = edgeCell.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead);
                    GenDraw.DrawArrowPointingAt(drawPos, true);
                }
            }
        }

        /// <summary>
        /// 获取随机边缘单元格
        /// </summary>
        private IntVec3 GetRandomEdgeCell()
        {
            int edgeDistance = 5;
            int x = Rand.Range(edgeDistance, map.Size.x - edgeDistance);
            int z = Rand.Range(0, map.Size.z);
            
            // 随机选择上边缘或下边缘
            if (Rand.Value > 0.5f)
            {
                z = edgeDistance; // 下边缘
            }
            else
            {
                z = map.Size.z - edgeDistance; // 上边缘
            }
            
            return new IntVec3(x, 0, z);
        }

        /// <summary>
        /// 天空目标（可选，如果需要改变天空颜色）
        /// </summary>
        public override SkyTarget SkyTarget => new SkyTarget(1f, new SkyColorSet(Color.red, Color.black, Color.red, 1f), 1f, 1f);

        /// <summary>
        /// 天空目标插值因子（控制天空颜色变化强度）
        /// </summary>
        public override float SkyTargetLerpFactor
        {
            get
            {
                // 在事件期间逐渐增加天空红色调
                float progress = (float)ticksActive / eventDurationTicks;
                return Mathf.Clamp01(progress * 0.5f); // 最大50%的红色天空
            }
        }
    }
    
    /// <summary>
    /// 地狱军团袭击天气事件生成器
    /// 用于在特定条件下生成这个天气事件
    /// </summary>
    public class WeatherEventMaker_HellLegionRaid : WeatherEventMaker
    {
        // 事件发生的概率（每帧）
        public float baseChancePerTick = 0.0001f;
        
        // 只有在特定天气下才触发
        public List<WeatherDef> requiredWeathers;
        
        // 需要的最低地图财富值
        public float minWealth = 10000f;
        
        // 需要的最低殖民者数量
        public int minColonists = 3;
        
        // 冷却时间（ticks）
        public int cooldownTicks = 60000; // 1天
        private int lastRaidTick = -999999;
        
        public void WeatherEventTick(Map map, float strength)
        {
            // 检查冷却时间
            if (Find.TickManager.TicksGame - lastRaidTick < cooldownTicks)
                return;
            
            // 检查天气条件
            if (requiredWeathers != null && requiredWeathers.Count > 0)
            {
                bool hasRequiredWeather = false;
                foreach (var weatherDef in requiredWeathers)
                {
                    if (map.weatherManager.curWeather == weatherDef)
                    {
                        hasRequiredWeather = true;
                        break;
                    }
                }
                
                if (!hasRequiredWeather)
                    return;
            }
            
            // 检查财富条件
            float mapWealth = map.wealthWatcher.WealthTotal;
            if (mapWealth < minWealth)
                return;
            
            // 检查殖民者数量
            int colonistCount = map.mapPawns.FreeColonistsCount;
            if (colonistCount < minColonists)
                return;
            
            // 计算最终概率（受难度影响）
            float finalChance = baseChancePerTick * strength;
            float difficultyFactor = Find.Storyteller.difficulty.threatScale;
            finalChance *= difficultyFactor;
            
            // 如果地图已经有袭击在进行，降低概率
            if (map.attackTargetsCache.TargetsHostileToColony.Count > 0)
            {
                finalChance *= 0.1f;
            }
            
            // 随机检查
            if (Rand.Value < finalChance)
            {
                TriggerHellLegionRaidEvent(map);
                lastRaidTick = Find.TickManager.TicksGame;
            }
        }
        
        /// <summary>
        /// 触发地狱军团袭击事件
        /// </summary>
        private void TriggerHellLegionRaidEvent(Map map)
        {
            try
            {
                // 创建天气事件
                WeatherEvent_HellLegionRaid weatherEvent = new WeatherEvent_HellLegionRaid(map);
                
                // 添加到地图的天气事件管理器
                map.weatherManager.eventHandler.AddEvent(weatherEvent);
                
                // 触发事件
                weatherEvent.FireEvent();
                
                Log.Message($"[DivineDiurganate] Hell Legion raid weather event triggered on map {map}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] Error triggering Hell Legion raid weather event: {ex}");
            }
        }
    }
}
