# AGENTS.md - DivineDiurganate RimWorld Mod

## Build Commands

### Primary Build Command
```bash
dotnet build "C:\Steam\steamapps\common\RimWorld\Mods\DivineDiurganate\Source\DivineDiurganate\DivineDiurganate.sln" -c Release
```

### Output Location
- Debug/Release: `C:\Steam\steamapps\common\RimWorld\Mods\DivineDiurganate\1.6\1.6\Assemblies\`

### Testing
Manual testing through RimWorld gameplay. No automated tests.

## Project Structure

```
DivineDiurganate\
├── Source\DivineDiurganate\      # C# source (SDK-style .csproj)
├── 1.6\                           # RimWorld mod files
└── BUILD_ENVIRONMENT.md           # Cloud build docs
```

## Code Style Guidelines

### Imports & Formatting
- Group RimWorld imports: `Verse`, `RimWorld`, `Verse.AI`, `Verse.Sound`, `UnityEngine`
- Group mod imports after RimWorld imports
- Use `using static` for utility classes when needed
- 4-space indentation, curly braces on new lines
- Use `var` when type is obvious, explicit types when clarity matters
- C# 11.0, .NET Framework 4.8, Build SDK: .NET 8.0.416

### Naming Conventions
- Classes/Methods/Properties: PascalCase (e.g., `CompFlyOverGenerator`, `CanActivateNow`)
- Fields: camelCase (e.g., `useCount`, `lastUseTick`), private: `callJobState`
- Constants: PascalCase (e.g., `WorkDurationTicks`)
- Namespaces: PascalCase (e.g., `DivineDiurganate.HarmonyPatches`)
- Harmony patches: `Patch_` prefix (e.g., `Patch_TakeDamage`)
- DefOf classes: `DD_` prefix (e.g., `DD_JobDefOf`)

### Harmony Patches
```csharp
[HarmonyPatch(typeof(Thing))]
[HarmonyPatch("TakeDamage")]
public static class Thing_TakeDamage_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Thing __instance, ref DamageInfo dinfo, ref DamageWorker.DamageResult __result)
    {
        return true;
    }
}
```

### DefOf Pattern
```csharp
[DefOf]
public static class DD_JobDefOf
{
    public static JobDef DD_EnterMech;
    static DD_JobDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DD_JobDefOf));
}
```

### Mod Initialization
```csharp
[StaticConstructorOnStartup]
public class DDMod : Mod
{
    public DDMod(ModContentPack content) : base(content)
    {
        new Harmony("com.kalospacer.arachnaeswarm").PatchAll(Assembly.GetExecutingAssembly());
    }
}
```

### Component Pattern
```csharp
public class CompFlyOverGenerator : ThingComp
{
    public CompProperties_FlyOverGenerator Props => (CompProperties_FlyOverGenerator)props;

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        useCount = 0;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
            yield return gizmo;
        yield return CreateFlyOverGizmo();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastUseTick, "lastUseTick", -99999);
    }
}
```

## Important Rules

### Knowledge Base Usage
When working on RimWorld modding, ALWAYS use `rimworld-knowledge-base` tool to:
- Search for correct class names, method signatures, and enum values
- Verify game mechanics and API usage
- Access decompiled RimWorld 1.6 source code
- **Do not rely on external memory or searches**

### Critical Paths
- Local C# Knowledge Base: `C:\Steam\steamapps\common\RimWorld\dll1.6`
- Mod Project: `C:\Steam\steamapps\common\RimWorld\Mods\DivineDiurganate`
- C# Project: `C:\Steam\steamapps\common\RimWorld\Mods\DivineDiurganate\Source\DivineDiurganate`

### Project File Sync
Modern SDK-style .csproj with `<EnableDefaultCompileItems>true>`. Files auto-included.

### Dependencies
- NuGet: `Krafs.Rimworld.Ref`, `Microsoft.NETFramework.ReferenceAssemblies.net48` (auto-restored)
- Local DLLs (in `Libs/`): `0Harmony.dll`, `AlienRace.dll`, `FacialAnimation.dll`
- Build SDK: .NET 8.0.416 (defined in root `global.json`)

## Additional Notes

### Serialization
Use `Scribe_Values.Look()` for primitives, `Scribe_Collections.Look()` for collections in `PostExposeData()`.

### Comments
- Use Chinese comments for Chinese-language code
- Use English comments for general API documentation
- XML documentation (`///`) for public APIs

### Gizmos
- Override `CompGetGizmosExtra()` for ThingComps
- Always yield base Gizmos first
- Use `DD_` prefix for mod-specific translation keys

### Debugging
Use `Log.Error()` for errors, `Log.Warning()` for warnings, `Log.Message()` for info.

### Signing Convention
沐雪写的代码会加上可爱的署名注释，例如：
```csharp
// ✨ 沐雪写的哦~
```
