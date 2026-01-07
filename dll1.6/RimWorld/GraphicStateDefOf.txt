using Verse;

namespace RimWorld;

[DefOf]
public static class GraphicStateDefOf
{
	public static GraphicStateDef Swimming;

	static GraphicStateDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(GraphicStateDefOf));
	}
}
