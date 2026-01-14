using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace DivineDiurganate
{
    /// <summary>
    /// 盾牌渲染处理器
    /// </summary>
    public static class DD_Handle_Shield
    {
        /// <summary>
        /// 四向渲染数据类
        /// </summary>
        public class ShieldRenderData
        {
            // 举起状态四个方向的绘制偏移
            public Vector3 DraftedNorthOffset = new Vector3(-0.2f, -0.2f, -0.09f);
            public Vector3 DraftedSouthOffset = new Vector3(0.2f, 0.2f, -0.15f);
            public Vector3 DraftedEastOffset = new Vector3(0.2f, -0.2f, -0.2f);
            public Vector3 DraftedWestOffset = new Vector3(-0.2f, 0.2f, -0.15f);

            // 背起状态四个方向的绘制偏移
            public Vector3 BackNorthOffset = new Vector3(0f, 0.2f, -0.2f);
            public Vector3 BackSouthOffset = new Vector3(0f, -0.2f, -0.09f);
            public Vector3 BackEastOffset = new Vector3(-0.15f, 0.05f, -0.07f);
            public Vector3 BackWestOffset = new Vector3(0.15f, -2f, -0.07f);

            // 背起状态旋转角度
            public float BackEastAngle = 15f;
            public float BackWestAngle = -15f;

            // 图形路径
            public string GraphicPath = "Apparel/";

            // 是否使用材质颜色
            public bool UseStuffColor = true;

            // 默认材质颜色（当不使用材质颜色时）
            public Color DefaultColor = Color.white;
            
            // 着色器
            public Shader Shader = ShaderDatabase.Cutout;

            // 绘制尺寸
            public Vector2 DrawSize = new Vector2(1, 1);

            // 图形缓存
            private Graphic graphicCache;

            /// <summary>
            /// 获取盾牌图形
            /// </summary>
            public Graphic GetGraphic(ThingDef shieldDef, ThingDef stuffDef = null)
            {
                if (graphicCache != null)
                    return graphicCache;

                string graphicPath = GraphicPath + shieldDef.defName;
                Color graphicColor = UseStuffColor && stuffDef != null ? 
                    stuffDef.stuffProps.color : DefaultColor;

                graphicCache = GraphicDatabase.Get<Graphic_Multi>(
                    graphicPath, 
                    Shader, 
                    DrawSize, 
                    graphicColor
                );

                return graphicCache;
            }

            /// <summary>
            /// 清理图形缓存
            /// </summary>
            public void ClearGraphicCache()
            {
                graphicCache = null;
            }

            /// <summary>
            /// 根据角色朝向获取举起状态的偏移
            /// </summary>
            public Vector3 GetDraftedOffsetForRotation(Rot4 rotation)
            {
                switch (rotation.AsInt)
                {
                    case 0: return DraftedNorthOffset;  // 北
                    case 1: return DraftedEastOffset;   // 东
                    case 2: return DraftedSouthOffset;  // 南
                    case 3: return DraftedWestOffset;   // 西
                    default: return Vector3.zero;
                }
            }

            /// <summary>
            /// 根据角色朝向获取背起状态的偏移和角度
            /// </summary>
            public (Vector3 offset, float angle) GetBackOffsetForRotation(Rot4 rotation)
            {
                switch (rotation.AsInt)
                {
                    case 0: return (BackNorthOffset, 0f);   // 北
                    case 1: return (BackEastOffset, BackEastAngle);   // 东
                    case 2: return (BackSouthOffset, 0f);  // 南
                    case 3: return (BackWestOffset, BackWestAngle);   // 西
                    default: return (Vector3.zero, 0f);
                }
            }
        }

        /// <summary>
        /// 盾牌数据组件
        /// </summary>
        public class CompShieldData : ThingComp
        {
            // 渲染数据
            private ShieldRenderData renderData;
            
            // 图形缓存
            private Graphic graphicCache;

            /// <summary>
            /// 渲染数据属性
            /// </summary>
            public ShieldRenderData RenderData
            {
                get
                {
                    if (renderData == null)
                        InitializeRenderData();
                    return renderData;
                }
                set { renderData = value; }
            }

            /// <summary>
            /// 初始化渲染数据
            /// </summary>
            private void InitializeRenderData()
            {
                renderData = new ShieldRenderData();
                
                // 从盾牌属性中加载配置（如果有）
                var props = parent.def.GetModExtension<ShieldProperties>();
                if (props != null)
                {
                    if (props.draftedOffsets != null && props.draftedOffsets.Length == 4)
                    {
                        renderData.DraftedNorthOffset = props.draftedOffsets[0];
                        renderData.DraftedEastOffset = props.draftedOffsets[1];
                        renderData.DraftedSouthOffset = props.draftedOffsets[2];
                        renderData.DraftedWestOffset = props.draftedOffsets[3];
                    }

                    if (props.backOffsets != null && props.backOffsets.Length == 4)
                    {
                        renderData.BackNorthOffset = props.backOffsets[0];
                        renderData.BackEastOffset = props.backOffsets[1];
                        renderData.BackSouthOffset = props.backOffsets[2];
                        renderData.BackWestOffset = props.backOffsets[3];
                    }

                    if (!string.IsNullOrEmpty(props.graphicPath))
                        renderData.GraphicPath = props.graphicPath;

                    renderData.UseStuffColor = props.useStuffColor;
                    renderData.DefaultColor = props.defaultColor;
                    renderData.DrawSize = props.drawSize;
                    
                    // 设置着色器
                    if (!string.IsNullOrEmpty(props.shaderType))
                    {
                        renderData.Shader = GetShaderFromString(props.shaderType);
                    }
                }
            }

            /// <summary>
            /// 获取盾牌图形
            /// </summary>
            public Graphic GetShieldGraphic()
            {
                if (graphicCache == null)
                {
                    graphicCache = RenderData.GetGraphic(parent.def, parent.Stuff);
                }
                return graphicCache;
            }

            /// <summary>
            /// 根据字符串获取着色器
            /// </summary>
            private Shader GetShaderFromString(string shaderType)
            {
                switch (shaderType.ToLower())
                {
                    case "cutoutcomplex": return ShaderDatabase.CutoutComplex;
                    case "transparent": return ShaderDatabase.Transparent;
                    case "transparentpostlight": return ShaderDatabase.TransparentPostLight;
                    case "mote": return ShaderDatabase.Mote;
                    default: return ShaderDatabase.Cutout;
                }
            }

            /// <summary>
            /// 清理图形缓存
            /// </summary>
            public void ClearGraphicCache()
            {
                graphicCache = null;
                RenderData.ClearGraphicCache();
            }

            /// <summary>
            /// 保存数据
            /// </summary>
            public override void PostExposeData()
            {
                base.PostExposeData();
                
                // 如果渲染数据有变化，需要重新初始化
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    renderData = null;
                    graphicCache = null;
                }
            }
        }

        /// <summary>
        /// 盾牌属性扩展
        /// </summary>
        public class ShieldProperties : DefModExtension
        {
            // 举起状态四个方向的偏移
            public Vector3[] draftedOffsets;

            // 背起状态四个方向的偏移
            public Vector3[] backOffsets;

            // 背起状态旋转角度
            public float backEastAngle = 15f;
            public float backWestAngle = -15f;

            // 图形路径
            public string graphicPath = "Apparel/";

            // 是否使用材质颜色
            public bool useStuffColor = true;

            // 默认材质颜色
            public Color defaultColor = Color.white;

            // 着色器类型
            public string shaderType = "cutout";

            // 绘制尺寸
            public Vector2 drawSize = new Vector2(1, 1);
        }

        /// <summary>
        /// 绘制盾牌
        /// </summary>
        public static void DrawShield(Apparel shield, Pawn pawn, bool isDrafted)
        {
            if (shield == null || pawn == null || pawn.Dead || pawn.Downed)
                return;

            var comp = shield.TryGetComp<CompShieldData>();
            if (comp == null)
                return;

            Vector3 rootLoc = pawn.DrawPos;
            Rot4 rotation = pawn.Rotation;
            Graphic shieldGraphic = comp.GetShieldGraphic();

            if (shieldGraphic == null)
                return;

            if (isDrafted)
            {
                // 举起状态
                Vector3 offset = comp.RenderData.GetDraftedOffsetForRotation(rotation);
                Material material = GetMaterialForRotation(shieldGraphic, rotation);
                DrawShieldMesh(material, rootLoc + offset, 0f);
            }
            else if (pawn.GetPosture() == PawnPosture.Standing)
            {
                // 背起状态
                var (offset, angle) = comp.RenderData.GetBackOffsetForRotation(rotation);
                Material material = GetBackMaterialForRotation(shieldGraphic, rotation);
                DrawShieldMesh(material, rootLoc + offset, angle);
            }
        }

        /// <summary>
        /// 获取举起状态的材质
        /// </summary>
        private static Material GetMaterialForRotation(Graphic graphic, Rot4 rotation)
        {
            if (graphic is Graphic_Multi multiGraphic)
            {
                switch (rotation.AsInt)
                {
                    case 0: return multiGraphic.MatNorth;  // 北
                    case 1: return multiGraphic.MatEast;   // 东
                    case 2: return multiGraphic.MatSouth;  // 南
                    case 3: return multiGraphic.MatWest;   // 西
                }
            }
            return graphic.MatSingle;
        }

        /// <summary>
        /// 获取背起状态的材质（与举起状态使用不同方向的材质）
        /// </summary>
        private static Material GetBackMaterialForRotation(Graphic graphic, Rot4 rotation)
        {
            if (graphic is Graphic_Multi multiGraphic)
            {
                // 背起状态使用相反方向的材质
                switch (rotation.AsInt)
                {
                    case 0: return multiGraphic.MatSouth;  // 北 -> 使用南
                    case 1: return multiGraphic.MatWest;   // 东 -> 使用西
                    case 2: return multiGraphic.MatNorth;  // 南 -> 使用北
                    case 3: return multiGraphic.MatEast;   // 西 -> 使用东
                }
            }
            return graphic.MatSingle;
        }

        /// <summary>
        /// 绘制盾牌网格
        /// </summary>
        private static void DrawShieldMesh(Material mat, Vector3 drawLoc, float angle)
        {
            if (mat == null)
                return;

            Mesh mesh = MeshPool.plane10;
            Quaternion rotation = angle != 0f ? 
                Quaternion.AngleAxis(angle, Vector3.up) : 
                Quaternion.identity;
            
            Graphics.DrawMesh(mesh, drawLoc, rotation, mat, 0);
        }

        /// <summary>
        /// 判断盾牌是否应该举起
        /// </summary>
        public static bool ShouldShieldUp(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned)
                return false;

            return pawn.InAggroMentalState || 
                   pawn.Drafted || 
                   (pawn.CurJob != null && pawn.CurJob.def.alwaysShowWeapon) || 
                   (pawn.mindState.duty != null && pawn.mindState.duty.def.alwaysShowWeapon);
        }

        /// <summary>
        /// 获取盾牌组件
        /// </summary>
        public static CompShieldData GetShieldComp(Thing shield)
        {
            return shield?.TryGetComp<CompShieldData>();
        }

        /// <summary>
        /// 重新加载盾牌图形
        /// </summary>
        public static void ReloadShieldGraphic(Thing shield)
        {
            var comp = GetShieldComp(shield);
            comp?.ClearGraphicCache();
        }
    }

    /// <summary>
    /// 盾牌ThingComp属性
    /// </summary>
    public class CompProperties_ShieldData : CompProperties
    {
        public CompProperties_ShieldData()
        {
            this.compClass = typeof(DD_Handle_Shield.CompShieldData);
        }
    }
}
