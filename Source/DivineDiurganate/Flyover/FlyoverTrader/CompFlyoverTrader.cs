using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompFlyoverTrader : ThingComp, ITrader, IThingHolder
    {
        public CompProperties_FlyoverTrader Props => (CompProperties_FlyoverTrader)props;

        private ThingOwner<Thing> things;
        private List<Pawn> soldPrisoners = new List<Pawn>();
        private int randomPriceFactorSeed = -1;
        private bool goodsGenerated;

        public TraderKindDef TraderKind => Props.traderKindDef;
        public IEnumerable<Thing> Goods
        {
            get
            {
                if (!goodsGenerated)
                {
                    GenerateGoods();
                }

                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn pawn && soldPrisoners.Contains(pawn))
                    {
                        continue;
                    }

                    yield return things[i];
                }
            }
        }

        public int RandomPriceFactorSeed => randomPriceFactorSeed;
        public string TraderName => parent.LabelCap;
        public bool CanTradeNow => CanCurrentlyTrade();
        public float TradePriceImprovementOffsetForPlayer => 0f;
        public Faction Faction => (parent as FlyOver)?.faction ?? parent.Faction;
        public TradeCurrency TradeCurrency => TraderKind?.tradeCurrency ?? TradeCurrency.Silver;

        public CompFlyoverTrader()
        {
            things = new ThingOwner<Thing>(this);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                randomPriceFactorSeed = Rand.RangeInclusive(1, 10000000);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref things, "things", this);
            Scribe_Collections.Look(ref soldPrisoners, "soldPrisoners", LookMode.Reference);
            Scribe_Values.Look(ref randomPriceFactorSeed, "randomPriceFactorSeed", -1);
            Scribe_Values.Look(ref goodsGenerated, "goodsGenerated", false);
            if (things == null)
            {
                things = new ThingOwner<Thing>(this);
            }

            if (soldPrisoners == null)
            {
                soldPrisoners = new List<Pawn>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                soldPrisoners.RemoveAll(pawn => pawn == null);
            }
        }

        private bool CanCurrentlyTrade()
        {
            if (Props.traderKindDef == null)
            {
                return false;
            }

            if (parent is FlyOver flyOver)
            {
                if (!Props.canTradeWhileMoving && flyOver.hasStarted && !flyOver.hasCompleted)
                {
                    return false;
                }

                float progress = flyOver.currentProgress;
                return progress >= Props.minProgressToTrade && progress <= Props.maxProgressToTrade;
            }

            return true;
        }

        public void GenerateGoods()
        {
            if (goodsGenerated || Props.traderKindDef == null)
            {
                return;
            }

            ThingSetMakerParams parms = default;
            parms.traderDef = Props.traderKindDef;
            parms.tile = parent.Map?.Tile ?? -1;
            foreach (Thing thing in ThingSetMakerDefOf.TraderStock.root.Generate(parms))
            {
                if (thing is Pawn pawn && pawn.Dead)
                {
                    continue;
                }

                things.TryAdd(thing, false);
            }

            goodsGenerated = true;
        }

        public int CountHeldOf(ThingDef thingDef, ThingDef stuffDef = null)
        {
            return things.TotalStackCountOfDef(thingDef);
        }

        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
        {
            foreach (Thing item in TradeUtility.AllLaunchableThingsForTrade(parent.Map, this))
            {
                yield return item;
            }

            foreach (Pawn pawn in TradeUtility.AllSellableColonyPawns(parent.Map, false))
            {
                yield return pawn;
            }
        }

        public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            Thing thing = toGive.SplitOff(countToGive);
            thing.PreTraded(TradeAction.PlayerSells, playerNegotiator, this);
            if (thing is Pawn pawn && pawn.RaceProps.Humanlike)
            {
                soldPrisoners.Add(pawn);
            }
            things.TryAdd(thing, false);
        }

        public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            Thing thing = toGive.SplitOff(countToGive);
            thing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, this);
            if (thing is Pawn pawn)
            {
                soldPrisoners.Remove(pawn);
            }
            TradeUtility.SpawnDropPod(DropCellFinder.TradeDropSpot(parent.Map), parent.Map, thing);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return things;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }
    }
}
