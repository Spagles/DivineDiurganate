using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;
using FacialAnimation;

namespace DivineDiurganate
{
    /// <summary>
    /// 修复版 - 添加更多调试信息
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EyeballPatchManager
    {
        static EyeballPatchManager()
        {
            Log.Message("[DD] EyeballPatchManager initializing...");
            
            bool faLoaded = ModLister.GetActiveModWithIdentifier("nals.facialanimation") != null;
            Log.Message($"[DD] FacialAnimation loaded: {faLoaded}");
            
            if (!faLoaded)
            {
                Log.Message("[DD] FacialAnimation not found. Eyeball override disabled.");
                return;
            }
            
            try
            {
                var harmony = new Harmony("DivineDiurganate.EyeballPatch");
                
                // 方法1：补丁GatherPawnParam方法
                Log.Message("[DD] Looking for GatherPawnParam method...");
                var gatherMethod = AccessTools.Method("FacialAnimation.FacialAnimationControllerComp:GatherPawnParam");
                if (gatherMethod != null)
                {
                    Log.Message("[DD] Found GatherPawnParam method, patching...");
                    var prefix = new HarmonyMethod(typeof(EyeballPatches).GetMethod("GatherPawnParam_Prefix"));
                    harmony.Patch(gatherMethod, prefix: prefix);
                    Log.Message("[DD] GatherPawnParam patched.");
                }
                else
                {
                    Log.Warning("[DD] Could not find GatherPawnParam method.");
                }
                
                // 方法2：补丁Pawn.SpawnSetup作为备用
                Log.Message("[DD] Patching Pawn.SpawnSetup...");
                var spawnMethod = typeof(Pawn).GetMethod("SpawnSetup");
                if (spawnMethod != null)
                {
                    var postfix = new HarmonyMethod(typeof(EyeballPatches).GetMethod("SpawnSetup_Postfix"));
                    harmony.Patch(spawnMethod, postfix: postfix);
                    Log.Message("[DD] Pawn.SpawnSetup patched.");
                }
                
                Log.Message("[DD] All patches applied successfully.");
                
                // 调试：列出所有FaceTypeDef
                Log.Message("[DD] List of FaceTypeDefs:");
                int count = 0;
                foreach (var def in DefDatabase<FaceTypeDef>.AllDefs)
                {
                    Log.Message($"  - {def.defName} (race: {def.raceName})");
                    count++;
                }
                Log.Message($"[DD] Total FaceTypeDefs: {count}");
                
                // 检查我们的Def是否存在
                var ourDef = DefDatabase<FaceTypeDef>.GetNamedSilentFail("DD_Maple_Sugar_Eyeball");
                if (ourDef != null)
                {
                    Log.Message($"[DD] Found DD_Maple_Sugar_Eyeball! Race: {ourDef.raceName}");
                }
                else
                {
                    Log.Warning("[DD] DD_Maple_Sugar_Eyeball not found in FaceTypeDef database.");
                    
                    // 尝试在所有Def中查找
                    var allDef = DefDatabase<Def>.AllDefs.FirstOrDefault(d => d.defName == "DD_Maple_Sugar_Eyeball");
                    if (allDef != null)
                    {
                        Log.Message($"[DD] Found in Def database, type: {allDef.GetType().FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] ERROR during patching: {ex}");
            }
        }
    }
    
    /// <summary>
    /// 补丁方法集合
    /// </summary>
    public static class EyeballPatches
    {
        /// <summary>
        /// GatherPawnParam前缀补丁
        /// </summary>
        public static void GatherPawnParam_Prefix(object __instance)
        {
            try
            {
                Log.Message("[DD] GatherPawnParam_Prefix called!");
                
                // 获取Pawn
                Pawn pawn = null;
                var pawnField = __instance.GetType().GetField("pawn", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    
                if (pawnField != null)
                {
                    pawn = pawnField.GetValue(__instance) as Pawn;
                    Log.Message($"[DD] Found pawn via pawn field: {pawn?.Label}");
                }
                
                if (pawn == null)
                {
                    // 尝试通过parent字段获取
                    var parentField = __instance.GetType().GetField("parent", 
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        
                    if (parentField != null)
                    {
                        var parent = parentField.GetValue(__instance) as ThingComp;
                        pawn = parent?.parent as Pawn;
                        Log.Message($"[DD] Found pawn via parent field: {pawn?.Label}");
                    }
                }
                
                if (pawn == null)
                {
                    Log.Message("[DD] Could not get pawn from controller.");
                    return;
                }
                
                // 尝试设置眼球
                TrySetEyeballForPawn(pawn);
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in GatherPawnParam_Prefix: {ex}");
            }
        }
        
        /// <summary>
        /// SpawnSetup后置补丁
        /// </summary>
        public static void SpawnSetup_Postfix(Pawn __instance)
        {
            try
            {
                if (!__instance.Spawned || __instance.Dead)
                    return;
                    
                Log.Message($"[DD] SpawnSetup_Postfix called for {__instance.Label}");
                
                // 延迟执行，确保所有组件已加载
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    TrySetEyeballForPawn(__instance);
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in SpawnSetup_Postfix: {ex}");
            }
        }
        
        /// <summary>
        /// 尝试为Pawn设置眼球类型
        /// </summary>
        private static void TrySetEyeballForPawn(Pawn pawn)
        {
            try
            {
                Log.Message($"[DD] TrySetEyeballForPawn called for {pawn.Label}");
                
                // 检查是否有我们的扩展
                var extension = PawnKindExtension_EyeballOverride.GetExtension(pawn);
                if (extension == null)
                {
                    Log.Message($"[DD] No eyeball extension for {pawn.Label}");
                    return;
                }
                
                Log.Message($"[DD] Found extension for {pawn.Label}");
                
                string eyeballDefName = extension.GetRandomEyeballTypeDefName();
                if (string.IsNullOrEmpty(eyeballDefName))
                {
                    Log.Message($"[DD] No eyeball def name specified");
                    return;
                }
                
                Log.Message($"[DD] Target eyeball def: {eyeballDefName}");
                
                // 查找眼球类型Def
                var eyeballDef = FindEyeballTypeDef(eyeballDefName);
                if (eyeballDef == null)
                {
                    Log.Warning($"[DD] Eyeball def not found: {eyeballDefName}");
                    return;
                }
                
                Log.Message($"[DD] Found eyeball def: {eyeballDef.defName}");
                
                // 查找眼球控制器
                var eyeballController = FindEyeballController(pawn);
                if (eyeballController == null)
                {
                    Log.Warning($"[DD] No eyeball controller found for {pawn.Label}");
                    return;
                }
                
                Log.Message($"[DD] Found eyeball controller");
                
                // 设置faceType字段
                var faceTypeField = GetFaceTypeField(eyeballController);
                if (faceTypeField == null)
                {
                    Log.Warning($"[DD] Could not find faceType field");
                    return;
                }
                
                Log.Message($"[DD] Setting faceType to {eyeballDef.defName}");
                faceTypeField.SetValue(eyeballController, eyeballDef);
                Log.Message($"[DD] Eyeball set successfully for {pawn.Label}");
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in TrySetEyeballForPawn: {ex}");
            }
        }
        
        /// <summary>
        /// 查找眼球类型Def
        /// </summary>
        private static FaceTypeDef FindEyeballTypeDef(string defName)
        {
            try
            {
                // 方法1：直接查找
                var def = DefDatabase<FaceTypeDef>.GetNamedSilentFail(defName);
                if (def != null)
                    return def;
                
                // 方法2：在所有Def中查找FaceTypeDef子类
                foreach (var d in DefDatabase<Def>.AllDefs)
                {
                    if (d.defName == defName && d is FaceTypeDef faceTypeDef)
                    {
                        return faceTypeDef;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in FindEyeballTypeDef: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 查找眼球控制器
        /// </summary>
        private static object FindEyeballController(Pawn pawn)
        {
            try
            {
                Log.Message($"[DD] Looking for eyeball controller in {pawn.Label}'s components...");
                
                foreach (var comp in pawn.AllComps)
                {
                    var compType = comp.GetType();
                    Log.Message($"[DD] Checking component type: {compType.FullName}");
                    
                    // 检查是否是眼球控制器
                    if (IsEyeballController(comp))
                    {
                        Log.Message($"[DD] Found eyeball controller: {compType.Name}");
                        return comp;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in FindEyeballController: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 判断是否是眼球控制器
        /// </summary>
        private static bool IsEyeballController(object comp)
        {
            var type = comp.GetType();
            
            // 检查类型名
            if (type.FullName == "FacialAnimation.EyeballControllerComp")
                return true;
            
            // 检查基类
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType)
                {
                    var genericTypeDef = baseType.GetGenericTypeDefinition();
                    if (genericTypeDef.FullName == "FacialAnimation.ControllerBaseComp`2")
                    {
                        // 检查泛型参数
                        var genericArgs = baseType.GetGenericArguments();
                        if (genericArgs.Length >= 1)
                        {
                            var faceTypeArg = genericArgs[0];
                            if (faceTypeArg.FullName == "FacialAnimation.EyeballTypeDef")
                                return true;
                        }
                    }
                }
                baseType = baseType.BaseType;
            }
            
            return false;
        }
        
        /// <summary>
        /// 获取faceType字段
        /// </summary>
        private static FieldInfo GetFaceTypeField(object controller)
        {
            var type = controller.GetType();
            
            // 在类和基类中查找
            while (type != null)
            {
                var field = type.GetField("faceType", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                    return field;
                    
                type = type.BaseType;
            }
            
            return null;
        }
    }
}
