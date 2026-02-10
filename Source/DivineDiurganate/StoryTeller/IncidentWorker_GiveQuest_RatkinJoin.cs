using System.Linq;
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class IncidentWorker_GiveQuest_RatkinJoin : IncidentWorker_GiveQuest
    {
        private const string QuestDefName = "DD_SP_Ratkin_Join";
        private const string TriggeredFlag = "DD_SP_Ratkin_Join_Triggered";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            if (HasTriggeredFlag())
                return false;

            var quests = Find.QuestManager?.QuestsListForReading;
            if (quests != null && quests.Any(q => q?.root?.defName == QuestDefName))
                return false;

            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (HasTriggeredFlag())
                return false;

            bool result = base.TryExecuteWorker(parms);
            if (result)
            {
                var variableManager = Find.World?.GetComponent<EventVariableManager>();
                variableManager?.SetVariable(TriggeredFlag, 1);
            }

            return result;
        }

        private static bool HasTriggeredFlag()
        {
            var variableManager = Find.World?.GetComponent<EventVariableManager>();
            return variableManager != null && variableManager.HasVariable(TriggeredFlag);
        }
    }
}
