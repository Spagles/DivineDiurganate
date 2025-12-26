using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace DivineDiurganate
{
    public class CompAbilityEffect_DestroyOwnBodyPart : CompAbilityEffect
    {
        public new CompProperties_AbilityDestroyOwnBodyPart Props => (CompProperties_AbilityDestroyOwnBodyPart)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null || caster.Dead)
                return;

            // 获取要破坏的身体部位
            List<BodyPartRecord> partsToDestroy = GetBodyPartsToDestroy(caster);
            
            if (partsToDestroy.Count == 0)
                return;

            // 对每个部位执行破坏
            foreach (BodyPartRecord part in partsToDestroy)
            {
                DestroyBodyPart(caster, part);
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            Pawn caster = parent.pawn;
            if (caster == null || caster.Dead)
            {
                reason = "CasterDead".Translate();
                return true;
            }

            List<BodyPartRecord> partsToDestroy = GetBodyPartsToDestroy(caster);
            if (partsToDestroy.Count == 0)
            {
                reason = "NoValidBodyParts".Translate();
                return true;
            }

            reason = null;
            return false;
        }

        // 在能力描述中显示会破坏的部位
        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster == null || caster.Dead)
                return null;

            List<BodyPartRecord> partsToDestroy = GetBodyPartsToDestroy(caster);
            if (partsToDestroy.Count == 0)
                return null;

            string partsText = GetBodyPartNames(partsToDestroy);
            return "WillDestroyBodyPart".Translate(partsText);
        }

        // 获取要破坏的身体部位列表
        private List<BodyPartRecord> GetBodyPartsToDestroy(Pawn pawn)
        {
            List<BodyPartRecord> validParts = new List<BodyPartRecord>();

            // 获取指定的身体部位
            foreach (BodyPartDef partDef in Props.bodyPartsToDestroy)
            {
                var parts = pawn.RaceProps.body.GetPartsWithDef(partDef);
                foreach (BodyPartRecord part in parts)
                {
                    // 检查部位是否已经缺失
                    if (!pawn.health.hediffSet.PartIsMissing(part))
                    {
                        validParts.Add(part);
                    }
                }
            }

            return validParts;
        }

        // 实际执行身体部位的破坏
        private void DestroyBodyPart(Pawn pawn, BodyPartRecord part)
        {
            // 直接添加缺失部位hediff
            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
        }

        // 获取身体部位名称列表
        private string GetBodyPartNames(List<BodyPartRecord> parts)
        {
            if (parts.Count == 0)
                return "";

            if (parts.Count == 1)
                return parts[0].Label;

            string result = "";
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    if (i == parts.Count - 1)
                        result += " and ";
                    else
                        result += ", ";
                }
                result += parts[i].Label;
            }
            return result;
        }
    }
}
