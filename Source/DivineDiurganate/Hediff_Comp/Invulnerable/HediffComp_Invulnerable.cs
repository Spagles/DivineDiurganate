using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace DivineDiurganate
{
    /// <summary>
    /// 伤害免疫组件
    /// 当此组件存在时，Pawn免疫所有伤害
    /// </summary>
    public class HediffComp_Invulnerable : HediffComp
    {
        /// <summary>
        /// 组件属性
        /// </summary>
        public HediffCompProperties_Invulnerable Props => (HediffCompProperties_Invulnerable)props;
        
        /// <summary>
        /// 是否应该阻挡伤害
        /// </summary>
        public virtual bool ShouldBlockDamage(DamageInfo dinfo)
        {
            // 检查是否启用免疫
            if (!Props.enabled)
                return false;
            
            // 检查伤害类型限制
            if (Props.excludedDamageDefs != null && Props.excludedDamageDefs.Contains(dinfo.Def))
                return false;
            
            // 默认免疫所有伤害
            return true;
        }
        
        /// <summary>
        /// 当伤害被阻挡时的处理
        /// </summary>
        public virtual void OnDamageBlocked(DamageInfo dinfo)
        {
            // 显示免疫效果（如果需要）
            if (Props.showImmuneEffect && Pawn.Spawned)
            {
                ShowImmuneEffect(dinfo);
            }
        }
        
        /// <summary>
        /// 显示免疫效果
        /// </summary>
        protected virtual void ShowImmuneEffect(DamageInfo dinfo)
        {
            // 显示文字效果
            MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "DD_ImmuneToDamage".Translate(), Props.immuneTextColor, Props.immuneTextDuration);
            
            // 显示粒子效果
            if (Props.immuneMoteDef != null)
            {
                MoteMaker.MakeStaticMote(Pawn.DrawPos, Pawn.Map, Props.immuneMoteDef, 1f);
            }
        }
        
        /// <summary>
        /// 在Hediff描述中显示信息
        /// </summary>
        public override string CompTipStringExtra
        {
            get
            {
                if (Props.enabled)
                {
                    string tip = "DD_ImmuneToAllDamage".Translate();
                    
                    // 如果有排除的伤害类型，显示出来
                    if (Props.excludedDamageDefs != null && Props.excludedDamageDefs.Count > 0)
                    {
                        tip += "\n" + "DD_ExcludedDamageTypes".Translate() + ": ";
                        foreach (var def in Props.excludedDamageDefs)
                        {
                            tip += def.label + ", ";
                        }
                        tip = tip.TrimEnd(',', ' ');
                    }
                    
                    return tip;
                }
                return null;
            }
        }
        
        /// <summary>
        /// 在Hediff标签中显示信息
        /// </summary>
        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (Props.enabled)
                {
                    return "DD_Immune".Translate();
                }
                return null;
            }
        }
    }
    
    /// <summary>
    /// 伤害免疫组件的属性
    /// </summary>
    public class HediffCompProperties_Invulnerable : HediffCompProperties
    {
        // 是否启用免疫
        public bool enabled = true;
        
        // 排除的伤害类型（如果配置，这些伤害类型不受免疫影响）
        public List<DamageDef> excludedDamageDefs;
        
        // 免疫时显示的效果
        public bool showImmuneEffect = true;
        public ThingDef immuneMoteDef;
        public SoundDef immuneSound;
        public Color immuneTextColor = Color.green;
        public float immuneTextDuration = 2.5f;
        
        public HediffCompProperties_Invulnerable()
        {
            compClass = typeof(HediffComp_Invulnerable);
        }
    }
}
