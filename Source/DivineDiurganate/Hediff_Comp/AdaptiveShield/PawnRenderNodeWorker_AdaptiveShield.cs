using UnityEngine;
using Verse;
using RimWorld;

namespace DivineDiurganate
{
    /// <summary>
    /// 自适应护盾的渲染节点Worker
    /// 负责控制护盾的显示条件和位置偏移
    /// </summary>
    public class PawnRenderNodeWorker_AdaptiveShield : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
                return false;

            // 获取对应的 HediffComp
            if (node.hediff == null)
                return false;
                
            var shieldComp = node.hediff.TryGetComp<HediffComp_AdaptiveShield>();
            if (shieldComp == null)
                return false;

            // 只有当 Pawn 被征召、或者处于战斗状态、或者强制显示时才显示
            // 这里我们保持一直显示，或者根据护盾状态
            // 可以添加一个属性来控制是否只在征召时显示
            
            return true;
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            
            // 根据朝向调整位置，使护盾位于身体前方
            switch (parms.facing.AsInt)
            {
                case 0: // North - 面向上
                    offset += new Vector3(0f, 0f, 0.2f);
                    break;
                case 1: // East - 面向右
                    offset += new Vector3(0.2f, 0f, 0f);
                    break;
                case 2: // South - 面向下
                    offset += new Vector3(0f, 0f, -0.2f);
                    break;
                case 3: // West - 面向左
                    offset += new Vector3(-0.2f, 0f, 0f);
                    break;
            }
            
            return offset;
        }
        
        public override float LayerFor(PawnRenderNode node, PawnDrawParms parms)
        {
            // 确保图层正确
            // 北向时在身体后面，南向时在身体前面
            // 通过调整 altitude layer
            
            return base.LayerFor(node, parms);
        }
    }
}
