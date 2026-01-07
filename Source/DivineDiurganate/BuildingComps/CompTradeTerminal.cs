using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    public class CompTradeTerminal : ThingComp
    {
        public CompProperties_TradeTerminal Props => (CompProperties_TradeTerminal)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Command_Action tradeGizmo = new Command_Action
            {
                defaultLabel = Props.gizmoLabel,
                defaultDesc = Props.gizmoDesc,
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false) ?? TexCommand.Trade,
                action = OpenFlyoverSelectionUI
            };

            if (Props.requiresPower)
            {
                CompPowerTrader powerComp = parent.GetComp<CompPowerTrader>();
                if (powerComp != null && !powerComp.PowerOn)
                {
                    tradeGizmo.Disable("NoPower".Translate());
                }
            }

            if (!GetAvailableTradeFlyovers().Any())
            {
                tradeGizmo.Disable("DD_NoTradeFlyoversAvailable".Translate());
            }

            yield return tradeGizmo;
        }

        private List<FlyOver> GetAvailableTradeFlyovers()
        {
            List<FlyOver> result = new List<FlyOver>();
            if (parent.Map == null)
            {
                return result;
            }

            foreach (Thing thing in parent.Map.listerThings.AllThings)
            {
                if (thing is FlyOver flyOver && !flyOver.Destroyed)
                {
                    CompFlyoverTrader traderComp = flyOver.GetComp<CompFlyoverTrader>();
                    if (traderComp == null || !traderComp.CanTradeNow)
                    {
                        continue;
                    }

                    if (Props.maxTradeRange > 0f)
                    {
                        float dist = parent.Position.DistanceTo(flyOver.Position);
                        if (dist > Props.maxTradeRange)
                        {
                            continue;
                        }
                    }

                    result.Add(flyOver);
                }
            }

            return result;
        }

        private void OpenFlyoverSelectionUI()
        {
            List<FlyOver> flyovers = GetAvailableTradeFlyovers();
            if (!flyovers.Any())
            {
                Messages.Message("DD_NoTradeFlyoversAvailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Pawn negotiator = TradeUtility.FindBestNegotiator(parent.Map);
            if (negotiator == null)
            {
                Messages.Message("DD_NoNegotiatorAvailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new Window_SelectTradeFlyover(
                flyovers,
                selectedFlyover => StartTrade(selectedFlyover, negotiator)));
        }

        private void StartTrade(FlyOver flyover, Pawn negotiator)
        {
            CompFlyoverTrader traderComp = flyover.GetComp<CompFlyoverTrader>();
            if (traderComp == null || !traderComp.CanTradeNow)
            {
                Messages.Message("DD_FlyoverNoLongerAvailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Find.WindowStack.Add(new Dialog_Trade(negotiator, traderComp));
        }

        public override string CompInspectStringExtra()
        {
            List<FlyOver> flyovers = GetAvailableTradeFlyovers();
            if (flyovers.Any())
            {
                return "DD_TradeFlyoversAvailable".Translate(flyovers.Count);
            }

            return null;
        }
    }
}
