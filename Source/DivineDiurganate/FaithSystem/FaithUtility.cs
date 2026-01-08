using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 信仰系统工具类
    /// </summary>
    public static class FaithUtility
    {
        /// <summary>
        /// 检查pawn是否是信仰系统的领袖
        /// </summary>
        public static bool IsFaithLeader(Pawn pawn)
        {
            if (pawn == null)
                return false;
                
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            return faithSystem.CurrentLeader == pawn;
        }
        
        /// <summary>
        /// 获取信仰值（如果pawn是领袖）
        /// </summary>
        public static float? GetFaithValue(Pawn pawn)
        {
            if (!IsFaithLeader(pawn))
                return null;
                
            var faithSystem = WorldComp_FaithSystem.Instance;
            return faithSystem?.CurrentFaith;
        }
        
        /// <summary>
        /// 获取最大信仰值（如果pawn是领袖）
        /// </summary>
        public static float? GetMaxFaithValue(Pawn pawn)
        {
            if (!IsFaithLeader(pawn))
                return null;
                
            var faithSystem = WorldComp_FaithSystem.Instance;
            return faithSystem?.MaxFaith;
        }
        
        /// <summary>
        /// 获取信仰百分比（如果pawn是领袖）
        /// </summary>
        public static float? GetFaithPercent(Pawn pawn)
        {
            if (!IsFaithLeader(pawn))
                return null;
                
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null)
                return null;
                
            return faithSystem.FaithPercent;
        }
        
        // ==================== 新增：信仰值操作方法 ====================
        
        /// <summary>
        /// 按数量增加信仰值
        /// </summary>
        public static bool AddFaith(float amount, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            faithSystem.AddFaith(amount, reason);
            return true;
        }
        
        /// <summary>
        /// 按比例增加信仰值
        /// </summary>
        public static bool AddFaithByPercent(float percent, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            faithSystem.AddFaithByPercent(percent, reason);
            return true;
        }
        
        /// <summary>
        /// 检查是否有足够的信仰值执行操作
        /// </summary>
        public static bool HasEnoughFaith(float requiredAmount)
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            return faithSystem.CurrentFaith >= requiredAmount;
        }
        
        /// <summary>
        /// 按数量消耗信仰值
        /// </summary>
        public static bool TryConsumeFaith(float amount, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            return faithSystem.TryConsumeFaith(amount, reason);
        }
        
        /// <summary>
        /// 按比例消耗信仰值
        /// </summary>
        public static bool TryConsumeFaithByPercent(float percent, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            return faithSystem.TryConsumeFaithByPercent(percent, reason);
        }
        
        /// <summary>
        /// 设置信仰值
        /// </summary>
        public static bool SetFaith(float value, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            faithSystem.SetFaith(value, reason);
            return true;
        }
        
        /// <summary>
        /// 按比例设置信仰值
        /// </summary>
        public static bool SetFaithByPercent(float percent, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            faithSystem.SetFaithByPercent(percent, reason);
            return true;
        }
        
        /// <summary>
        /// 填充信仰值到指定百分比
        /// </summary>
        public static bool FillFaithToPercent(float percent, string reason = "")
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            faithSystem.FillFaithToPercent(percent, reason);
            return true;
        }
        
        // ==================== 新增：检查方法 ====================
        
        /// <summary>
        /// 检查是否有足够百分比的信仰值
        /// </summary>
        public static bool HasEnoughFaithPercent(float percent)
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return false;
                
            return faithSystem.HasEnoughFaithPercent(percent);
        }
        
        /// <summary>
        /// 获取可用信仰值百分比
        /// </summary>
        public static float GetAvailableFaithPercent()
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return 0f;
                
            return faithSystem.GetAvailableFaithPercent();
        }
        
        /// <summary>
        /// 获取剩余信仰值容量
        /// </summary>
        public static float GetRemainingFaithCapacity()
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return 0f;
                
            return faithSystem.GetRemainingFaithCapacity();
        }
        
        /// <summary>
        /// 获取剩余信仰值容量百分比
        /// </summary>
        public static float GetRemainingFaithCapacityPercent()
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null || !faithSystem.IsActive)
                return 0f;
                
            return faithSystem.GetRemainingFaithCapacityPercent();
        }
        
        /// <summary>
        /// 获取信仰系统状态描述
        /// </summary>
        public static string GetFaithSystemStatus()
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem == null)
                return "Faith System: Not initialized";
                
            if (!faithSystem.IsActive)
                return "Faith System: Inactive";
                
            return $"Faith System: Active\nLeader: {faithSystem.CurrentLeader?.NameShortColored ?? "None"}\nFaith: {faithSystem.CurrentFaith:F0}/{faithSystem.MaxFaith:F0} ({faithSystem.FaithPercent:P1})\nFollowers: {faithSystem.FollowerCount}";
        }
        
        /// <summary>
        /// 检查殖民者是否拥有DD_law_Meme
        /// </summary>
        public static bool HasLawMeme(Pawn pawn)
        {
            if (pawn?.Ideo == null)
                return false;
                
            MemeDef lawMeme = DefDatabase<MemeDef>.GetNamedSilentFail("DD_law_Meme");
            if (lawMeme == null)
                return false;
                
            return pawn.Ideo.memes.Contains(lawMeme);
        }
        
        /// <summary>
        /// 计算拥有DD_law_Meme的殖民者数量
        /// </summary>
        public static int CountFollowersWithLawMeme()
        {
            var faithSystem = WorldComp_FaithSystem.Instance;
            if (faithSystem != null && faithSystem.IsActive)
            {
                return faithSystem.FollowerCount;
            }
            
            int count = 0;
            var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists;
            
            foreach (Pawn pawn in colonists)
            {
                if (HasLawMeme(pawn))
                {
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// 计算最大信仰值（基于信徒数量）
        /// </summary>
        public static float CalculateMaxFaith()
        {
            int followerCount = CountFollowersWithLawMeme();
            return followerCount * 100f; // 每个信徒100点
        }
        
        /// <summary>
        /// 获取信仰效果加成（已废弃，保留用于兼容性）
        /// </summary>
        [System.Obsolete("Use direct faith value manipulation methods instead")]
        public static float GetFaithEffectMultiplier(FaithEffectType effectType)
        {
            return 1f; // 已删除自动效果系统
        }
    }
    
    /// <summary>
    /// 信仰效果类型（已废弃，保留用于兼容性）
    /// </summary>
    public enum FaithEffectType
    {
        ColonyMood,     // 殖民地心情
        WorkSpeed,      // 工作效率
        ResearchSpeed,  // 研究速度
        TradePrice      // 交易价格
    }
}
