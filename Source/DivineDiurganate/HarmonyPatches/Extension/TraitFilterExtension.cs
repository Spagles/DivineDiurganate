using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 特性过滤器扩展
    /// 用于在生成Pawn时删除非强制特性
    /// </summary>
    public class TraitFilterExtension : DefModExtension
    {
        // 不需要额外字段，扩展本身就是一个标记
        // 可以添加一些可选配置参数
        public bool keepBackstoryTraits = true;
        public bool keepPawnKindTraits = true;
        public bool allowRandomTraits = false; // 是否允许随机特性
        public List<TraitDef> exceptions = new List<TraitDef>(); // 例外特性列表
    }
}
