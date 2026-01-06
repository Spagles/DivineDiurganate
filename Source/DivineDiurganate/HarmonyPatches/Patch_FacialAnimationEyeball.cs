using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;
using FacialAnimation;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DivineDiurganate
{
    /// <summary>
    /// 修复版 - 强制设置眼球颜色为白色
    /// 注意：仅在游戏进行中时启动补丁
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EyeballPatchManager
    {
        // 添加一个静态字段来跟踪补丁是否已应用
        private static bool patchesApplied = false;
        
        // 添加一个静态字段来跟踪游戏是否已开始
        public static bool gameStarted = false;
        
        static EyeballPatchManager()
        {
            // 等待游戏初始化完成
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                // 检查是否在游戏中（不在主菜单或选人界面）
                if (!IsInGame())
                {
                    Log.Message("[DD] 游戏未开始，眼球补丁不启动");
                    return;
                }
                
                // 标记游戏已开始
                gameStarted = true;
                
                bool faLoaded = ModLister.GetActiveModWithIdentifier("nals.facialanimation") != null;
                
                if (!faLoaded)
                {
                    Log.Message("[DD] FacialAnimation未加载，眼球补丁不启动");
                    return;
                }
                
                try
                {
                    // 避免重复应用补丁
                    if (patchesApplied)
                    {
                        Log.Message("[DD] 眼球补丁已应用，跳过");
                        return;
                    }
                    
                    var harmony = new Harmony("DivineDiurganate.EyeballPatch");
                    
                    // 方法1：补丁GatherPawnParam方法
                    var gatherMethod = AccessTools.Method("FacialAnimation.FacialAnimationControllerComp:GatherPawnParam");
                    if (gatherMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(EyeballPatches).GetMethod("GatherPawnParam_Prefix"));
                        harmony.Patch(gatherMethod, prefix: prefix);
                        Log.Message("[DD] 已应用GatherPawnParam补丁");
                    }
                    else
                    {
                        Log.Warning("[DD] Could not find GatherPawnParam method.");
                    }
                    
                    // 方法2：补丁Pawn.SpawnSetup作为备用
                    var spawnMethod = typeof(Pawn).GetMethod("SpawnSetup");
                    if (spawnMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(EyeballPatches).GetMethod("SpawnSetup_Postfix"));
                        harmony.Patch(spawnMethod, postfix: postfix);
                        Log.Message("[DD] 已应用SpawnSetup补丁");
                    }
                    
                    // 方法3：补丁ControllerBaseComp的Color属性getter
                    var controllerBaseType = AccessTools.TypeByName("FacialAnimation.ControllerBaseComp`2");
                    if (controllerBaseType != null)
                    {
                        var colorProperty = AccessTools.Property(controllerBaseType, "Color");
                        if (colorProperty != null)
                        {
                            var getter = colorProperty.GetGetMethod();
                            if (getter != null)
                            {
                                var postfix = new HarmonyMethod(typeof(EyeballPatches).GetMethod("ColorGetter_Postfix"));
                                harmony.Patch(getter, postfix: postfix);
                                Log.Message("[DD] 已应用ColorGetter补丁");
                            }
                        }
                    }
                    
                    patchesApplied = true;
                    Log.Message("[DD] 眼球补丁应用完成");
                    
                    // 调试：列出所有FaceTypeDef
                    int count = 0;
                    foreach (var def in DefDatabase<FaceTypeDef>.AllDefs)
                    {
                        count++;
                    }
                    
                    // 检查我们的Def是否存在
                    var ourDef = DefDatabase<FaceTypeDef>.GetNamedSilentFail("DD_Maple_Sugar_Eyeball");
                    if (ourDef != null)
                    {
                        Log.Message($"[DD] 找到自定义眼球定义: {ourDef.defName}");
                    }
                    else
                    {
                        Log.Warning("[DD] DD_Maple_Sugar_Eyeball not found in FaceTypeDef database.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[DD] ERROR during patching: {ex}");
                }
            });
        }
        
        /// <summary>
        /// 检查是否在游戏中（不在主菜单或选人界面）
        /// </summary>
        private static bool IsInGame()
        {
            try
            {
                // 检查游戏是否在播放状态
                if (Current.ProgramState != ProgramState.Playing)
                {
                    return false;
                }
                
                // 检查是否有世界地图
                if (Find.World == null)
                {
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[DD] Error checking game state: {ex.Message}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// 补丁方法集合
    /// </summary>
    public static class EyeballPatches
    {
        // 避免递归调用的标志
        private static bool isInForceWhiteColor = false;
        
        /// <summary>
        /// GatherPawnParam前缀补丁
        /// </summary>
        public static void GatherPawnParam_Prefix(object __instance)
        {
            try
            {
                // 获取Pawn
                Pawn pawn = null;
                var pawnField = __instance.GetType().GetField("pawn", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    
                if (pawnField != null)
                {
                    pawn = pawnField.GetValue(__instance) as Pawn;
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
                    }
                }
                
                if (pawn == null)
                {
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
        /// Color属性getter后置补丁，强制眼球控制器返回白色
        /// </summary>
        public static void ColorGetter_Postfix(object __instance, ref Color __result)
        {
            try
            {
                // 只有在是眼球控制器时才强制返回白色
                if (IsEyeballController(__instance))
                {
                    __result = Color.white;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in ColorGetter_Postfix: {ex}");
            }
        }
        
        /// <summary>
        /// 尝试为Pawn设置眼球类型
        /// </summary>
        private static void TrySetEyeballForPawn(Pawn pawn)
        {
            try
            {
                // 检查是否有我们的扩展
                var extension = PawnKindExtension_EyeballOverride.GetExtension(pawn);
                if (extension == null)
                {
                    return;
                }
                
                string eyeballDefName = extension.GetRandomEyeballTypeDefName();
                if (string.IsNullOrEmpty(eyeballDefName))
                {
                    return;
                }
                
                // 查找眼球类型Def
                var eyeballDef = FindEyeballTypeDef(eyeballDefName);
                if (eyeballDef == null)
                {
                    Log.Warning($"[DD] Eyeball def not found: {eyeballDefName}");
                    return;
                }
                
                // 查找眼球控制器
                var eyeballController = FindEyeballController(pawn);
                if (eyeballController == null)
                {
                    Log.Warning($"[DD] No eyeball controller found for {pawn.Label}");
                    return;
                }
                
                // 设置faceType字段
                var faceTypeField = GetFaceTypeField(eyeballController);
                if (faceTypeField == null)
                {
                    Log.Warning($"[DD] Could not find faceType field");
                    return;
                }
                
                faceTypeField.SetValue(eyeballController, eyeballDef);
                
                // 强制设置颜色为白色
                ForceWhiteColor(eyeballController);
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in TrySetEyeballForPawn: {ex}");
            }
        }
        
        /// <summary>
        /// 强制设置颜色为白色
        /// </summary>
        private static void ForceWhiteColor(object controller)
        {
            try
            {
                if (isInForceWhiteColor)
                    return;
                    
                isInForceWhiteColor = true;
                
                // 1. 设置Color属性为白色
                var colorProperty = controller.GetType().GetProperty("Color",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (colorProperty != null && colorProperty.CanWrite)
                {
                    colorProperty.SetValue(controller, Color.white);
                }
                else
                {
                    // 尝试直接设置color字段
                    var colorField = controller.GetType().GetField("color",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (colorField != null)
                    {
                        colorField.SetValue(controller, Color.white);
                    }
                    else
                    {
                        Log.Warning("[DD] Could not find Color property or color field");
                    }
                }
                
                // 2. 设置OffsetColor为透明
                var offsetColorProperty = controller.GetType().GetProperty("OffsetColor",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (offsetColorProperty != null && offsetColorProperty.CanWrite)
                {
                    offsetColorProperty.SetValue(controller, Color.clear);
                }
                else
                {
                    var offsetColorField = controller.GetType().GetField("offsetColor",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (offsetColorField != null)
                    {
                        offsetColorField.SetValue(controller, Color.clear);
                    }
                }
                
                // 3. 尝试直接修改材质颜色，而不是重新加载纹理
                TryUpdateMaterialColors(controller);
                
                isInForceWhiteColor = false;
            }
            catch (Exception ex)
            {
                Log.Error($"[DD] Error in ForceWhiteColor: {ex}");
                isInForceWhiteColor = false;
            }
        }
        
        /// <summary>
        /// 尝试直接更新材质颜色
        /// </summary>
        private static void TryUpdateMaterialColors(object controller)
        {
            try
            {
                // 获取shapeGraphicList字典
                var shapeGraphicListField = controller.GetType().GetField("shapeGraphicList",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                var shapeRottingGraphicListField = controller.GetType().GetField("shapeRottingGraphicList",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                if (shapeGraphicListField == null || shapeRottingGraphicListField == null)
                {
                    return;
                }
                
                // 获取默认形状
                var defaultFaceShapeField = controller.GetType().GetField("defaultFaceShape",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                
                object defaultShape = null;
                if (defaultFaceShapeField != null)
                {
                    defaultShape = defaultFaceShapeField.GetValue(null);
                }
                
                // 更新shapeGraphicList中的材质颜色
                UpdateGraphicListColors(shapeGraphicListField.GetValue(controller), defaultShape, Color.white);
            }
            catch (Exception ex)
            {
                Log.Warning($"[DD] Could not update material colors: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新图形列表中的材质颜色
        /// </summary>
        private static void UpdateGraphicListColors(object graphicList, object defaultShape, Color targetColor)
        {
            try
            {
                if (graphicList is IDictionary dictionary)
                {
                    // 如果字典为空，跳过
                    if (dictionary.Count == 0)
                    {
                        return;
                    }
                    
                    // 尝试获取默认形状的图形
                    if (defaultShape != null && dictionary.Contains(defaultShape))
                    {
                        UpdateGraphicColors(dictionary[defaultShape], targetColor);
                    }
                    else
                    {
                        // 否则更新所有图形
                        foreach (var key in dictionary.Keys)
                        {
                            try
                            {
                                UpdateGraphicColors(dictionary[key], targetColor);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[DD] Failed to update graphic for key {key}: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    Log.Warning("[DD] Graphic list is not a dictionary");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DD] Error in UpdateGraphicListColors: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新单个图形的材质颜色
        /// </summary>
        private static void UpdateGraphicColors(object graphic, Color color)
        {
            try
            {
                var graphicType = graphic.GetType();
                
                // 检查是否是Graphic_SepLR类型
                if (graphicType.Name == "Graphic_SepLR")
                {
                    // 尝试获取左右材质
                    var matLeftProperty = graphicType.GetProperty("MatLeft", 
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var matRightProperty = graphicType.GetProperty("MatRight", 
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    
                    // 方法1：通过属性设置
                    if (matLeftProperty != null)
                    {
                        var matLeft = matLeftProperty.GetValue(graphic) as Material;
                        if (matLeft != null)
                        {
                            matLeft.color = color;
                        }
                    }
                    
                    if (matRightProperty != null)
                    {
                        var matRight = matRightProperty.GetValue(graphic) as Material;
                        if (matRight != null)
                        {
                            matRight.color = color;
                        }
                    }
                    
                    // 方法2：尝试通过字段设置
                    var matLeftField = graphicType.GetField("matLeft",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var matRightField = graphicType.GetField("matRight",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    
                    if (matLeftField != null)
                    {
                        var matLeft = matLeftField.GetValue(graphic) as Material;
                        if (matLeft != null)
                        {
                            matLeft.color = color;
                        }
                    }
                    
                    if (matRightField != null)
                    {
                        var matRight = matRightField.GetValue(graphic) as Material;
                        if (matRight != null)
                        {
                            matRight.color = color;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[DD] Could not update graphic colors: {ex.Message}");
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
                foreach (var comp in pawn.AllComps)
                {
                    var compType = comp.GetType();
                    
                    // 检查是否是眼球控制器
                    if (IsEyeballController(comp))
                    {
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
