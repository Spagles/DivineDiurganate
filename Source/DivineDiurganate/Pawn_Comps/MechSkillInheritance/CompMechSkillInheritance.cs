// File: CompMechSkillInheritance.cs
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 组件：机甲技能继承自驾驶员
    /// 每个技能以驾驶员中该技能的最高等级为准
    /// </summary>
    public class CompMechSkillInheritance : ThingComp
    {
        public CompProperties_MechSkillInheritance Props => 
            (CompProperties_MechSkillInheritance)props;
        
        // 缓存引用
        private CompMechPilotHolder pilotHolder;
        private Pawn mechPawn;
        
        // 上次检查的技能哈希值（用于避免不必要的更新）
        private int lastPilotSkillHash = 0;
        private int lastUpdateTick = -9999;
        
        // 技能缓存（技能定义 -> 等级）
        private Dictionary<SkillDef, int> cachedSkillLevels = new Dictionary<SkillDef, int>();
        
        // 技能覆盖（可选）
        private Dictionary<SkillDef, int> skillOverrides = new Dictionary<SkillDef, int>();
        
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            
            mechPawn = parent as Pawn;
            pilotHolder = parent.TryGetComp<CompMechPilotHolder>();
            
            // 初始技能更新
            if (mechPawn != null && mechPawn.skills != null)
            {
                UpdateSkillsFromPilots();
            }
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (mechPawn == null || mechPawn.skills == null || pilotHolder == null)
                return;
            
            int currentTick = Find.TickManager.TicksGame;
            
            // 检查是否需要更新技能
            if (ShouldUpdateSkills(currentTick))
            {
                UpdateSkillsFromPilots();
                lastUpdateTick = currentTick;
            }
        }
        
        /// <summary>
        /// 检查是否需要更新技能
        /// </summary>
        private bool ShouldUpdateSkills(int currentTick)
        {
            // 检查更新频率
            if (currentTick - lastUpdateTick < Props.updateIntervalTicks)
                return false;
            
            // 检查驾驶员变化
            if (pilotHolder == null)
                return false;
            
            // 计算当前驾驶员的技能哈希值
            int currentHash = CalculatePilotSkillHash();
            
            // 如果哈希值变化，需要更新
            if (currentHash != lastPilotSkillHash)
            {
                lastPilotSkillHash = currentHash;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 计算驾驶员技能哈希值（用于检测变化）
        /// </summary>
        private int CalculatePilotSkillHash()
        {
            if (pilotHolder == null || !pilotHolder.HasPilots)
                return 0;
            
            int hash = 17;
            
            foreach (var pilot in pilotHolder.GetPilots())
            {
                hash = hash * 31 + pilot.thingIDNumber;
                
                if (pilot.skills != null)
                {
                    foreach (var skill in pilot.skills.skills)
                    {
                        hash = hash * 31 + skill.def.GetHashCode();
                        hash = hash * 31 + skill.Level;
                        hash = hash * 31 + (skill.passion.GetHashCode());
                    }
                }
            }
            
            return hash;
        }
        
        /// <summary>
        /// 从驾驶员更新技能
        /// </summary>
        private void UpdateSkillsFromPilots()
        {
            if (mechPawn == null || mechPawn.skills == null)
                return;
            
            // 清空缓存
            cachedSkillLevels.Clear();
            
            // 获取所有驾驶员
            var pilots = pilotHolder?.GetPilots().ToList() ?? new List<Pawn>();
            
            // 遍历机甲的所有技能
            foreach (var mechSkill in mechPawn.skills.skills)
            {
                if (mechSkill.TotallyDisabled)
                    continue;
                    
                // 检查是否有技能覆盖
                if (skillOverrides.TryGetValue(mechSkill.def, out int overrideLevel))
                {
                    mechSkill.Level = overrideLevel;
                    cachedSkillLevels[mechSkill.def] = overrideLevel;
                    continue;
                }
                
                // 如果没有驾驶员，使用默认值
                if (pilots.Count == 0)
                {
                    int baseLevel = Props.baseSkillLevelWhenNoPilot;
                    mechSkill.Level = baseLevel;
                    cachedSkillLevels[mechSkill.def] = baseLevel;
                    continue;
                }
                
                // 找出所有驾驶员中该技能的最高等级
                int maxLevel = 0;
                foreach (var pilot in pilots)
                {
                    if (pilot.skills == null)
                        continue;
                        
                    var pilotSkill = pilot.skills.GetSkill(mechSkill.def);
                    if (pilotSkill != null && !pilotSkill.TotallyDisabled)
                    {
                        // 应用技能倍率
                        int pilotLevel = pilotSkill.Level;
                        float multiplier = Props.skillMultiplierForPilots;
                        int adjustedLevel = (int)(pilotLevel * multiplier);
                        
                        // 应用等级上限
                        if (Props.maxSkillLevel > 0)
                        {
                            adjustedLevel = System.Math.Min(adjustedLevel, Props.maxSkillLevel);
                        }
                        
                        if (adjustedLevel > maxLevel)
                            maxLevel = adjustedLevel;
                    }
                }
                
                // 应用最低等级限制
                if (Props.minSkillLevel > 0)
                {
                    maxLevel = System.Math.Max(maxLevel, Props.minSkillLevel);
                }
                
                // 设置技能等级
                mechSkill.Level = maxLevel;
                cachedSkillLevels[mechSkill.def] = maxLevel;
            }
            
            // 触发更新事件
            OnSkillsUpdated();
        }
        
        /// <summary>
        /// 技能更新后的事件
        /// </summary>
        private void OnSkillsUpdated()
        {
            // 可以在这里添加技能更新后的逻辑
            // 例如：通知其他组件、播放音效等
            
            if (Props.debugLogging)
            {
                Log.Message($"[DD] Mech skills updated for {parent.LabelShort}");
                foreach (var kv in cachedSkillLevels)
                {
                    Log.Message($"  {kv.Key.LabelCap}: {kv.Value}");
                }
            }
        }
        
        /// <summary>
        /// 获取当前技能信息
        /// </summary>
        public string GetSkillInfo()
        {
            string info = $"<b>{parent.LabelShort}的技能信息</b>\n\n";
            
            if (pilotHolder == null || !pilotHolder.HasPilots)
            {
                info += "无驾驶员\n";
            }
            else
            {
                info += $"驾驶员: {pilotHolder.CurrentPilotCount}人\n\n";
                
                // 显示每个技能及其来源
                if (mechPawn != null && mechPawn.skills != null)
                {
                    foreach (var skill in mechPawn.skills.skills)
                    {
                        if (skill.TotallyDisabled)
                            continue;
                            
                        int mechLevel = skill.Level;
                        string source = "默认";
                        
                        if (skillOverrides.ContainsKey(skill.def))
                        {
                            source = "覆盖";
                        }
                        else if (pilotHolder.HasPilots)
                        {
                            // 找出提供该技能的驾驶员
                            var bestPilot = GetBestPilotForSkill(skill.def);
                            if (bestPilot != null)
                            {
                                source = $"{bestPilot.LabelShort} ({bestPilot.skills.GetSkill(skill.def)?.Level ?? 0}级)";
                            }
                        }
                        
                        info += $"{skill.def.LabelCap}: {mechLevel}级 (来源: {source})\n";
                    }
                }
            }
            
            return info;
        }
        
        /// <summary>
        /// 获取指定技能的最佳驾驶员
        /// </summary>
        private Pawn GetBestPilotForSkill(SkillDef skillDef)
        {
            if (pilotHolder == null || !pilotHolder.HasPilots)
                return null;
                
            Pawn bestPilot = null;
            int bestLevel = -1;
            
            foreach (var pilot in pilotHolder.GetPilots())
            {
                if (pilot.skills == null)
                    continue;
                    
                var pilotSkill = pilot.skills.GetSkill(skillDef);
                if (pilotSkill != null && !pilotSkill.TotallyDisabled)
                {
                    int level = pilotSkill.Level;
                    if (level > bestLevel)
                    {
                        bestLevel = level;
                        bestPilot = pilot;
                    }
                }
            }
            
            return bestPilot;
        }
        
        /// <summary>
        /// 设置技能覆盖
        /// </summary>
        public void SetSkillOverride(SkillDef skillDef, int level)
        {
            if (skillDef == null)
                return;
                
            skillOverrides[skillDef] = level;
            
            // 立即更新
            if (mechPawn != null && mechPawn.skills != null)
            {
                var skill = mechPawn.skills.GetSkill(skillDef);
                if (skill != null && !skill.TotallyDisabled)
                {
                    skill.Level = level;
                }
            }
        }
        
        /// <summary>
        /// 清除技能覆盖
        /// </summary>
        public void ClearSkillOverride(SkillDef skillDef)
        {
            if (skillDef == null)
                return;
                
            skillOverrides.Remove(skillDef);
            
            // 重新从驾驶员更新
            UpdateSkillsFromPilots();
        }
        
        /// <summary>
        /// 获取调试按钮
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            // 只在开发模式下显示调试按钮
            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: 技能信息",
                    defaultDesc = GetSkillInfo(),
                    icon = TexCommand.DesirePower,
                    action = () =>
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(
                            GetSkillInfo(),
                            "关闭",
                            null,
                            null,
                            null,
                            "机甲技能信息"
                        ));
                    }
                };
                
                yield return new Command_Action
                {
                    defaultLabel = "DEBUG: 强制更新技能",
                    defaultDesc = "立即从驾驶员更新所有技能",
                    icon = TexCommand.Install,
                    action = () =>
                    {
                        UpdateSkillsFromPilots();
                        Messages.Message("技能已强制更新", MessageTypeDefOf.TaskCompletion);
                    }
                };
                
                // 如果没有驾驶员，显示设置默认等级的按钮
                if ((pilotHolder == null || !pilotHolder.HasPilots) && mechPawn != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = $"DEBUG: 设为默认({Props.baseSkillLevelWhenNoPilot}级)",
                        defaultDesc = $"将所有技能设为默认等级({Props.baseSkillLevelWhenNoPilot}级)",
                        //icon = TexCommand.SetTargetFuelLevel,
                        action = () =>
                        {
                            foreach (var skill in mechPawn.skills.skills)
                            {
                                if (!skill.TotallyDisabled)
                                {
                                    skill.Level = Props.baseSkillLevelWhenNoPilot;
                                }
                            }
                            Messages.Message("技能已设为默认等级", MessageTypeDefOf.TaskCompletion);
                        }
                    };
                }
            }
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            // 保存技能覆盖
            Scribe_Collections.Look(ref skillOverrides, "skillOverrides", LookMode.Def, LookMode.Value);
            
            // 如果加载后技能覆盖不为null，应用它们
            if (Scribe.mode == LoadSaveMode.PostLoadInit && skillOverrides != null)
            {
                // 重新从驾驶员更新技能（覆盖会保留）
                UpdateSkillsFromPilots();
            }
        }
    }
    
    /// <summary>
    /// 组件属性
    /// </summary>
    public class CompProperties_MechSkillInheritance : CompProperties
    {
        // 基础设置
        public int updateIntervalTicks = 60; // 更新间隔（60ticks = 1秒）
        public int baseSkillLevelWhenNoPilot = 0; // 无驾驶员时的基础技能等级
        public float skillMultiplierForPilots = 1.0f; // 技能倍率（1.0 = 100%继承）
        public int minSkillLevel = 0; // 最低技能等级限制
        public int maxSkillLevel = 0; // 最高技能等级限制（0 = 无限制）
        
        // 调试
        public bool debugLogging = false;
        
        // 技能组设置（可选）
        public List<SkillDef> prioritizedSkills; // 优先技能（会额外加成）
        public List<SkillDef> excludedSkills; // 排除的技能（不继承）
        
        public CompProperties_MechSkillInheritance()
        {
            compClass = typeof(CompMechSkillInheritance);
        }
        
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            
            if (baseSkillLevelWhenNoPilot < 0)
            {
                yield return $"baseSkillLevelWhenNoPilot must be >= 0 for {parentDef.defName}";
            }
            
            if (skillMultiplierForPilots < 0)
            {
                yield return $"skillMultiplierForPilots must be >= 0 for {parentDef.defName}";
            }
            
            if (minSkillLevel < 0)
            {
                yield return $"minSkillLevel must be >= 0 for {parentDef.defName}";
            }
            
            if (maxSkillLevel > 0 && maxSkillLevel < minSkillLevel)
            {
                yield return $"maxSkillLevel must be >= minSkillLevel for {parentDef.defName}";
            }
        }
    }
}
