using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Illustrated-by-text rules on parchment, paged.</summary>
    public class HowToScreen : UiScreen
    {
        public Action CloseClicked;
        int page;
        Text title, body, pageText;
        UiButton prev, next;

        static readonly string[] Titles = { "The Goal", "Your Turn", "The Six Deeds", "Battle and Fate", "Seeing the Map" };
        static readonly string[] Bodies =
        {
            "Rule the map.\n\nAfter the final round the hero with the highest score wins.\n\n" +
            "Every hex you hold is worth 10 points. Every coin you keep is worth 1.\n\n" +
            "Ties go to the hero holding more land.",

            "Each round, every hero acts once, in seat order.\n\n" +
            "Choose a deed from the bar at the bottom (keys 1-6), then click one of the glowing hexes. " +
            "Gather and Upgrade happen at once. Press Space to pass.\n\n" +
            "Deeds that cannot be used right now are greyed out.",

            "Move - step your hero to a neighbouring hex.\n" +
            "Claim - pay coins to take a free hex touching your land.\n" +
            "Reinforce - add a soldier to one of your hexes.\n" +
            "Attack - strike an enemy hex next to a hex of yours holding two or more soldiers.\n" +
            "Gather - collect coins where your hero stands; deposits and upgrades add more.\n" +
            "Upgrade - pay coins to gather more every time.",

            "In an attack each soldier rolls a die and the totals are compared. The attacker needs the higher total; " +
            "ties favour the defender. A winner takes the hex and marches in.\n\n" +
            "Fate intervenes: deposits can surge or run dry, rockslides can close hexes for a few rounds, " +
            "and a hero may abandon the quest. None of it is announced in advance.",

            "Right-drag or WASD  -  pan the camera\n" +
            "Mouse wheel  -  zoom\n" +
            "Esc  -  cancel an action, or open the menu\n" +
            "Space  -  pass your turn\n\n" +
            "A shield on a hex shows its soldiers. A gold coin shows a deposit. " +
            "A coloured glow shows who owns the land.",
        };

        protected override void OnBuild(RectTransform root)
        {
            var dim = UIKit.NewRect("Dim", root).Stretch();
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.78f);

            var panel = UIKit.Panel(root, "Parchment", "ui_panel_parchment");
            panel.Place(UIKit.MC, UIKit.MC, Vector2.zero, new Vector2(1240f, 800f));
            title = UIKit.AddText(panel, "", 58, Palette.Ink, TextAnchor.MiddleCenter, true);
            title.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -60f), new Vector2(1000f, 80f));
            UIKit.Divider(panel, 640f, UIKit.TC, new Vector2(0f, -150f)).GetComponent<Image>().color = Palette.GoldDark;
            body = UIKit.AddText(panel, "", 36, Palette.Ink, TextAnchor.UpperLeft, false);
            body.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(110f, -185f), new Vector2(1020f, 440f));

            prev = UIKit.Button(panel, "Prev", "Back", () => Go(-1), false, 30);
            ((RectTransform)prev.transform).Place(UIKit.BL, UIKit.BL, new Vector2(80f, 60f), new Vector2(240f, 80f));
            next = UIKit.Button(panel, "Next", "Next", () => Go(1), true, 30);
            ((RectTransform)next.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-80f, 60f), new Vector2(240f, 80f));
            pageText = UIKit.AddText(panel, "", 30, Palette.Ink, TextAnchor.MiddleCenter, false);
            pageText.rectTransform.Place(UIKit.BC, UIKit.BC, new Vector2(0f, 72f), new Vector2(300f, 50f));
        }

        void Go(int d)
        {
            page += d;
            if (page >= Titles.Length) { if (CloseClicked != null) CloseClicked(); return; }
            page = Mathf.Max(0, page);
            Show();
        }

        void Show()
        {
            title.text = Titles[page];
            body.text = Bodies[page];
            pageText.text = (page + 1) + " / " + Titles.Length;
            prev.Interactable = page > 0;
            ((Text)next.Label).text = page == Titles.Length - 1 ? "Done" : "Next";
        }

        protected override void OnOpen() { page = 0; Show(); }

        public override bool OnEscape() { if (CloseClicked != null) CloseClicked(); return true; }
    }
}
