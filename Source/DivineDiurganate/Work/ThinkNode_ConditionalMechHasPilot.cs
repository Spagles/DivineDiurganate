// ThinkNode_ConditionalMechHasPilot.cs
using RimWorld;
using Verse;
using Verse.AI;

namespace DivineDiurganate
{
    public class ThinkNode_ConditionalMechHasPilot : ThinkNode_Conditional
    {
        // 可选的：可以在XML中设置的参数
        public int minPilotCount = 1;  // 最少需要的驾驶员数量
        
        protected override bool Satisfied(Pawn pawn)
        {
            var pilotComp = pawn.TryGetComp<CompMechPilotHolder>();
            
            return !pilotComp.HasPilots;
        }
        
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalMechHasPilot thinkNode_ConditionalMechHasPilot = (ThinkNode_ConditionalMechHasPilot)base.DeepCopy(resolve);
            thinkNode_ConditionalMechHasPilot.minPilotCount = minPilotCount;
            return thinkNode_ConditionalMechHasPilot;
        }
    }
}
