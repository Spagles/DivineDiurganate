using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class VerbProperties_FireSpew : VerbProperties
    {
        public float degrees = 26f; // Default based on original Verb_SpewFire (13 * 2)
        public DamageDef damageDef;
        public int damageAmount = -1;
        public float armorPenetration = -1f;
        public ThingDef filthDef;
        public EffecterDef effecterDef;
        public float propagationSpeed = -1f; // Speed of the explosion spread
        public float chanceToStartFire = -1f; // Chance to start a fire
        public bool avoidFriendlyFire = true; // Avoids damaging non-hostile targets

        public VerbProperties_FireSpew()
        {
            this.verbClass = typeof(Verb_ShootFireSpew);
        }
    }
}