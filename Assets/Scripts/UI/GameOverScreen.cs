using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Final standings with a crown for the winner.</summary>
    public class GameOverScreen : UiScreen
    {
        public Action RematchClicked, LobbyClicked, MenuClicked;
        Text headline, subline;
        RectTransform list;
        Image crown;

        protected override void OnBuild(RectTransform root)
        {
            var dim = UIKit.NewRect("Dim", root).Stretch();
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.72f);

            var panel = UIKit.Panel(root, "Panel");
            panel.Place(UIKit.MC, UIKit.MC, Vector2.zero, new Vector2(1000f, 960f));
            crown = UIKit.Icon(panel, "crown", Palette.Gold, 150f, UIKit.TC, new Vector2(0f, -30f));
            headline = UIKit.AddText(panel, "", 74, Palette.Gold, TextAnchor.MiddleCenter, true).Shadow(3f);
            headline.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -190f), new Vector2(900f, 96f));
            subline = UIKit.AddText(panel, "", 32, Palette.Cream, TextAnchor.MiddleCenter, false).Shadow();
            subline.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -290f), new Vector2(900f, 50f));
            UIKit.Divider(panel, 640f, UIKit.TC, new Vector2(0f, -350f));

            list = UIKit.NewRect("List", panel).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -380f), new Vector2(860f, 400f));

            var again = UIKit.Button(panel, "Rematch", "Rematch", () => { if (RematchClicked != null) RematchClicked(); }, true, 36, "dice");
            ((RectTransform)again.transform).Place(UIKit.BC, UIKit.BC, new Vector2(0f, 150f), new Vector2(500f, 96f));
            var lobby = UIKit.Button(panel, "Lobby", "Lobby", () => { if (LobbyClicked != null) LobbyClicked(); }, false, 30, "seats");
            ((RectTransform)lobby.transform).Place(UIKit.BL, UIKit.BL, new Vector2(70f, 40f), new Vector2(380f, 84f));
            var menu = UIKit.Button(panel, "Menu", "Main Menu", () => { if (MenuClicked != null) MenuClicked(); }, false, 30, "door");
            ((RectTransform)menu.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-70f, 40f), new Vector2(380f, 84f));
        }

        public void Present(StateMessage s, int localId)
        {
            var ranking = new List<PlayerData>(s.players);
            ranking.Sort((a, b) => b.score.CompareTo(a.score));
            var winner = ranking[0];
            bool tie = ranking.Count > 1 && ranking[1].score == winner.score;
            bool won = winner.player_id == localId && !tie;

            headline.text = won ? "Victory!" : (tie ? "A Hard-Fought Draw" : winner.name + " Prevails");
            headline.color = won ? Palette.Gold : (tie ? Palette.Cream : Palette.Danger);
            crown.color = won ? Palette.Gold : new Color(0.7f, 0.7f, 0.75f);
            subline.text = won ? "Your banner rules the realm." : "The realm bows to " + winner.name + ".";

            foreach (Transform c in list) Destroy(c.gameObject);
            var owned = new int[s.players.Count];
            foreach (var c in s.cells) if (c.owner >= 0) owned[c.owner]++;
            for (int i = 0; i < ranking.Count; i++)
            {
                var p = ranking[i];
                var row = UIKit.NewRect("Row", list).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -i * 66f), new Vector2(860f, 60f));
                UIKit.Img(row, "ui_chip", new Color(1f, 1f, 1f, p.player_id == localId ? 0.95f : 0.5f), false);
                UIKit.AddText(row, (i + 1).ToString(), 36, Palette.Gold, TextAnchor.MiddleCenter, false).rectTransform.StretchXY(0f, 0f, 800f, 0f);
                var hex = UIKit.NewRect("Hex", row).Place(UIKit.ML, UIKit.MC, new Vector2(90f, 0f), new Vector2(40f, 46f));
                UIKit.Img(hex, "ui_hex_rim", Color.white, false);
                UIKit.Img(UIKit.NewRect("F", hex).Stretch(), "ui_hex_face", Palette.ForPlayer(p.player_id), false);
                var name = UIKit.AddText(row, p.name + (p.player_id == localId ? "  (you)" : ""), 32, Palette.Cream, TextAnchor.MiddleLeft, false).Shadow(1.5f);
                name.rectTransform.StretchXY(132f, 0f, 330f, 0f);
                var lands = UIKit.AddText(row, owned[p.player_id] + " hexes   " + p.resources + " coins", 24, Palette.Dim, TextAnchor.MiddleRight, false);
                lands.rectTransform.StretchXY(0f, 0f, 170f, 0f);
                var score = UIKit.AddText(row, p.score.ToString(), 40, Palette.Gold, TextAnchor.MiddleRight, false).Shadow();
                score.rectTransform.StretchXY(0f, 0f, 24f, 0f);
            }
        }

        public override bool OnEscape() { return true; }     // must choose an option
    }
}
