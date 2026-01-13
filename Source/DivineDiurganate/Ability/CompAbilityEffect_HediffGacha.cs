using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// Hediff抽卡技能效果组件
    /// 对目标释放后，从Hediff池中随机抽取指定数量的Hediff让玩家选择
    /// 选择完成后，目标获得选中的Hediff
    /// </summary>
    public class CompAbilityEffect_HediffGacha : CompAbilityEffect
    {
        // 缓存目标，用于窗口选择后的回调
        private Pawn cachedTarget;
        
        public new CompProperties_AbilityHediffGacha Props => 
            (CompProperties_AbilityHediffGacha)props;
        
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            
            // 验证目标
            if (!target.IsValid || target.Pawn == null)
                return;
                
            Pawn targetPawn = target.Pawn;
            cachedTarget = targetPawn;
            
            // 从Hediff池中随机抽取
            List<HediffDef> drawnHediffs = DrawHediffsFromPool(Props.choiceCount);
            
            if (drawnHediffs.NullOrEmpty())
            {
                Log.Warning("[DivineDiurganate] HediffGacha: No valid hediffs drawn from pool!");
                return;
            }
            
            // 打开选择窗口
            Window_HediffSelection window = new Window_HediffSelection(
                drawnHediffs,
                targetPawn,
                OnHediffSelected,
                Props.windowTitle,
                Props.allowCancel
            );
            
            Find.WindowStack.Add(window);
        }
        
        /// <summary>
        /// 从Hediff池中随机抽取指定数量的Hediff
        /// </summary>
        private List<HediffDef> DrawHediffsFromPool(int count)
        {
            if (Props.hediffPool.NullOrEmpty())
            {
                Log.Error("[DivineDiurganate] HediffGacha: hediffPool is empty!");
                return null;
            }
            
            // 复制池子以便进行无重复抽取
            List<HediffPoolEntry> availablePool = Props.hediffPool.ListFullCopy();
            List<HediffDef> result = new List<HediffDef>();
            
            // 如果池子不足以抽取指定数量，则取池子的全部
            int actualCount = System.Math.Min(count, availablePool.Count);
            
            for (int i = 0; i < actualCount; i++)
            {
                if (availablePool.Count == 0)
                    break;
                    
                // 根据权重随机选择
                HediffPoolEntry selected;
                if (Props.useWeights)
                {
                    selected = availablePool.RandomElementByWeight(e => e.weight);
                }
                else
                {
                    selected = availablePool.RandomElement();
                }
                
                if (selected?.hediff != null)
                {
                    result.Add(selected.hediff);
                    
                    // 从池子中移除已选择的，确保不重复
                    if (!Props.allowDuplicates)
                    {
                        availablePool.Remove(selected);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 玩家选择Hediff后的回调
        /// </summary>
        private void OnHediffSelected(HediffDef selectedHediff)
        {
            if (selectedHediff == null || cachedTarget == null || cachedTarget.Dead)
            {
                Log.Warning("[DivineDiurganate] HediffGacha: Invalid selection or target is dead");
                return;
            }
            
            // 为目标添加Hediff
            ApplyHediffToTarget(cachedTarget, selectedHediff);
            
            // 播放特效（如果有）
            if (Props.selectionFleck != null && cachedTarget.Map != null)
            {
                FleckMaker.Static(cachedTarget.Position, cachedTarget.Map, Props.selectionFleck);
            }
            
            // 显示消息（如果配置启用）
            if (Props.showSelectionMessage)
            {
                string message = Props.selectionMessageKey.NullOrEmpty() 
                    ? "DD_HediffGacha_Selected".Translate(cachedTarget.LabelShortCap, selectedHediff.LabelCap)
                    : Props.selectionMessageKey.Translate(cachedTarget.LabelShortCap, selectedHediff.LabelCap);
                    
                Messages.Message(message, cachedTarget, MessageTypeDefOf.PositiveEvent);
            }
            
            // 清空缓存
            cachedTarget = null;
        }
        
        /// <summary>
        /// 将Hediff应用到目标
        /// </summary>
        private void ApplyHediffToTarget(Pawn target, HediffDef hediffDef)
        {
            if (target == null || hediffDef == null)
                return;
                
            // 如果已有该Hediff且设置为替换，则先移除
            if (Props.replaceExisting)
            {
                Hediff existingHediff = target.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (existingHediff != null)
                {
                    target.health.RemoveHediff(existingHediff);
                }
            }
            
            // 创建新的Hediff
            BodyPartRecord targetPart = null;
            if (Props.onlyBrain)
            {
                targetPart = target.health.hediffSet.GetBrain();
            }
            
            Hediff hediff = HediffMaker.MakeHediff(hediffDef, target, targetPart);
            
            // 设置严重度（如果有指定）
            if (Props.initialSeverity >= 0f)
            {
                hediff.Severity = Props.initialSeverity;
            }
            
            // 设置持续时间（如果Hediff支持）
            HediffComp_Disappears disappearsComp = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappearsComp != null && Props.durationSeconds > 0)
            {
                disappearsComp.ticksToDisappear = (int)(Props.durationSeconds * 60f);
            }
            
            // 添加Hediff
            target.health.AddHediff(hediff);
        }
        
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;
                
            // 验证目标是Pawn
            if (target.Pawn == null)
            {
                if (throwMessages)
                    Messages.Message("DD_HediffGacha_NeedPawnTarget".Translate(), 
                                    MessageTypeDefOf.RejectInput);
                return false;
            }
            
            // 验证Hediff池不为空
            if (Props.hediffPool.NullOrEmpty())
            {
                if (throwMessages)
                    Messages.Message("DD_HediffGacha_EmptyPool".Translate(), 
                                    MessageTypeDefOf.RejectInput);
                return false;
            }
            
            return true;
        }
        
        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return target.Pawn != null && !Props.hediffPool.NullOrEmpty();
        }
    }
    
    /// <summary>
    /// Hediff池条目 - 包含Hediff定义和权重
    /// </summary>
    public class HediffPoolEntry
    {
        /// <summary>
        /// Hediff定义
        /// </summary>
        public HediffDef hediff;
        
        /// <summary>
        /// 抽取权重（仅在useWeights为true时生效）
        /// </summary>
        public float weight = 1f;
        
        /// <summary>
        /// 可选的描述覆盖（用于在选择界面显示自定义描述）
        /// </summary>
        public string descriptionOverride;
    }
    
    /// <summary>
    /// Hediff抽卡技能属性
    /// </summary>
    public class CompProperties_AbilityHediffGacha : CompProperties_AbilityEffect
    {
        /// <summary>
        /// Hediff池 - 可供抽取的Hediff列表
        /// </summary>
        public List<HediffPoolEntry> hediffPool;
        
        /// <summary>
        /// 每次抽取的选项数量（默认3个，即三选一）
        /// </summary>
        public int choiceCount = 3;
        
        /// <summary>
        /// 是否使用权重进行随机抽取
        /// </summary>
        public bool useWeights = true;
        
        /// <summary>
        /// 是否允许抽取重复的Hediff
        /// </summary>
        public bool allowDuplicates = false;
        
        /// <summary>
        /// 窗口标题翻译键
        /// </summary>
        public string windowTitle = "DD_HediffGacha_Title";
        
        /// <summary>
        /// 是否允许取消选择
        /// </summary>
        public bool allowCancel = false;
        
        /// <summary>
        /// 选择后的特效
        /// </summary>
        public FleckDef selectionFleck;
        
        /// <summary>
        /// 是否显示选择消息
        /// </summary>
        public bool showSelectionMessage = true;
        
        /// <summary>
        /// 选择消息的翻译键（可选，默认使用DD_HediffGacha_Selected）
        /// </summary>
        public string selectionMessageKey;
        
        /// <summary>
        /// Hediff初始严重度（负数表示使用默认值）
        /// </summary>
        public float initialSeverity = -1f;
        
        /// <summary>
        /// Hediff持续时间（秒，0表示永久）
        /// </summary>
        public float durationSeconds = 0f;
        
        /// <summary>
        /// 是否只应用到大脑
        /// </summary>
        public bool onlyBrain = false;
        
        /// <summary>
        /// 是否替换已存在的相同Hediff
        /// </summary>
        public bool replaceExisting = true;
        
        public CompProperties_AbilityHediffGacha()
        {
            compClass = typeof(CompAbilityEffect_HediffGacha);
        }
    }
}
