using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    [HarmonyPatch(typeof(PawnGenerator))]
    [HarmonyPatch("GeneratePawn", typeof(PawnGenerationRequest))]
    public static class Patch_GeneratePawn_HeadType
    {
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            Pawn pawn = __result;
            if (pawn == null) return;
            
            // 只处理人类
            if (!pawn.RaceProps.Humanlike) return;

            // 检查 FixedIdentityExtension
            HeadTypeExtension fixedIdentityExt = request.KindDef?.GetModExtension<HeadTypeExtension>();
            if (fixedIdentityExt == null) return;
            
            // 只保留头部类型修改的部分
            if (pawn.story != null && !fixedIdentityExt.forcedHeadTypeDef.NullOrEmpty())
            {
                HeadTypeDef headTypeDef = DefDatabase<HeadTypeDef>.GetNamedSilentFail(fixedIdentityExt.forcedHeadTypeDef);
                if (headTypeDef != null && 
                    (headTypeDef.gender == Gender.None || headTypeDef.gender == pawn.gender) && 
                    pawn.story.headType != headTypeDef)
                {
                    pawn.story.headType = headTypeDef;
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                }
            }
        }
    }

    /// <summary>
    /// Harmony补丁：在生成Pawn时过滤特性
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator))]
    [HarmonyPatch("GeneratePawn", typeof(PawnGenerationRequest))]
    public static class Patch_GeneratePawn_TraitFilter
    {
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            Pawn pawn = __result;
            if (pawn == null || !pawn.RaceProps.Humanlike)
                return;

            // 检查PawnKindDef是否有TraitFilterExtension
            TraitFilterExtension traitFilter = request.KindDef?.GetModExtension<TraitFilterExtension>();
            if (traitFilter == null)
            {
                // 如果PawnKindDef没有，检查是否有全局设置
                traitFilter = GetGlobalTraitFilter();
                if (traitFilter == null)
                    return;
            }

            // 获取允许的特性列表
            HashSet<string> allowedTraits = GetAllowedTraits(pawn, traitFilter, request);

            // 过滤特性
            FilterPawnTraits(pawn, allowedTraits, traitFilter);
        }

        /// <summary>
        /// 获取全局特性过滤器（如果需要）
        /// </summary>
        private static TraitFilterExtension GetGlobalTraitFilter()
        {
            // 这里可以添加全局设置，比如通过Mod设置或特定条件启用
            // 目前返回null，表示只处理有扩展的PawnKindDef
            return null;
        }

        /// <summary>
        /// 获取允许的特性集合
        /// </summary>
        private static HashSet<string> GetAllowedTraits(Pawn pawn, TraitFilterExtension traitFilter, PawnGenerationRequest request)
        {
            HashSet<string> allowedTraits = new HashSet<string>();

            // 1. 从背景故事获取强制特性
            if (traitFilter.keepBackstoryTraits && pawn.story != null)
            {
                // 童年背景
                if (pawn.story.Childhood != null)
                {
                    foreach (var trait in pawn.story.Childhood.forcedTraits)
                    {
                        string key = GetTraitKey(trait.def, trait.degree);
                        allowedTraits.Add(key);
                    }
                }

                // 成年背景
                if (pawn.story.Adulthood != null)
                {
                    foreach (var trait in pawn.story.Adulthood.forcedTraits)
                    {
                        string key = GetTraitKey(trait.def, trait.degree);
                        allowedTraits.Add(key);
                    }
                }
            }

            // 2. 从PawnKindDef获取强制特性
            if (traitFilter.keepPawnKindTraits && request.KindDef != null && request.KindDef.forcedTraits != null)
            {
                foreach (var trait in request.KindDef.forcedTraits)
                {
                    string key = GetTraitKey(trait.def, trait.degree);
                    allowedTraits.Add(key);
                }
            }

            // 3. 添加例外特性
            if (traitFilter.exceptions != null)
            {
                foreach (var traitDef in traitFilter.exceptions)
                {
                    // 检查是否已有该特性的某个等级
                    bool hasTrait = false;
                    foreach (string key in allowedTraits)
                    {
                        if (key.StartsWith(traitDef.defName + "_"))
                        {
                            hasTrait = true;
                            break;
                        }
                    }

                    // 如果没有，添加默认等级0
                    if (!hasTrait)
                    {
                        allowedTraits.Add(GetTraitKey(traitDef, 0));
                    }
                }
            }

            return allowedTraits;
        }

        /// <summary>
        /// 生成特性唯一键
        /// </summary>
        private static string GetTraitKey(TraitDef traitDef, int? degree)
        {
            return $"{traitDef.defName}_{degree}";
        }

        /// <summary>
        /// 过滤Pawn的特性
        /// </summary>
        private static void FilterPawnTraits(Pawn pawn, HashSet<string> allowedTraits, TraitFilterExtension traitFilter)
        {
            if (pawn.story == null || pawn.story.traits == null)
                return;

            // 创建要删除的特性列表
            List<Trait> traitsToRemove = new List<Trait>();

            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                string traitKey = GetTraitKey(trait.def, trait.Degree);

                // 检查是否在允许列表中
                if (!allowedTraits.Contains(traitKey))
                {
                    traitsToRemove.Add(trait);
                }
                else
                {
                    // 如果在允许列表中，从集合中移除，避免重复
                    allowedTraits.Remove(traitKey);
                }
            }

            // 删除不允许的特性
            foreach (Trait trait in traitsToRemove)
            {
                pawn.story.traits.RemoveTrait(trait);

                // 可选：记录日志
                if (Prefs.DevMode)
                {
                    Log.Message($"[TraitFilter] Removed trait: {trait.def.defName} (degree: {trait.Degree}) from {pawn.Name}");
                }
            }

            // 如果需要，添加缺失的强制特性
            if (traitFilter.keepBackstoryTraits || traitFilter.keepPawnKindTraits)
            {
                foreach (string traitKey in allowedTraits)
                {
                    string[] parts = traitKey.Split('_');
                    if (parts.Length >= 2)
                    {
                        string traitDefName = parts[0];
                        int degree = 0;
                        if (int.TryParse(parts[1], out degree))
                        {
                            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                            if (traitDef != null)
                            {
                                // 检查是否已经存在该特性（不同等级）
                                bool alreadyHasTrait = false;
                                foreach (Trait existingTrait in pawn.story.traits.allTraits)
                                {
                                    if (existingTrait.def == traitDef)
                                    {
                                        alreadyHasTrait = true;
                                        break;
                                    }
                                }

                                if (!alreadyHasTrait)
                                {
                                    pawn.story.traits.GainTrait(new Trait(traitDef, degree, false));

                                    // 可选：记录日志
                                    if (Prefs.DevMode)
                                    {
                                        Log.Message($"[TraitFilter] Added forced trait: {traitDef.defName} (degree: {degree}) to {pawn.Name}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}