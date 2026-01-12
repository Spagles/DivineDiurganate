using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld.Planet;
using UnityEngine;

namespace DivineDiurganate
{
    /// <summary>
    /// 全局信仰系统管理器
    /// </summary>
    public class WorldComp_FaithSystem : WorldComponent
    {
        // 单例访问
        private static WorldComp_FaithSystem instance;
        public static WorldComp_FaithSystem Instance => instance;

        // 状态跟踪
        private bool isFaithSystemActive = false;
        private Pawn currentLeader = null;
        private int lastLeaderCheckTick = -1;

        // 信仰值数据
        private float currentFaith = 0f;
        private float maxFaith = 100f; // 默认100，实际会根据信徒数量计算
        private int lastFollowerCount = 0;
        
        // ==================== 新增：祈愿值数据 ====================
        private float currentWish = 0f;
        private float wishRecoveryRate = 10f; // 每天恢复10点祈愿值
        private int lastWishUpdateTick = -1;
        private const int WishUpdateIntervalTicks = 6000; // 每6000ticks（游戏内1/10天）更新一次祈愿值
        
        // 领袖追踪列表
        private List<Pawn> trackedLeaders = new List<Pawn>();
        public Dictionary<Pawn, float> leaderFaithCache = new Dictionary<Pawn, float>();
        public Dictionary<Pawn, float> leaderWishCache = new Dictionary<Pawn, float>(); // 新增：祈愿值缓存

        // 配置
        private const int LeaderCheckIntervalTicks = 2500; // 每2500ticks检查一次领袖
        private const float FaithPerFollower = 100f;       // 每个信徒提供100点信仰上限
        private const float MaxWishRecoveryRate = 50f;     // 最大祈愿值恢复速率（每天）
        private const float MinWishRecoveryRate = 1f;      // 最小祈愿值恢复速率（每天）

        public WorldComp_FaithSystem(World world) : base(world)
        {
            instance = this;
        }

        /// <summary>
        /// 检查信仰系统是否应该激活
        /// </summary>
        private bool ShouldFaithSystemBeActive()
        {
            // 检查玩家派系的主要文化是否拥有 DD_law_Meme
            Faction playerFaction = Faction.OfPlayer;
            if (playerFaction == null || playerFaction.ideos == null)
                return false;

            // 检查主要文化
            Ideo primaryIdeo = playerFaction.ideos.PrimaryIdeo;
            if (primaryIdeo == null)
                return false;

            // 检查是否包含 DD_law_Meme
            MemeDef lawMeme = DefDatabase<MemeDef>.GetNamedSilentFail("DD_law_Meme");
            if (lawMeme == null)
                return false;

            bool hasLawMeme = primaryIdeo.memes.Contains(lawMeme);
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Primary ideo = {primaryIdeo.name}, has DD_law_Meme = {hasLawMeme}");
            }

            return hasLawMeme;
        }

