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
                defaultLabel = Props.gizmoLabel.Translate(),
                defaultDesc = Props.gizmoDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get(Props.gizmoIconPath, false) ?? TexCommand.OpenLinkedQuestTex,
                action = OpenFlyoverSelectionUI
            };

            CompPowerTrader powerComp = parent.GetComp<CompPowerTrader>();
            if (Props.requiresPower && (powerComp == null || !powerComp.PowerOn))
            {
                tradeGizmo.Disable("NoPower".Translate());
            }
            else if (!GetAvailableTradeFlyovers().Any())
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

            foreach (FlyOver flyOver in parent.Map.listerThings.GetThingsOfType<FlyOver>())
            {
                if (!flyOver.Destroyed)
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

            Pawn negotiator = FindBestNegotiator();
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

        private Pawn FindBestNegotiator()
        {
            if (parent.Map == null)
            {
                return null;
            }

            Pawn bestPawn = null;
            float bestScore = float.MinValue;
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                {
                    continue;
                }

                float score = pawn.GetStatValue(StatDefOf.TradePriceImprovement);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPawn = pawn;
                }
            }

            return bestPawn;
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
