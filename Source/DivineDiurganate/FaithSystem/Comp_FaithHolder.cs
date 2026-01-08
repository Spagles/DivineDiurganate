using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 领袖信仰持有组件
    /// </summary>
    public class Comp_FaithHolder : ThingComp
    {
        private WorldComp_FaithSystem faithSystem;
        
        public CompProperties_FaithHolder Props =>
            (CompProperties_FaithHolder)props;
            
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            faithSystem = WorldComp_FaithSystem.Instance;
            
            // 如果是领袖，注册到信仰系统
            if (faithSystem != null && faithSystem.IsActive)
            {
                Pawn pawn = parent as Pawn;
                if (pawn != null && faithSystem.CurrentLeader == pawn)
                {
                    // 领袖pawn已经加载，确保系统知道
                    faithSystem.CheckForLeaderChange();
                }
            }
        }
        
        public void PostDeSpawn(Map map)
        {
            // 如果这个pawn是领袖且离开了地图，通知系统
            if (faithSystem != null && faithSystem.CurrentLeader == parent)
            {
                faithSystem.CheckForLeaderChange();
            }
        }
        
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            
            // 如果领袖被摧毁（死亡），通知系统
            if (faithSystem != null && faithSystem.CurrentLeader == parent)
            {
                faithSystem.CheckForLeaderChange();
            }
        }
        
        /// <summary>
        /// 获取此pawn的信仰Gizmo
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 只有领袖才显示信仰Gizmo
            if (faithSystem != null && faithSystem.IsActive && faithSystem.CurrentLeader == parent)
            {
                var faithGizmo = new Gizmo_FaithStatus();
                if (faithGizmo.ShouldDisplay())
                {
                    yield return faithGizmo;
                }
            }
        }
        
        /// <summary>
        /// 检查此pawn是否是当前领袖
        /// </summary>
        public bool IsCurrentLeader()
        {
            return faithSystem != null && faithSystem.CurrentLeader == parent;
        }
        
        /// <summary>
        /// 获取此pawn的信仰值（如果是领袖）
        /// </summary>
        public float? GetFaithValue()
        {
            if (IsCurrentLeader() && faithSystem != null)
            {
                return faithSystem.CurrentFaith;
            }
            return null;
        }
        
        /// <summary>
        /// 获取此pawn的最大信仰值（如果是领袖）
        /// </summary>
        public float? GetMaxFaithValue()
        {
            if (IsCurrentLeader() && faithSystem != null)
            {
                return faithSystem.MaxFaith;
            }
            return null;
        }
        
        /// <summary>
        /// 增加此pawn的信仰值（如果是领袖）
        /// </summary>
        public bool TryAddFaith(float amount, string reason = "")
        {
            if (IsCurrentLeader() && faithSystem != null)
            {
                faithSystem.AddFaith(amount, reason);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 消耗此pawn的信仰值（如果是领袖）
        /// </summary>
        public bool TryConsumeFaith(float amount, string reason = "")
        {
            if (IsCurrentLeader() && faithSystem != null)
            {
                return faithSystem.TryConsumeFaith(amount, reason);
            }
            return false;
        }
    }
    
    /// <summary>
    /// CompProperties for Comp_FaithHolder
    /// </summary>
    public class CompProperties_FaithHolder : CompProperties
    {
        public CompProperties_FaithHolder()
        {
            compClass = typeof(Comp_FaithHolder);
        }
    }
}
