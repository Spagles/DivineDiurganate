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
            // 默认：使用攻击性最强的策略
            return RaidStrategyDefOf.ImmediateAttack;
        }

        /// <summary>
        /// 获取地狱军团入场方式
        /// </summary>
        private PawnsArrivalModeDef GetHellLegionArrivalMode()
        {
            PawnsArrivalModeDef edgeTeleport = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("DD_Hell_EdgeTeleport");
            if (edgeTeleport != null)
            {
                return edgeTeleport;
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
        /// 获取默认生成中心（避开庇护区域）
        /// </summary>
        private IntVec3 GetDefaultSpawnCenter(Map map)
        {
            // 尝试多次寻找不在庇护区域内的生成点
            for (int attempt = 0; attempt < 20; attempt++)
            {
                IntVec3 candidate;

                // 使用边缘单元格生成
                if (RCellFinder.TryFindRandomPawnEntryCell(out candidate, map, CellFinder.EdgeRoadChance_Hostile, false))
                {
                    // 检查是否在庇护区域内
                    if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                    {
                        return candidate;
                    }
                }
            }

            // 如果找不到合适的位置，尝试地图上的任何非庇护单元格
            for (int attempt = 0; attempt < 10; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomCell(map);
                if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                {
                    return candidate;
                }
            }

            // 如果所有尝试都失败，返回地图中心（即使可能在庇护区域内）
            Log.Warning("[DivineDiurganate] 无法找到不在庇护区域内的生成点，使用地图中心");
            return map.Center;
        }

        /// <summary>
        /// 触发天气事件
        /// </summary>
        public override void FireEvent()
        {
            // 如果已触发，则跳过
            if (raidTriggered)
                return;

            // 触发袭击
            TriggerHellLegionRaid();

            // 标记为已触发
            raidTriggered = true;
        }

        /// <summary>
        /// 触发地狱军团袭击（检查庇护区域）
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

                // 检查生成中心是否在庇护区域内
                if (raidParms.spawnCenter != null && raidParms.spawnCenter.IsValid)
                {
                    if (WeatherShelterManager.IsCellSheltered(map, raidParms.spawnCenter))
                    {
                        // 重新选择不在庇护区域内的生成点
                        IntVec3 newSpawnCenter = FindNonShelteredSpawnCenter(map);
                        if (newSpawnCenter != raidParms.spawnCenter)
                        {
                            raidParms.spawnCenter = newSpawnCenter;
                        }
                    }
                }

                // 获取袭击事件定义
                IncidentDef raidIncident = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy");
                if (raidIncident == null)
                {
                    raidIncident = IncidentDefOf.RaidEnemy;
                }

                // 执行袭击事件
                bool raidSuccess = raidIncident.Worker.TryExecute(raidParms);
            }
            catch (Exception ex)
            {
                Log.Error($"[DivineDiurganate] Error triggering Hell Legion raid: {ex}");
            }
        }

        /// <summary>
        /// 寻找不在庇护区域内的生成点
        /// </summary>
        private IntVec3 FindNonShelteredSpawnCenter(Map map)
        {
            // 尝试边缘单元格
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 candidate, map, 0.5f, false))
                {
                    if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                    {
                        return candidate;
                    }
                }
            }

            // 尝试随机单元格
            for (int attempt = 0; attempt < 30; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomCell(map);
                if (!WeatherShelterManager.IsCellSheltered(map, candidate))
                {
                    return candidate;
                }
            }

            // 如果所有尝试都失败，返回原始生成点
            return raidParms.spawnCenter;
        }

        /// <summary>
        /// 天气事件更新
        /// </summary>
        public override void WeatherEventTick()
        {
            ticksActive++;

            // 检查事件是否应该过期
            if (ticksActive >= eventDurationTicks)
            {
                expired = true;
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
        public float minWealth = 1f;
        
        // 需要的最低殖民者数量
        public int minColonists = 1;
        
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
