using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 简化的眼球替换扩展 - 直接在PawnKindDef中指定眼球类型
    /// </summary>
    public class PawnKindExtension_EyeballOverride : DefModExtension
    {
        // 眼球类型Def的名称（支持多个，随机选择）
        public List<string> eyeballTypeDefNames;
        
        // 是否强制覆盖（如果为true，会覆盖其他设置）
        public bool forceOverride = false;
        
        // 获取随机眼球类型Def名称
        public string GetRandomEyeballTypeDefName()
        {
            if (eyeballTypeDefNames == null || eyeballTypeDefNames.Count == 0)
                return null;
                
            return eyeballTypeDefNames.RandomElement();
        }
        
        // Pawn是否有此扩展
        public static bool HasExtension(Pawn pawn)
        {
            return pawn?.kindDef?.GetModExtension<PawnKindExtension_EyeballOverride>() != null;
        }
        
        // 获取Pawn的眼球替换设置
        public static PawnKindExtension_EyeballOverride GetExtension(Pawn pawn)
        {
            return pawn?.kindDef?.GetModExtension<PawnKindExtension_EyeballOverride>();
        }
    }
}
