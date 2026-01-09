using Verse;

namespace DivineDiurganate
{
    public class CompProperties_TradeTerminal : CompProperties
    {
        public float maxTradeRange = 0f;
        public bool requiresPower = true;
        public string gizmoIconPath = "UI/Commands/Trade";
        public string gizmoLabel = "DD_GizmoTradeWithFlyoverLabel";
        public string gizmoDesc = "DD_GizmoTradeWithFlyoverDesc";

        public CompProperties_TradeTerminal()
        {
            compClass = typeof(CompTradeTerminal);
        }
    }
}