        /// <summary>
        /// 获取当前领袖
        /// </summary>
        private Pawn GetCurrentLeader()
        {
            try
            {
                Faction playerFaction = Faction.OfPlayer;
                if (playerFaction == null || playerFaction.ideos == null)
                    return null;

                // 获取玩家派系的主要意识形态
                Ideo primaryIdeo = playerFaction.ideos.PrimaryIdeo;
                if (primaryIdeo == null)
                    return null;

                // 查找 Leader 角色
                var leaderRole = primaryIdeo.PreceptsListForReading
                    .FirstOrDefault(precept => precept is Precept_Role role && 
                        role.def == PreceptDefOf.IdeoRole_Leader) as Precept_Role;

                if (leaderRole == null)
                    return null;

                // 获取被选为领袖的殖民者
                Pawn leaderPawn = leaderRole.ChosenPawnSingle();
                
                if (DebugSettings.godMode && leaderPawn != null)
                {
                    Log.Message($"FaithSystem: Current leader = {leaderPawn.NameShortColored}");
                }

                return leaderPawn;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error getting current leader: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 计算拥有 DD_law_Meme 的信徒数量
        /// </summary>
        private int CalculateFollowerCount()
        {
            try
            {
                MemeDef lawMeme = DefDatabase<MemeDef>.GetNamedSilentFail("DD_law_Meme");
                if (lawMeme == null)
                    return 0;

                int count = 0;
                
                // 获取所有殖民者
                List<Pawn> colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList();
                
                foreach (Pawn pawn in colonists)
                {
                    if (pawn?.Ideo == null)
                        continue;

                    // 检查殖民者的意识形态是否包含 DD_law_Meme
                    if (pawn.Ideo.memes.Contains(lawMeme))
                    {
                        count++;
                    }
                }

                if (DebugSettings.godMode)
                {
                    Log.Message($"FaithSystem: Found {count} followers with DD_law_Meme");
                }

                return count;
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error calculating follower count: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// 更新最大信仰值
        /// </summary>
        private void UpdateMaxFaith()
        {
            int followerCount = CalculateFollowerCount();
            float newMaxFaith = followerCount * FaithPerFollower;
            
            // 如果信徒数量变化，记录日志
            if (followerCount != lastFollowerCount)
            {
                if (DebugSettings.godMode)
                {
                    Log.Message($"FaithSystem: Follower count changed from {lastFollowerCount} to {followerCount}, max faith from {maxFaith} to {newMaxFaith}");
                }
                lastFollowerCount = followerCount;
            }
            
            maxFaith = newMaxFaith;
            
            // 确保当前信仰不超过新的上限
            if (currentFaith > maxFaith)
            {
                currentFaith = maxFaith;
            }
        }

        /// <summary>
        /// 更新祈愿值（自然恢复）
        /// </summary>
        private void UpdateWishValue()
        {
            if (!isFaithSystemActive || currentLeader == null || currentLeader.Dead || currentLeader.Downed)
            {
                if (currentWish > 0f)
                {
                    currentWish = 0f;
                }
                return;
            }

            // 计算祈愿值上限（等于剩余信仰容量）
            float wishCapacity = GetRemainingFaithCapacity();
            
            // 如果当前祈愿值已经达到上限，不更新
            if (currentWish >= wishCapacity)
            {
                return;
            }

            // 计算恢复量（按天比例计算）
            float recoveryAmount = wishRecoveryRate * (WishUpdateIntervalTicks / 60000f);
            
            // 更新祈愿值
            currentWish = Mathf.Min(currentWish + recoveryAmount, wishCapacity);

            if (DebugSettings.godMode && recoveryAmount > 0f)
            {
                Log.Message($"FaithSystem: Wish updated to {currentWish:F1}/{wishCapacity:F1}, recovery rate = {wishRecoveryRate:F1}/day");
            }
        }

        /// <summary>
        /// 计算祈愿值恢复速率
        /// </summary>
        public float CalculateWishRecoveryRate()
        {
            if (currentLeader == null)
                return wishRecoveryRate;

            float baseRate = wishRecoveryRate;
            
            // 信徒数量影响（越多信徒，恢复越快）
            baseRate += lastFollowerCount * 0.5f;

            return Mathf.Clamp(baseRate, MinWishRecoveryRate, MaxWishRecoveryRate);
        }

        /// <summary>
        /// 检查领袖是否变更
        /// </summary>
        public bool CheckForLeaderChange()
        {
            Pawn newLeader = GetCurrentLeader();
            
            // 如果领袖变化
            if (newLeader != currentLeader)
            {
                Pawn oldLeader = currentLeader;
                currentLeader = newLeader;
                
                if (DebugSettings.godMode)
                {
                    Log.Message($"FaithSystem: Leader changed from {oldLeader?.NameShortColored ?? "None"} to {newLeader?.NameShortColored ?? "None"}");
                }

                // 如果是新领袖，保存旧领袖的信仰值和祈愿值（如果有）
                if (oldLeader != null)
                {
                    if (leaderFaithCache.ContainsKey(oldLeader))
                    {
                        leaderFaithCache[oldLeader] = currentFaith;
                    }
                    else
                    {
                        leaderFaithCache.Add(oldLeader, currentFaith);
                    }
                    
                    if (leaderWishCache.ContainsKey(oldLeader))
                    {
                        leaderWishCache[oldLeader] = currentWish;
                    }
                    else
                    {
                        leaderWishCache.Add(oldLeader, currentWish);
                    }
                }

                // 如果是已有领袖，恢复其信仰值和祈愿值
                if (newLeader != null)
                {
                    if (leaderFaithCache.ContainsKey(newLeader))
                    {
                        currentFaith = leaderFaithCache[newLeader];
                    }
                    else
                    {
                        // 新领袖从0开始，或者使用默认值
                        currentFaith = Mathf.Min(currentFaith, 50f); // 新领袖最多继承50点信仰
                        leaderFaithCache.Add(newLeader, currentFaith);
                    }

                    if (leaderWishCache.ContainsKey(newLeader))
                    {
                        currentWish = leaderWishCache[newLeader];
                    }
                    else
                    {
                        // 新领袖从0开始
                        currentWish = 0f;
                        leaderWishCache.Add(newLeader, currentWish);
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查领袖状态
        /// </summary>
        private bool CheckLeaderStatus()
        {
            if (currentLeader == null)
                return false;

            // 检查领袖是否死亡、倒下等
            if (currentLeader.Dead || currentLeader.Downed || !currentLeader.Spawned)
            {
                if (DebugSettings.godMode)
                {
                    Log.Message($"FaithSystem: Leader {currentLeader.NameShortColored} is unavailable (Dead: {currentLeader.Dead}, Downed: {currentLeader.Downed}, Spawned: {currentLeader.Spawned})");
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// 更新系统状态
        /// </summary>
        private void UpdateSystemState()
        {
            bool shouldBeActive = ShouldFaithSystemBeActive();
            
            if (shouldBeActive != isFaithSystemActive)
            {
                isFaithSystemActive = shouldBeActive;
                
                if (DebugSettings.godMode)
                {
                    Log.Message($"FaithSystem: System {(isFaithSystemActive ? "activated" : "deactivated")}");
                }

                if (!isFaithSystemActive)
                {
                    // 系统停用，重置状态
                    currentLeader = null;
                    currentFaith = 0f;
                    currentWish = 0f;
                }
            }

            if (isFaithSystemActive)
            {
                // 检查领袖变更
                CheckForLeaderChange();
                
                // 检查领袖状态
                if (!CheckLeaderStatus())
                {
                    currentLeader = null;
                }
                
                // 更新最大信仰值
                UpdateMaxFaith();
                
                // 更新祈愿值恢复速率
                wishRecoveryRate = CalculateWishRecoveryRate();
            }
        }

        /// <summary>
        /// 获取当前信仰值
        /// </summary>
        public float CurrentFaith => currentFaith;

        /// <summary>
        /// 获取最大信仰值
        /// </summary>
        public float MaxFaith => maxFaith;

        /// <summary>
        /// 获取信仰百分比
        /// </summary>
        public float FaithPercent => maxFaith > 0 ? currentFaith / maxFaith : 0f;

        /// <summary>
        /// 信仰系统是否活跃
        /// </summary>
        public bool IsActive => isFaithSystemActive;

        /// <summary>
        /// 获取当前领袖
        /// </summary>
        public Pawn CurrentLeader => currentLeader;

        /// <summary>
        /// 获取信徒数量
        /// </summary>
        public int FollowerCount => lastFollowerCount;

        // ==================== 新增：祈愿值属性 ====================

        /// <summary>
        /// 获取当前祈愿值
        /// </summary>
        public float CurrentWish => currentWish;

        /// <summary>
        /// 获取祈愿值上限（等于剩余信仰容量）
        /// </summary>
        public float MaxWish => GetRemainingFaithCapacity();

        /// <summary>
        /// 获取祈愿值百分比
        /// </summary>
        public float WishPercent => MaxWish > 0 ? currentWish / MaxWish : 0f;

        /// <summary>
        /// 获取祈愿值恢复速率（每天）
        /// </summary>
        public float WishRecoveryRate => wishRecoveryRate;

        // ==================== 新增：祈愿值操作方法 ====================

        /// <summary>
        /// 按数量增加祈愿值
        /// </summary>
        public void AddWish(float amount, string reason = "")
        {
            if (!isFaithSystemActive || amount <= 0f)
                return;

            float wishCapacity = MaxWish;
            float oldWish = currentWish;
            currentWish = Mathf.Min(currentWish + amount, wishCapacity);
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Wish added: {amount:F1} (from {oldWish:F1} to {currentWish:F1}) - Reason: {reason}");
            }
        }

        /// <summary>
        /// 按比例增加祈愿值（相对于祈愿值上限）
        /// </summary>
        public void AddWishByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return;

            float amount = MaxWish * percent;
            AddWish(amount, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 消耗祈愿值
        /// </summary>
        public bool TryConsumeWish(float amount, string reason = "")
        {
            if (!isFaithSystemActive || amount <= 0f || currentWish < amount)
                return false;

            float oldWish = currentWish;
            currentWish -= amount;
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Wish consumed: {amount:F1} (from {oldWish:F1} to {currentWish:F1}) - Reason: {reason}");
            }

            return true;
        }

        /// <summary>
        /// 按比例消耗祈愿值
        /// </summary>
        public bool TryConsumeWishByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return false;

            float amount = MaxWish * percent;
            return TryConsumeWish(amount, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 设置祈愿值
        /// </summary>
        public void SetWish(float value, string reason = "")
        {
            if (!isFaithSystemActive)
                return;

            float wishCapacity = MaxWish;
            float oldWish = currentWish;
            currentWish = Mathf.Clamp(value, 0f, wishCapacity);
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Wish set: {value:F1} (from {oldWish:F1} to {currentWish:F1}) - Reason: {reason}");
            }
        }

        /// <summary>
        /// 按比例设置祈愿值
        /// </summary>
        public void SetWishByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent < 0f || percent > 1f)
                return;

            float value = MaxWish * percent;
            SetWish(value, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 填充祈愿值到上限
        /// </summary>
        public void FillWishToFull(string reason = "")
        {
            if (!isFaithSystemActive)
                return;

            float amountNeeded = MaxWish - currentWish;
            if (amountNeeded > 0f)
            {
                AddWish(amountNeeded, $"{reason} (fill to full)");
            }
        }

        // ==================== 信仰值操作方法 ====================

        /// <summary>
        /// 按数量增加信仰值
        /// </summary>
        public void AddFaith(float amount, string reason = "")
        {
            if (!isFaithSystemActive || amount <= 0f)
                return;

            float oldFaith = currentFaith;
            currentFaith = Mathf.Min(currentFaith + amount, maxFaith);
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Faith added: {amount:F1} (from {oldFaith:F1} to {currentFaith:F1}) - Reason: {reason}");
            }
        }

        /// <summary>
        /// 按比例增加信仰值
        /// </summary>
        public void AddFaithByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return;

            float amount = maxFaith * percent;
            AddFaith(amount, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 消耗信仰值
        /// </summary>
        public bool TryConsumeFaith(float amount, string reason = "")
        {
            if (!isFaithSystemActive || amount <= 0f || currentFaith < amount)
                return false;

            float oldFaith = currentFaith;
            currentFaith -= amount;
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Faith consumed: {amount:F1} (from {oldFaith:F1} to {currentFaith:F1}) - Reason: {reason}");
            }

            return true;
        }

        /// <summary>
        /// 按比例消耗信仰值
        /// </summary>
        public bool TryConsumeFaithByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return false;

            float amount = maxFaith * percent;
            return TryConsumeFaith(amount, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 设置信仰值
        /// </summary>
        public void SetFaith(float value, string reason = "")
        {
            if (!isFaithSystemActive)
                return;

            float oldFaith = currentFaith;
            currentFaith = Mathf.Clamp(value, 0f, maxFaith);
            
            if (DebugSettings.godMode)
            {
                Log.Message($"FaithSystem: Faith set: {value:F1} (from {oldFaith:F1} to {currentFaith:F1}) - Reason: {reason}");
            }
        }

        /// <summary>
        /// 按比例设置信仰值
        /// </summary>
        public void SetFaithByPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent < 0f || percent > 1f)
                return;

            float value = maxFaith * percent;
            SetFaith(value, $"{reason} (by percent: {percent:P0})");
        }

        /// <summary>
        /// 将信仰值设置为最大值的百分比
        /// </summary>
        public void FillFaithToPercent(float percent, string reason = "")
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return;

            float targetFaith = maxFaith * percent;
            float amountNeeded = targetFaith - currentFaith;
            
            if (amountNeeded > 0f)
            {
                AddFaith(amountNeeded, $"{reason} (fill to {percent:P0})");
            }
            else if (amountNeeded < 0f)
            {
                TryConsumeFaith(-amountNeeded, $"{reason} (reduce to {percent:P0})");
            }
        }

        // ==================== 检查方法 ====================

        /// <summary>
        /// 检查是否有足够的信仰值
        /// </summary>
        public bool HasEnoughFaith(float amount)
        {
            return isFaithSystemActive && currentFaith >= amount;
        }

        /// <summary>
        /// 检查是否有足够的祈愿值
        /// </summary>
        public bool HasEnoughWish(float amount)
        {
            return isFaithSystemActive && currentWish >= amount;
        }

        /// <summary>
        /// 检查是否有足够百分比的信仰值
        /// </summary>
        public bool HasEnoughFaithPercent(float percent)
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return false;

            float requiredAmount = maxFaith * percent;
            return currentFaith >= requiredAmount;
        }

        /// <summary>
        /// 检查是否有足够百分比的祈愿值
        /// </summary>
        public bool HasEnoughWishPercent(float percent)
        {
            if (!isFaithSystemActive || percent <= 0f || percent > 1f)
                return false;

            float requiredAmount = MaxWish * percent;
            return currentWish >= requiredAmount;
        }

        /// <summary>
        /// 获取可用信仰值百分比
        /// </summary>
        public float GetAvailableFaithPercent()
        {
            if (!isFaithSystemActive || maxFaith <= 0f)
                return 0f;
            
            return currentFaith / maxFaith;
        }

        /// <summary>
        /// 获取可用祈愿值百分比
        /// </summary>
        public float GetAvailableWishPercent()
        {
            if (!isFaithSystemActive || MaxWish <= 0f)
                return 0f;
            
            return currentWish / MaxWish;
        }

        /// <summary>
        /// 获取剩余信仰值容量
        /// </summary>
        public float GetRemainingFaithCapacity()
        {
            if (!isFaithSystemActive)
                return 0f;
            
            return maxFaith - currentFaith;
        }

        /// <summary>
        /// 获取剩余信仰值容量百分比
        /// </summary>
        public float GetRemainingFaithCapacityPercent()
        {
            if (!isFaithSystemActive || maxFaith <= 0f)
                return 0f;
            
            return (maxFaith - currentFaith) / maxFaith;
        }

        /// <summary>
        /// 获取剩余祈愿值容量
        /// </summary>
        public float GetRemainingWishCapacity()
        {
            if (!isFaithSystemActive)
                return 0f;
            
            return MaxWish - currentWish;
        }

        /// <summary>
        /// 获取剩余祈愿值容量百分比
        /// </summary>
        public float GetRemainingWishCapacityPercent()
        {
            if (!isFaithSystemActive || MaxWish <= 0f)
                return 0f;
            
            return (MaxWish - currentWish) / MaxWish;
        }

        // ==================== 批量操作方法 ====================

        /// <summary>
        /// 批量增加信仰值（逐个增加，直到达到上限）
        /// </summary>
        public void AddFaithBatch(float[] amounts, string reason = "")
        {
            if (!isFaithSystemActive)
                return;

            float totalAdded = 0f;
            foreach (float amount in amounts)
            {
                if (amount <= 0f)
                    continue;

                float oldFaith = currentFaith;
                currentFaith = Mathf.Min(currentFaith + amount, maxFaith);
                totalAdded += currentFaith - oldFaith;
                
                if (currentFaith >= maxFaith)
                    break;
            }

            if (DebugSettings.godMode && totalAdded > 0f)
            {
                Log.Message($"FaithSystem: Batch faith added: {totalAdded:F1} (total now: {currentFaith:F1}) - Reason: {reason}");
            }
        }

        /// <summary>
        /// 批量消耗信仰值（逐个消耗，直到不足）
        /// </summary>
        public bool TryConsumeFaithBatch(float[] amounts, string reason = "")
        {
            if (!isFaithSystemActive)
                return false;

            float totalConsumed = 0f;
            foreach (float amount in amounts)
            {
                if (amount <= 0f)
                    continue;

                if (currentFaith < amount)
                    return false;

                currentFaith -= amount;
                totalConsumed += amount;
            }

            if (DebugSettings.godMode && totalConsumed > 0f)
            {
                Log.Message($"FaithSystem: Batch faith consumed: {totalConsumed:F1} (total now: {currentFaith:F1}) - Reason: {reason}");
            }

            return true;
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            int currentTick = Find.TickManager.TicksGame;

            // 定期检查领袖（每LeaderCheckIntervalTicks）
            if (currentTick - lastLeaderCheckTick >= LeaderCheckIntervalTicks)
            {
                UpdateSystemState();
                lastLeaderCheckTick = currentTick;
            }

            // 定期更新祈愿值（每WishUpdateIntervalTicks）
            if (isFaithSystemActive && currentTick - lastWishUpdateTick >= WishUpdateIntervalTicks)
            {
                UpdateWishValue();
                lastWishUpdateTick = currentTick;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref isFaithSystemActive, "isFaithSystemActive", false);
            Scribe_References.Look(ref currentLeader, "currentLeader");
            Scribe_Values.Look(ref currentFaith, "currentFaith", 0f);
            Scribe_Values.Look(ref maxFaith, "maxFaith", 100f);
            Scribe_Values.Look(ref lastFollowerCount, "lastFollowerCount", 0);
            Scribe_Values.Look(ref lastLeaderCheckTick, "lastLeaderCheckTick", -1);
            
            // 保存祈愿值数据
            Scribe_Values.Look(ref currentWish, "currentWish", 0f);
            Scribe_Values.Look(ref wishRecoveryRate, "wishRecoveryRate", 10f);
            Scribe_Values.Look(ref lastWishUpdateTick, "lastWishUpdateTick", -1);

            // 保存领袖信仰缓存
            Scribe_Collections.Look(ref leaderFaithCache, "leaderFaithCache", LookMode.Reference, LookMode.Value);
            Scribe_Collections.Look(ref leaderWishCache, "leaderWishCache", LookMode.Reference, LookMode.Value);
            Scribe_Collections.Look(ref trackedLeaders, "trackedLeaders", LookMode.Reference);
        }
    }
}
