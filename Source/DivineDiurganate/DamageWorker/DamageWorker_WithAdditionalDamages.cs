using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 支持附加额外伤害的DamageWorker
    /// 类似于 additionalHediffs，但是附加额外的 DamageInfo
    /// </summary>
    public class DamageWorker_WithAdditionalDamages : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            // 首先应用原始伤害
            DamageResult result = base.Apply(dinfo, thing);
            
            // 获取额外伤害配置
            var extension = def.GetModExtension<AdditionalDamagesExtension>();
            if (extension == null || extension.additionalDamages.NullOrEmpty())
                return result;
            
            // 如果目标是Pawn且未死亡，应用额外伤害
            if (thing is Pawn pawn && !pawn.Dead)
            {
                foreach (var additionalDamage in extension.additionalDamages)
                {
                    ApplyAdditionalDamage(dinfo, pawn, additionalDamage);
                }
            }
            
            return result;
        }
        
        private void ApplyAdditionalDamage(DamageInfo originalDinfo, Pawn pawn, AdditionalDamageEntry entry)
        {
            if (entry.damageDef == null)
                return;
            
            // 计算额外伤害量
            float amount;
            if (entry.amount > 0)
            {
                // 使用固定伤害量
                amount = entry.amount;
            }
            else
            {
                // 使用倍率计算
                amount = originalDinfo.Amount * entry.damageMultiplier;
            }
            
            if (amount <= 0f)
                return;
            
            // 概率检查
            if (entry.chance < 1f && !Rand.Chance(entry.chance))
                return;
            
            // 创建额外伤害信息
            DamageInfo additionalDinfo = new DamageInfo(
                def: entry.damageDef,
                amount: amount,
                armorPenetration: entry.armorPenetration >= 0 ? entry.armorPenetration : originalDinfo.ArmorPenetrationInt,
                angle: originalDinfo.Angle,
                instigator: originalDinfo.Instigator,
                hitPart: null,
                weapon: originalDinfo.Weapon,
                category: originalDinfo.Category,
                intendedTarget: originalDinfo.IntendedTarget
            );
            
            // 应用额外伤害
            pawn.TakeDamage(additionalDinfo);
        }
    }
    
    /// <summary>
    /// 额外伤害配置 - DefModExtension
    /// </summary>
    public class AdditionalDamagesExtension : DefModExtension
    {
        public List<AdditionalDamageEntry> additionalDamages = new List<AdditionalDamageEntry>();
    }
    
    /// <summary>
    /// 单个额外伤害条目
    /// </summary>
    public class AdditionalDamageEntry
    {
        /// <summary>
        /// 额外伤害的DamageDef
        /// </summary>
        public DamageDef damageDef;
        
        /// <summary>
        /// 固定伤害量（如果>0则使用此值，否则使用damageMultiplier）
        /// </summary>
        public float amount = 0f;
        
        /// <summary>
        /// 伤害倍率（相对于原始伤害）
        /// 例如：2.0 = 200%原伤害
        /// </summary>
        public float damageMultiplier = 1f;
        
        /// <summary>
        /// 触发概率 (0-1)
        /// </summary>
        public float chance = 1f;
        
        /// <summary>
        /// 穿甲值（-1表示使用原始伤害的穿甲值）
        /// </summary>
        public float armorPenetration = -1f;
    }
}
