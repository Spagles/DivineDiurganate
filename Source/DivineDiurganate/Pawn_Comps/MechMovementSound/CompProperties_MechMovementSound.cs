// CompProperties_MechMovementSound.cs
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace DivineDiurganate
{
    public class CompProperties_MechMovementSound : CompProperties
    {
        // 只保留最基础的音效配置
        public SoundDef movementSound;
        
        // 基础控制
        public bool requirePilot = false; // 是否需要驾驶员
        public bool requirePower = false; // 是否需要电源
        public float minMovementSpeed = 0.1f; // 触发音效的最小移动速度
        
        public CompProperties_MechMovementSound()
        {
            this.compClass = typeof(CompMechMovementSound);
        }
        
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            
            if (movementSound == null)
            {
                yield return $"movementSound is not defined for {parentDef.defName}";
            }
            
            if (minMovementSpeed < 0f)
            {
                yield return $"minMovementSpeed cannot be negative for {parentDef.defName}";
            }
            
            // 如果需要驾驶员，检查是否配置了驾驶员容器
            if (requirePilot && parentDef.GetCompProperties<CompProperties_MechPilotHolder>() == null)
            {
                Log.Warning($"[DD] requirePilot is true but no CompProperties_MechPilotHolder found for {parentDef.defName}");
            }
        }
    }
}
