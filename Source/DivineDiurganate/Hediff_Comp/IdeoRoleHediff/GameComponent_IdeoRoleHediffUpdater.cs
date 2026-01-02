// File: GameComponent_IdeoRoleHediffUpdater_Fixed.cs
using System.Collections.Generic;
using Verse;

namespace DivineDiurganate
{
    public class GameComponent_IdeoRoleHediffUpdater : GameComponent
    {
        private int lastUpdateTick = 0;
        private const int UpdateIntervalTicks = 300; // 1游戏天
        
        public GameComponent_IdeoRoleHediffUpdater(Game game)
        {
        }
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            
            // 定期更新所有殖民者的Hediff状态
            if (Find.TickManager.TicksGame >= lastUpdateTick + UpdateIntervalTicks)
            {
                lastUpdateTick = Find.TickManager.TicksGame;
                UpdateAllColonists();
            }
        }
        
        private void UpdateAllColonists()
        {
            if (Current.Game == null || Current.Game.Maps == null)
                return;
                
            int updatedCount = 0;
            int skippedCount = 0;
            
            foreach (var map in Current.Game.Maps)
            {
                if (map == null || !map.IsPlayerHome)
                    continue;
                    
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    // 检查是否为DDmechunit
                    if (IdeoRoleHediffManager.IsPawnExcluded(pawn))
                    {
                        skippedCount++;
                        continue;
                    }
                    
                    if (pawn != null && pawn.Spawned && !pawn.Dead)
                    {
                        IdeoRoleHediffManager.CheckPawn(pawn);
                        updatedCount++;
                    }
                }
            }
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastUpdateTick, "lastUpdateTick", 0);
        }
    }
}
