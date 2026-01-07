using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 受管理战机组件的属性
    /// </summary>
    public class CompProperties_FlyoverManaged : CompProperties
    {
        // 基础配置
        public bool autoRegister = true;

        // 新增：UI图标配置
        public string iconPath;  // UI显示的图标路径

        // 新增：数据销毁配置
        public bool destroyDataWithFlyover = true;  // 是否在flyover销毁时同步销毁flyoverdata

        public CompProperties_FlyoverManaged()
        {
            compClass = typeof(CompFlyoverManaged);
        }
    }
}
