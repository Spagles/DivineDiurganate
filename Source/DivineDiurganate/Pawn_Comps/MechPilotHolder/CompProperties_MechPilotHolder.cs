// File: CompProperties_MechPilotHolder.cs (修改版)
using RimWorld;
using Verse;

namespace DivineDiurganate
{
    public class CompProperties_MechPilotHolder : CompProperties
    {
        public int maxPilots = 1;
        public string pilotWorkTag = "MechPilot";

        // 新增：驾驶员图标配置
        public string summonPilotIcon = "DivineDiurganate/UI/Commands/DD_Enter_Mech";
        public string ejectPilotIcon = "DivineDiurganate/UI/Commands/DD_Exit_Mech";
        public string ejectSinglePilotIcon = null;

        // 新增：低血量自动弹出配置
        public float autoEjectHealthPercent = 0.3f; // 默认30%血量时自动弹出
        public bool autoEjectEnabled = true; // 是否启用自动弹出
        public bool blockEntryWhenLowHealth = true; // 低血量时是否禁止进入
        public float minHealthForEntry = 0.5f; // 允许进入的最低血量百分比（默认50%）

        public CompProperties_MechPilotHolder()
        {
            this.compClass = typeof(CompMechPilotHolder);
        }

        // 新增：加载图标的方法
        public Texture2D GetSummonPilotIcon()
        {
            if (!string.IsNullOrEmpty(summonPilotIcon) && ContentFinder<Texture2D>.Get(summonPilotIcon, false) != null)
            {
                return ContentFinder<Texture2D>.Get(summonPilotIcon);
            }
            return ContentFinder<Texture2D>.Get("UI/Commands/SummonPilot", false) ??
                   BaseContent.BadTex;
        }

        public Texture2D GetEjectPilotIcon()
        {
            if (!string.IsNullOrEmpty(ejectPilotIcon) && ContentFinder<Texture2D>.Get(ejectPilotIcon, false) != null)
            {
                return ContentFinder<Texture2D>.Get(ejectPilotIcon);
            }
            return ContentFinder<Texture2D>.Get("UI/Commands/Eject", false) ??
                   BaseContent.BadTex;
        }
    }
}
