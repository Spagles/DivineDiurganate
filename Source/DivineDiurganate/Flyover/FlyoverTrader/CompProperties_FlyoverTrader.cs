using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompProperties_FlyoverTrader : CompProperties
    {
        public TraderKindDef traderKindDef;
        public int tradeDurationTicks = 30000;
        public bool canTradeWhileMoving = true;
        public float minProgressToTrade = 0.2f;
        public float maxProgressToTrade = 0.8f;

        public CompProperties_FlyoverTrader()
        {
            compClass = typeof(CompFlyoverTrader);
        }
    }
}
