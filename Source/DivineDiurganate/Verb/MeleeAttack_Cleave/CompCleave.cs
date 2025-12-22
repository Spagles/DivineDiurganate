using Verse;

namespace DivineDiurganate
{
    public class CompProperties_Cleave : CompProperties
    {
        public float cleaveAngle = 90f;
        public float cleaveRange = 2.9f;
        public float cleaveDamageFactor = 0.7f;
        public bool damageDowned = false;
        public DamageDef explosionDamageDef = null;
        
        // 新增：攻击特效
        public EffecterDef attackEffecter = null;
        public EffecterDef cleaveEffecter = null;
        public SoundDef attackSound = null;

        public CompProperties_Cleave()
        {
            this.compClass = typeof(CompCleave);
        }
    }

    public class CompCleave : ThingComp
    {
        public CompProperties_Cleave Props => (CompProperties_Cleave)this.props;
    }
}
