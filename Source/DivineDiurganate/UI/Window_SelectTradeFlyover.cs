using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DivineDiurganate
{
    public class Window_SelectTradeFlyover : Window
    {
        private readonly List<FlyOver> flyovers;
        private readonly Action<FlyOver> onSelect;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public Window_SelectTradeFlyover(List<FlyOver> flyovers, Action<FlyOver> onSelect)
        {
            this.flyovers = flyovers;
            this.onSelect = onSelect;
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 40f), "DD_SelectTradeFlyover".Translate());
            Text.Font = GameFont.Small;

            Rect scrollRect = new Rect(0f, 50f, inRect.width, inRect.height - 90f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, flyovers.Count * 80f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (FlyOver flyover in flyovers)
            {
                Rect entryRect = new Rect(0f, y, viewRect.width, 75f);
                DrawFlyoverEntry(entryRect, flyover);
                y += 80f;
            }

            Widgets.EndScrollView();
        }

        private void DrawFlyoverEntry(Rect rect, FlyOver flyover)
        {
            Widgets.DrawMenuSection(rect);

            CompFlyoverTrader traderComp = flyover.GetComp<CompFlyoverTrader>();
            if (traderComp == null)
            {
                return;
            }

            Rect iconRect = new Rect(rect.x + 5f, rect.y + 5f, 65f, 65f);
            Widgets.ThingIcon(iconRect, flyover);

            float textX = rect.x + 80f;
            float textWidth = rect.width - 170f;

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(textX, rect.y + 5f, textWidth, 22f), flyover.LabelCap);

            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(textX, rect.y + 25f, textWidth, 20f),
                traderComp.TraderKind?.LabelCap ?? "Unknown");

            float progress = flyover.currentProgress * 100f;
            Widgets.Label(new Rect(textX, rect.y + 43f, textWidth, 20f),
                "DD_FlyoverProgress".Translate(progress.ToString("F0")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect buttonRect = new Rect(rect.xMax - 85f, rect.y + 20f, 80f, 35f);
            if (Widgets.ButtonText(buttonRect, "DD_Select".Translate()))
            {
                onSelect?.Invoke(flyover);
                Close();
            }
        }
    }
}
