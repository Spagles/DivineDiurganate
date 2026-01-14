using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompGameConditionTeleporter : ThingComp
    {
        public CompProperties_GameConditionTeleporter Props => (CompProperties_GameConditionTeleporter)props;
        
        private Dictionary<Map, GameCondition> causedConditions = new Dictionary<Map, GameCondition>();
        private static List<Map> tmpDeadConditionMaps = new List<Map>();
        
        public GameConditionDef ConditionDef => Props.conditionDef;
        public int WorldRange => Props.worldRange;
        public bool HideSource => Props.hideSource;
        public bool PreventConditionStacking => Props.preventConditionStacking;
        
        public bool Active => parent.GetComp<CompMapTeleporter>()?.IsWarmingUp ?? false;
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                causedConditions.RemoveAll((KeyValuePair<Map, GameCondition> x) => !Find.Maps.Contains(x.Key));
            }
            
            Scribe_Collections.Look(ref causedConditions, "causedConditions", LookMode.Reference, LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                causedConditions.RemoveAll((KeyValuePair<Map, GameCondition> x) => x.Value == null);
                foreach (KeyValuePair<Map, GameCondition> causedCondition in causedConditions)
                {
                    causedCondition.Value.conditionCauser = parent;
                    causedCondition.Value.hideSource = Props.hideSource;
                }
            }
        }
        
        protected GameCondition GetConditionInstance(Map map)
        {
            if (!causedConditions.TryGetValue(map, out var value) && Props.preventConditionStacking)
            {
                value = map.GameConditionManager.GetActiveCondition(Props.conditionDef);
                if (value != null)
                {
                    causedConditions.Add(map, value);
                    SetupCondition(value, map);
                }
            }
            return value;
        }
        
        public override void CompTick()
        {
            base.CompTick();
            
            if (Active)
            {
                // 激活时强制执行游戏条件
                foreach (Map map in Find.Maps)
                {
                    if (InAoE(map.Tile))
                    {
                        EnforceConditionOn(map);
                    }
                }
            }
            
            // 清理过期的条件
            tmpDeadConditionMaps.Clear();
            foreach (KeyValuePair<Map, GameCondition> causedCondition in causedConditions)
            {
                if (causedCondition.Value.Expired || !causedCondition.Key.GameConditionManager.ConditionIsActive(causedCondition.Value.def))
                {
                    tmpDeadConditionMaps.Add(causedCondition.Key);
                }
            }
            
            foreach (Map tmpDeadConditionMap in tmpDeadConditionMaps)
            {
                causedConditions.Remove(tmpDeadConditionMap);
            }
        }
        
        private bool InAoE(int tile)
        {
            if (!Active) return false;
            
            int myTile = parent.Tile;
            if (myTile < 0) return false;
            
            if (tile == myTile) return true;
            
            if (Props.worldRange <= 0) return false;
            
            return Find.WorldGrid.ApproxDistanceInTiles(tile, myTile) < (float)Props.worldRange;
        }
        
        private GameCondition EnforceConditionOn(Map map)
        {
            GameCondition gameCondition = GetConditionInstance(map);
            if (gameCondition == null)
            {
                gameCondition = CreateConditionOn(map);
            }
            else
            {
                gameCondition.TicksLeft = gameCondition.TransitionTicks;
            }
            return gameCondition;
        }
        
        protected virtual GameCondition CreateConditionOn(Map map)
        {
            GameCondition gameCondition = GameConditionMaker.MakeCondition(ConditionDef);
            gameCondition.Duration = gameCondition.TransitionTicks;
            gameCondition.conditionCauser = parent;
            gameCondition.hideSource = Props.hideSource;
            map.gameConditionManager.RegisterCondition(gameCondition);
            causedConditions.Add(map, gameCondition);
            SetupCondition(gameCondition, map);
            return gameCondition;
        }
        
        protected virtual void SetupCondition(GameCondition condition, Map map)
        {
            condition.suppressEndMessage = true;
        }
        
        // 停止所有游戏条件
        public void StopAllConditions()
        {
            foreach (var condition in causedConditions.Values)
            {
                if (!condition.Expired)
                {
                    condition.End();
                }
            }
            causedConditions.Clear();
        }
        
        public override string CompInspectStringExtra()
        {
            if (DebugSettings.godMode && Active)
            {
                GameCondition gameCondition = parent.Map?.GameConditionManager?.ActiveConditions?
                    .Find((GameCondition c) => c.def == Props.conditionDef);
                
                if (gameCondition != null)
                {
                    return $"[DEV] 传送条件激活中\n[DEV] 已过时间: {gameCondition.TicksPassed}\n[DEV] 剩余时间: {gameCondition.TicksLeft}";
                }
            }
            return base.CompInspectStringExtra();
        }
    }
}
