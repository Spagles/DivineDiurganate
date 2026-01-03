// File: FixedIdentityExtension.cs
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    public class FixedIdentityExtension : DefModExtension
    {
        // 头部类型设置
        public string forcedHeadTypeDef;
        
        // 眼睛颜色设置
        public Color? eyeColor = null; // 单眼颜色（如果双眼相同）
        public Color? leftEyeColor = null; // 左眼颜色
        public Color? rightEyeColor = null; // 右眼颜色
        public string eyeTypeDef; // 眼睛类型定义（可选）
    }
}
