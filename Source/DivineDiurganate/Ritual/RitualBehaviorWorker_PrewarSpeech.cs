// Assembly-CSharp, Version=1.6.9438.37837, Culture=neutral, PublicKeyToken=null
// RimWorld.RitualBehaviorWorker_Speech
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DivineDiurganate {
    public class RitualBehaviorWorker_PrewarSpeech : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_PrewarSpeech()
        {
        }

        public RitualBehaviorWorker_PrewarSpeech(RitualBehaviorDef def)
            : base(def)
        {
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            Pawn organizer2 = assignments.AssignedPawns("speaker").First();
            return new LordJob_Joinable_Speech(target, organizer2, ritual, def.stages, assignments, titleSpeech: false);
        }

        protected override void PostExecute(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            Pawn arg = assignments.AssignedPawns("speaker").First();
            Find.LetterStack.ReceiveLetter(def.letterTitle.Formatted(ritual.Named("RITUAL")), def.letterText.Formatted(arg.Named("SPEAKER"), ritual.Named("RITUAL"), ritual.ideo.MemberNamePlural.Named("IDEOMEMBERS")) + "\n\n" + ritual.outcomeEffect.ExtraAlertParagraph(ritual), LetterDefOf.PositiveEvent, target);
        }

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            Precept_Role precept_Role = ritual.ideo.RolesListForReading.FirstOrDefault((Precept_Role r) => r.def == DD_PreceptDefOf.DD_IdeoRole_Exorcist);
            if (precept_Role == null)
            {
                return null;
            }
            if (precept_Role.ChosenPawnSingle() == null)
            {
                return "CantStartRitualRoleNotAssigned".Translate(precept_Role.LabelCap);
            }
            return base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
        }
    }

}