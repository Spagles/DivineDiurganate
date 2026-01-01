// File: RitualBehaviorWorker_ExorcistSpeech_Fixed.cs
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DivineDiurganate
{
    
    public class RitualBehaviorWorker_ExorcistSpeech : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_ExorcistSpeech()
        {
        }

        public RitualBehaviorWorker_ExorcistSpeech(RitualBehaviorDef def)
            : base(def)
        {
        }

        protected override LordJob CreateLordJob(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            // 获取分配为"speaker"角色的驱魔人
            Pawn speaker = assignments.AssignedPawns("speaker").First();
            return new LordJob_Joinable_Speech(target, speaker, ritual, def.stages, assignments, titleSpeech: false);
        }

        protected override void PostExecute(TargetInfo target, Pawn organizer, Precept_Ritual ritual, RitualObligation obligation, RitualRoleAssignments assignments)
        {
            // 获取驱魔人演讲者
            Pawn speaker = assignments.AssignedPawns("speaker").First();
            
            // 发送通知信件
            Find.LetterStack.ReceiveLetter(
                def.letterTitle.Formatted(ritual.Named("RITUAL")),
                def.letterText.Formatted(speaker.Named("SPEAKER"), ritual.Named("RITUAL"), ritual.ideo.MemberNamePlural.Named("IDEOMEMBERS")) + "\n\n" + ritual.outcomeEffect.ExtraAlertParagraph(ritual),
                LetterDefOf.PositiveEvent,
                target
            );
        }

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            // 检查意识形态中是否有驱魔人角色
            if (ritual?.ideo?.RolesListForReading == null)
                return "DD_NoIdeoOrRoles".Translate();
            
            // 修复：使用GetNamed而不是DD_PreceptDefOf（可能未初始化）
            PreceptDef exorcistDef = DefDatabase<PreceptDef>.GetNamed("DD_IdeoRole_Exorcist", false);
            if (exorcistDef == null)
                return "DD_ExorcistRoleDefNotFound".Translate();
            
            Precept_Role exorcistRole = ritual.ideo.RolesListForReading.FirstOrDefault((Precept_Role r) => r.def == exorcistDef);
            
            if (exorcistRole == null)
            {
                return "DD_NoExorcistRoleInIdeo".Translate(ritual.ideo.name);
            }
            
            if (exorcistRole.ChosenPawnSingle() == null)
            {
                // 驱魔人角色未分配人员
                return "CantStartRitualRoleNotAssigned".Translate(exorcistRole.LabelCap);
            }
            
            // 检查驱魔人是否可用
            Pawn exorcist = exorcistRole.ChosenPawnSingle();
            if (exorcist == null || exorcist.Dead || exorcist.Downed || !exorcist.Spawned)
            {
                return "DD_ExorcistUnavailable".Translate();
            }
            
            // 检查驱魔人是否能够移动和说话
            if (!exorcist.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return "DD_ExorcistCannotMove".Translate();
            }
            
            if (!exorcist.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                return "DD_ExorcistCannotTalk".Translate();
            }
            
            // 调用基类检查
            return base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
        }

        // 检查pawn是否可以担任角色（这里检查驱魔人）
        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string reason, TargetInfo ritualTarget)
        {
            reason = "";
            
            // 如果是speaker角色，需要检查是否为驱魔人
            if (role?.id == "speaker")
            {
                // 安全性检查
                if (pawn == null)
                {
                    reason = "Pawn is null";
                    return false;
                }
                
                // 检查pawn是否有意识形态
                if (pawn.Ideo == null)
                {
                    reason = "DD_PawnHasNoIdeo".Translate();
                    return false;
                }
                
                // 获取驱魔人角色定义
                PreceptDef exorcistDef = DefDatabase<PreceptDef>.GetNamed("DD_IdeoRole_Exorcist", false);
                if (exorcistDef == null)
                {
                    reason = "DD_ExorcistRoleDefNotFound".Translate();
                    return false;
                }
                
                // 检查pawn是否有驱魔人角色
                var exorcistRole = pawn.Ideo.RolesListForReading.FirstOrDefault(r => r?.def == exorcistDef);
                
                if (exorcistRole == null)
                {
                    reason = "DD_MustBeExorcist".Translate();
                    return false;
                }
                
                if (exorcistRole.ChosenPawnSingle() != pawn)
                {
                    reason = "DD_NotAssignedExorcist".Translate();
                    return false;
                }
                
                // 检查pawn是否能够说话
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
                {
                    reason = "CannotTalk".Translate();
                    return false;
                }
                
                // 检查pawn是否能够移动
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                {
                    reason = "CannotMove".Translate();
                    return false;
                }
                
                // 检查pawn是否有战斗能力（战前布道需要）
                if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                {
                    reason = "DD_MustBeWarrior".Translate();
                    return false;
                }
            }
            
            // 对于其他角色，使用默认检查
            return base.PawnCanFillRole(pawn, role, out reason, ritualTarget);
        }

        // 检查目标是否仍然允许（战前布道可能需要战斗环境）
        public override bool TargetStillAllowed(TargetInfo selectedTarget, LordJob_Ritual ritual)
        {
            // 基础检查
            if (!base.TargetStillAllowed(selectedTarget, ritual))
                return false;
            
            // 战前布道需要在殖民地范围内进行
            Map map = selectedTarget.Map;
            if (map == null)
                return false;
            
            return true;
        }
    }
}
