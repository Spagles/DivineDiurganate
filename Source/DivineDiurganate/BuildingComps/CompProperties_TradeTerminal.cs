using Verse;

namespace DivineDiurganate
{
    public class CompProperties_TradeTerminal : CompProperties
    {
        public float maxTradeRange = 0f;
        public bool requiresPower = true;
        public string gizmoIconPath = "UI/Commands/Trade";
        public string gizmoLabel = "Trade with Flyover";
        public string gizmoDesc = "Select a flyover trader to initiate trade.";

        public CompProperties_TradeTerminal()
        {
            compClass = typeof(CompTradeTerminal);
        }
    }
}
