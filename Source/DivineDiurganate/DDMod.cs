using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    [StaticConstructorOnStartup]
    public class DDMod : Mod
    {
        public DDMod(ModContentPack content) : base(content)
        {
            // 初始化Harmony
            var harmony = new Harmony("com.kalospacer.arachnaeswarm");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
