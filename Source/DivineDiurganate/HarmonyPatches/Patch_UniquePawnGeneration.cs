using HarmonyLib;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    [HarmonyPatch(typeof(PawnGenerator))]
    [HarmonyPatch("GeneratePawn", typeof(PawnGenerationRequest))]
    public static class Patch_UniquePawnGeneration
    {
        [HarmonyPrefix]
        public static bool Prefix(PawnGenerationRequest request, ref Pawn __result)
        {
            // 检查PawnKindDef是否有UniquePawnExtension
            var extension = request.KindDef?.GetModExtension<UniquePawnExtension>();
            if (extension == null || !extension.isSingleton)
                return true; // 继续正常生成
            
            // 检查是否已存在相同的特殊Pawn
            var manager = UniquePawnManager.Instance;
            if (manager == null)
                return true; // 管理器未初始化，继续正常生成
            
            // 根据范围检查是否已存在
            bool alreadyExists = false;
            Pawn existingPawn = null;
            
            switch (extension.singletonScope)
            {
                case UniquePawnScope.Map:
                    // 检查当前请求的地图
                    if (request.Context == PawnGenerationContext.NonPlayer && request.Tile >= 0)
                    {
                        var map = Find.World.worldObjects.FindMap(request.Tile);
                        if (map != null)
                        {
                            alreadyExists = manager.CheckPawnExistsInMap(request.KindDef, map);
                        }
                    }
                    break;
                    
                case UniquePawnScope.World:
                    alreadyExists = manager.CheckPawnExistsInWorld(request.KindDef);
                    break;
            }
            
            if (alreadyExists)
            {
                // 阻止生成或处理重复
                if (extension.destroyDuplicates)
                {
                    // 允许生成但稍后会销毁
                    return true;
                }
                else
                {
                    // 完全阻止生成
                    __result = null;
                    manager.LogMessage($"阻止生成重复的特殊Pawn: {request.KindDef.label}", 
                        extension.logLevel);
                    return false;
                }
            }
            
            return true;
        }
        
        [HarmonyPostfix]
        public static void Postfix(ref Pawn __result, PawnGenerationRequest request)
        {
            Pawn pawn = __result;
            if (pawn == null)
                return;
            
            // 检查PawnKindDef是否有UniquePawnExtension
            var extension = request.KindDef?.GetModExtension<UniquePawnExtension>();
            if (extension == null || !extension.isSingleton)
                return;
            
            // 获取管理器
            var manager = UniquePawnManager.Instance;
            if (manager == null)
                return;
            
            // 检查是否已存在相同的特殊Pawn
            if (extension.isSingleton)
            {
                bool shouldRegister = true;
                
                switch (extension.singletonScope)
                {
                    case UniquePawnScope.Map:
                        if (pawn.Map != null)
                        {
                            shouldRegister = !manager.CheckPawnExistsInMap(pawn.kindDef, pawn.Map);
                        }
                        break;
                        
                    case UniquePawnScope.World:
                        shouldRegister = !manager.CheckPawnExistsInWorld(pawn.kindDef);
                        break;
                        
                    case UniquePawnScope.Archive:
                        shouldRegister = !manager.CheckPawnExistsInArchive(pawn.kindDef);
                        break;
                }
                
                if (!shouldRegister)
                {
                    // 销毁重复的Pawn
                    if (extension.destroyDuplicates)
                    {
                        pawn.Destroy();
                        __result = null;
                        manager.LogMessage($"销毁重复的特殊Pawn: {pawn.LabelCap}", 
                            extension.logLevel);
                    }
                    return;
                }
            }
            
            // 注册特殊Pawn
            manager.RegisterUniquePawn(pawn, extension);
        }
    }
    
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("Kill")]
    public static class Patch_PawnKill
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            // 检查是否为特殊Pawn
            var manager = UniquePawnManager.Instance;
            if (manager == null)
                return;
            
            var extension = __instance.kindDef?.GetModExtension<UniquePawnExtension>();
            if (extension == null || !extension.isSingleton)
                return;
            
            // 根据设置决定是否移除记录
            if (extension.removeOnDeath)
            {
                manager.RemovePawnRecord(__instance);
            }
        }
    }
    
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("Destroy")]
    public static class Patch_PawnDestroy
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance)
        {
            // 检查是否为特殊Pawn
            var manager = UniquePawnManager.Instance;
            if (manager == null)
                return;
            
            var extension = __instance.kindDef?.GetModExtension<UniquePawnExtension>();
            if (extension == null || !extension.isSingleton)
                return;
            
            // 总是移除记录（因为Pawn被销毁了）
            manager.RemovePawnRecord(__instance);
        }
    }
    
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("FinalizeInit")]
    public static class Patch_GameInit
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // 确保管理器已初始化
            if (UniquePawnManager.Instance == null)
            {
                Log.Error("[UniquePawnManager] 管理器未初始化！");
            }
        }
    }
}
