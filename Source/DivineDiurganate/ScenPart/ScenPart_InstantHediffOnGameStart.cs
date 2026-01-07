using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    /// <summary>
    /// 场景部件：在游戏开始后立即对玩家殖民地的所有人类施加Hediff
    /// </summary>
    public class ScenPart_InstantHediffOnGameStart : ScenPart
    {
        private HediffDef hediff;
        private FloatRange severityRange = new FloatRange(1f, 1f);
        private float chance = 1f;
        private string chanceBuf;

        // 用于配置界面的临时变量
        private static readonly Vector2 HediffIconSize = new Vector2(24f, 24f);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref hediff, "hediff");
            Scribe_Values.Look(ref severityRange, "severityRange");
            Scribe_Values.Look(ref chance, "chance", 1f);
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            Rect scenPartRect = listing.GetScenPartRect(this, ScenPart.RowHeight * 3f + 31f);
            
            // Hediff选择按钮
            Rect hediffRect = scenPartRect.TopPartPixels(ScenPart.RowHeight);
            if (Widgets.ButtonText(hediffRect, hediff?.LabelCap ?? "SelectHediff".Translate()))
            {
                OpenHediffSelectionMenu();
            }

            // 严重程度范围滑块
            Rect severityRect = new Rect(scenPartRect.x, scenPartRect.y + ScenPart.RowHeight, scenPartRect.width, 31f);
            float maxSeverity = GetMaxSeverity();
            Widgets.FloatRange(severityRect, listing.CurHeight.GetHashCode(), ref severityRange, 0f, maxSeverity, "ConfigurableSeverity");

            // 概率设置
            Rect chanceRect = new Rect(scenPartRect.x, severityRect.yMax, scenPartRect.width, ScenPart.RowHeight);
            DoChanceEditInterface(chanceRect);
        }

        private void OpenHediffSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (HediffDef hd in PossibleHediffs())
            {
                string label = hd.LabelCap;

                options.Add(new FloatMenuOption(label, () => {
                    hediff = hd;
                    // 如果选择的hediff有致死严重度，调整范围
                    if (severityRange.max > GetMaxSeverity())
                    {
                        severityRange.max = GetMaxSeverity();
                    }
                }));
            }

            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private IEnumerable<HediffDef> PossibleHediffs()
        {
            return DefDatabase<HediffDef>.AllDefsListForReading
                .Where(x => x.scenarioCanAdd && !x.isBad && x.stages != null && x.stages.Count > 0)
                .OrderBy(x => x.LabelCap.RawText);
        }

        private float GetMaxSeverity()
        {
            if (hediff == null) return 1f;
            if (hediff.lethalSeverity > 0f) return hediff.lethalSeverity * 0.99f;
            if (hediff.maxSeverity > 0f) return hediff.maxSeverity;
            return 1f;
        }

        private void DoChanceEditInterface(Rect rect)
        {
            Rect labelRect = rect.LeftPart(0.3f).Rounded();
            Rect fieldRect = rect.RightPart(0.7f).Rounded();
            
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(labelRect, "chance".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            
            Widgets.TextFieldPercent(fieldRect, ref chance, ref chanceBuf);
        }

        public override string Summary(Scenario scen)
        {
            if (hediff == null) return "No hediff selected";
            
            return "ScenPart_InstantHediffOnGameStart".Translate(
                chance.ToStringPercent(),
                hediff.label,
                severityRange.min.ToStringPercent(),
                severityRange.max.ToStringPercent()
            ).CapitalizeFirst();
        }

        public override void Randomize()
        {
            base.Randomize();
            
            var possibleHediffs = PossibleHediffs().ToList();
            if (possibleHediffs.Any())
            {
                hediff = possibleHediffs.RandomElement();
                float maxSeverity = GetMaxSeverity();
                severityRange.max = Rand.Range(maxSeverity * 0.2f, maxSeverity * 0.95f);
                severityRange.min = severityRange.max * Rand.Range(0f, 0.95f);
                chance = GenMath.RoundedHundredth(Rand.Range(0.5f, 1f));
            }
        }

        /// <summary>
        /// 游戏开始后立即执行
        /// </summary>
        public override void PostGameStart()
        {
            base.PostGameStart();
            
            // 遍历所有地图，对玩家殖民地的所有人类施加Hediff
            foreach (Map map in Find.Maps)
            {
                ApplyHediffToPlayerColonists(map);
            }
        }

        /// <summary>
        /// 对指定地图中的玩家殖民地成员施加Hediff
        /// </summary>
        private void ApplyHediffToPlayerColonists(Map map)
        {
            if (map == null || hediff == null) return;

            // 获取地图中所有活着的、属于玩家殖民地的、人类pawn
            List<Pawn> colonists = map.mapPawns.FreeColonistsAndPrisoners
                .Where(p => p.RaceProps.Humanlike && !p.Dead && !p.Destroyed)
                .ToList();

            foreach (Pawn pawn in colonists)
            {
                // 检查概率
                if (!Rand.Chance(chance))
                {
                    continue;
                }

                try
                {
                    // 检查pawn是否已经有这个hediff
                    var existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
                    if (existingHediff != null)
                    {
                        // 如果已有，更新严重度
                        float newSeverity = severityRange.RandomInRange;
                        if (newSeverity > existingHediff.Severity)
                        {
                            existingHediff.Severity = newSeverity;
                        }
                    }
                    else
                    {
                        // 如果没有，添加新的hediff
                        Hediff newHediff = HediffMaker.MakeHediff(hediff, pawn);
                        newHediff.Severity = severityRange.RandomInRange;
                        pawn.health.AddHediff(newHediff);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error($"ScenPart_InstantHediffOnGameStart: 对 {pawn.NameShortColored} 施加Hediff时出错: {ex}");
                }
            }
        }

        /// <summary>
        /// 地图加载后立即执行（用于加载存档后的处理）
        /// </summary>
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            
            // 注意：这个方法会在每次地图生成时调用，包括加载存档时
            // 为了避免重复施加，我们可以检查游戏是否已经开始了足够长的时间
            // 或者我们可以依赖PostGameStart只在游戏开始时调用的特性
            // 但为了保险起见，我们只在游戏刚刚开始时处理
            if (Find.TickManager.TicksGame < 1000) // 游戏开始后的前1000 ticks
            {
                ApplyHediffToPlayerColonists(map);
            }
        }

        public override bool HasNullDefs()
        {
            if (base.HasNullDefs())
            {
                return true;
            }
            return hediff == null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (hediff == null)
            {
                yield return "No hediff selected for ScenPart_InstantHediffOnGameStart";
            }
        }

        public override int GetHashCode()
        {
            int hashCode = base.GetHashCode();
            hashCode = (hashCode * 397) ^ (hediff != null ? hediff.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ severityRange.GetHashCode();
            hashCode = (hashCode * 397) ^ chance.GetHashCode();
            return hashCode;
        }
    }
}
