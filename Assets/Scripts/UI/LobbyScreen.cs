using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>
    /// Party lobby: choose how many heroes, who controls each seat (you + AI opponents), board size,
    /// rounds and map seed, then start. Shows whether the game server is reachable.
    /// </summary>
    public class LobbyScreen : UiScreen
    {
        public Action BackClicked;
        public Action<MatchConfig> StartClicked;
        public Action<Action<bool, string>> CheckServer;     // set by AppFlow

        // Agent catalogue: server spec -> fantasy name + blurb
        static readonly string[] Specs = { "random", "greedy", "planner", "learner" };
        static readonly string[] Names = { "Wanderer", "Warlord", "Strategist", "Oracle" };
        static readonly string[] Stars = { "Easy", "Normal", "Hard", "Learned" };
        static readonly string[] Blurbs =
        {
            "Wanderer - walks and grabs at random. A gentle first opponent.",
            "Warlord - always takes the best immediate gain. Greedy but fast.",
            "Strategist - simulates many futures and plays for the final score. A real challenge.",
            "Oracle - a mind trained by self-play, with learned judgement of the map.",
        };
        static readonly string[] BoardOptions = { "Small  (61 hexes)", "Large  (91 hexes)" };
        static readonly string[] RoundOptions = { "20 rounds", "30 rounds", "40 rounds", "60 rounds" };
        static readonly int[] RoundValues = { 20, 30, 40, 60 };

        MatchConfig cfg;

        sealed class SeatRow
        {
            public RectTransform root;
            public Text label;
            public UiCycler cycler;
            public GameObject remove;
        }

        readonly List<SeatRow> rows = new List<SeatRow>();
        UiButton addButton, startButton;
        Text blurb, seedText, statusText, offlineNote;
        Image statusDot;
        UiCycler boardCycler, roundCycler;
        RectTransform seatPanel;

        protected override void OnBuild(RectTransform root)
        {
            var title = UIKit.AddText(root, "Muster Your Party", 64, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow(3f);
            title.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -36f), new Vector2(1200f, 90f));
            UIKit.Divider(root, 760f, UIKit.TC, new Vector2(0f, -130f));

            BuildSeatPanel(root);
            BuildSettingsPanel(root);

            var back = UIKit.Button(root, "Back", "Back", () => { if (BackClicked != null) BackClicked(); }, false, 36, "cancel");
            ((RectTransform)back.transform).Place(UIKit.BL, UIKit.BL, new Vector2(60f, 50f), new Vector2(320f, 96f));
            startButton = UIKit.Button(root, "Start", "Start Quest", OnStart, true, 44, "play");
            ((RectTransform)startButton.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-60f, 44f), new Vector2(520f, 112f));

            // server status chip (bottom centre)
            var chip = UIKit.NewRect("Status", root).Place(UIKit.BC, UIKit.BC, new Vector2(-30f, 56f), new Vector2(640f, 72f));
            UIKit.Img(chip, "ui_chip", Color.white, false);
            var dot = UIKit.NewRect("Dot", chip).Place(UIKit.ML, UIKit.MC, new Vector2(34f, 0f), new Vector2(24f, 24f));
            statusDot = UIKit.Img(dot, "coin", Palette.Dim, false);
            statusText = UIKit.AddText(chip, "Checking server...", 26, Palette.Cream, TextAnchor.MiddleLeft, false);
            statusText.rectTransform.StretchXY(68f, 0f, 150f, 0f);
            var test = UIKit.Button(chip, "Test", "Test", () => TestServer(), false, 24);
            ((RectTransform)test.transform).Place(UIKit.MR, UIKit.MR, new Vector2(-8f, 0f), new Vector2(130f, 56f));
        }

        // ------------------------------------------------------------------ seats

        void BuildSeatPanel(RectTransform root)
        {
            seatPanel = UIKit.Panel(root, "SeatPanel");
            seatPanel.Place(UIKit.TL, UIKit.TL, new Vector2(60f, -170f), new Vector2(1010f, 720f));
            var head = UIKit.AddText(seatPanel, "The Party", 40, Palette.Gold, TextAnchor.MiddleLeft, true).Shadow();
            head.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(52f, -30f), new Vector2(600f, 60f));

            for (int i = 0; i < 6; i++)
            {
                int seat = i;
                var row = new SeatRow();
                row.root = UIKit.NewRect("Seat " + (i + 1), seatPanel).Place(UIKit.TL, UIKit.TL, new Vector2(46f, -100f - i * 84f), new Vector2(918f, 76f));
                UIKit.Img(row.root, "ui_chip", new Color(1f, 1f, 1f, 0.55f), false);
                var hex = UIKit.NewRect("Hex", row.root).Place(UIKit.ML, UIKit.MC, new Vector2(46f, 0f), new Vector2(54f, 62f));
                UIKit.Img(hex, "ui_hex_rim", Color.white, false);
                UIKit.Img(UIKit.NewRect("Face", hex).Stretch(), "ui_hex_face", Palette.ForPlayer(i), false);
                UIKit.AddText(hex, (i + 1).ToString(), 28, Color.white, TextAnchor.MiddleCenter, false).Shadow(1.5f);

                row.label = UIKit.AddText(row.root, Palette.NameForPlayer(i), 30, Palette.Cream, TextAnchor.MiddleLeft, true).Shadow();
                row.label.rectTransform.StretchXY(92f, 0f, 560f, 0f);

                if (i == 0)
                {
                    var you = UIKit.AddText(row.root, "You  -  Human hero", 28, Palette.Gold, TextAnchor.MiddleLeft, true).Shadow();
                    you.rectTransform.StretchXY(330f, 0f, 20f, 0f);
                }
                else
                {
                    var names = new string[Names.Length];
                    for (int k = 0; k < names.Length; k++) names[k] = Names[k] + "  (" + Stars[k] + ")";
                    row.cycler = UIKit.Cycler(row.root, "Agent", names, 1, idx => OnAgentChanged(seat, idx), new Vector2(430f, 60f));
                    ((RectTransform)row.cycler.transform).Place(UIKit.MR, UIKit.MR, new Vector2(i >= 2 ? -78f : -22f, 0f), new Vector2(430f, 60f));
                    if (i >= 2)
                    {
                        var rm = UIKit.RoundButton(row.root, "Remove", "cancel", () => RemoveSeat(), 56f, new Color(0, 0, 0, 0));
                        ((RectTransform)rm.transform).Place(UIKit.MR, UIKit.MR, new Vector2(-14f, 0f), new Vector2(56f, 56f));
                        row.remove = rm.gameObject;
                    }
                }
                rows.Add(row);
            }

            addButton = UIKit.Button(seatPanel, "Add", "+  Add a seat", AddSeat, false, 30);
            blurb = UIKit.AddText(seatPanel, "", 26, Palette.Dim, TextAnchor.MiddleLeft, false).Shadow(1.5f);
            blurb.rectTransform.Place(UIKit.BL, UIKit.BL, new Vector2(52f, 22f), new Vector2(900f, 70f));
        }

        void OnAgentChanged(int seat, int idx)
        {
            while (cfg.agents.Count < 5) cfg.agents.Add("greedy");
            cfg.agents[seat - 1] = Specs[idx];
            blurb.text = Blurbs[idx];
        }

        void AddSeat()
        {
            if (cfg.player_count >= 6) return;
            cfg.player_count++;
            RefreshSeats();
        }

        void RemoveSeat()
        {
            if (cfg.player_count <= 2) return;
            cfg.player_count--;
            RefreshSeats();
        }

        void RefreshSeats()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool active = i < cfg.player_count;
                rows[i].root.gameObject.SetActive(active);
                if (active && rows[i].cycler != null)
                {
                    string spec = cfg.agents[i - 1].Split(':')[0];
                    int idx = Array.IndexOf(Specs, spec);
                    rows[i].cycler.SetOptions(CurrentNames(), idx < 0 ? 1 : idx);
                }
                if (rows[i].remove != null) rows[i].remove.SetActive(i == cfg.player_count - 1 && cfg.player_count > 2);
            }
            addButton.gameObject.SetActive(cfg.player_count < 6);
            ((RectTransform)addButton.transform).Place(UIKit.TL, UIKit.TL,
                new Vector2(46f, -100f - cfg.player_count * 84f), new Vector2(918f, 70f));
        }

        static string[] CurrentNames()
        {
            var names = new string[Names.Length];
            for (int k = 0; k < names.Length; k++) names[k] = Names[k] + "  (" + Stars[k] + ")";
            return names;
        }

        // ------------------------------------------------------------------ settings

        void BuildSettingsPanel(RectTransform root)
        {
            var panel = UIKit.Panel(root, "QuestPanel");
            panel.Place(UIKit.TR, UIKit.TR, new Vector2(-60f, -170f), new Vector2(690f, 720f));
            var head = UIKit.AddText(panel, "The Quest", 40, Palette.Gold, TextAnchor.MiddleLeft, true).Shadow();
            head.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(52f, -30f), new Vector2(500f, 60f));

            Row(panel, "Board", -120f);
            boardCycler = UIKit.Cycler(panel, "Board", BoardOptions, 0, idx => cfg.board_radius = idx == 0 ? 4 : 5, new Vector2(380f, 64f));
            ((RectTransform)boardCycler.transform).Place(UIKit.TR, UIKit.TR, new Vector2(-40f, -108f), new Vector2(380f, 64f));

            Row(panel, "Length", -214f);
            roundCycler = UIKit.Cycler(panel, "Rounds", RoundOptions, 2, idx => cfg.rounds = RoundValues[idx], new Vector2(380f, 64f));
            ((RectTransform)roundCycler.transform).Place(UIKit.TR, UIKit.TR, new Vector2(-40f, -202f), new Vector2(380f, 64f));

            Row(panel, "Map seed", -308f);
            seedText = UIKit.AddText(panel, "", 38, Palette.Cream, TextAnchor.MiddleCenter, false).Shadow();
            seedText.rectTransform.Place(UIKit.TR, UIKit.TR, new Vector2(-150f, -296f), new Vector2(230f, 64f));
            var dice = UIKit.RoundButton(panel, "Dice", "dice", () => { cfg.map_seed = UnityEngine.Random.Range(1, 99999); RefreshSeed(); }, 76f, new Color(0, 0, 0, 0));
            ((RectTransform)dice.transform).Place(UIKit.TR, UIKit.TR, new Vector2(-48f, -290f), new Vector2(76f, 76f));
            dice.Tip = "Roll a new map";

            UIKit.Divider(panel, 560f, UIKit.TC, new Vector2(0f, -420f));

            var rules = UIKit.AddText(panel,
                "Each round every hero acts once. Claim land, gather gold, upgrade, and fight over the border. " +
                "When the last round ends, the highest score wins: 10 per hex plus 1 per coin.",
                27, Palette.Cream, TextAnchor.UpperLeft, false).Shadow(1.5f);
            rules.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(52f, -450f), new Vector2(590f, 170f));

            offlineNote = UIKit.AddText(panel, "", 24, Palette.Gold, TextAnchor.UpperLeft, false).Shadow(1.5f);
            offlineNote.rectTransform.Place(UIKit.BL, UIKit.BL, new Vector2(52f, 24f), new Vector2(590f, 80f));
        }

        void Row(Transform parent, string label, float y)
        {
            var t = UIKit.AddText(parent, label, 34, Palette.Cream, TextAnchor.MiddleLeft, true).Shadow();
            t.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(52f, y + 10f), new Vector2(240f, 64f));
        }

        void RefreshSeed() { seedText.text = "#" + cfg.map_seed; }

        // ------------------------------------------------------------------ flow

        protected override void OnOpen()
        {
            cfg = UserSettings.LoadMatchConfig();
            boardCycler.SetIndex(cfg.board_radius >= 5 ? 1 : 0, false);
            int ri = Array.IndexOf(RoundValues, cfg.rounds);
            roundCycler.SetIndex(ri < 0 ? 2 : ri, false);
            RefreshSeed();
            RefreshSeats();
            blurb.text = Blurbs[2];
            offlineNote.text = UserSettings.UseMock
                ? "Offline practice mode: opponents are simple built-in wanderers and the chosen types are ignored. Change this in Settings."
                : "";
            TestServer();
        }

        void TestServer()
        {
            if (UserSettings.UseMock)
            {
                SetStatus(Palette.Gold, "Offline practice mode (no server needed)");
                return;
            }
            SetStatus(Palette.Dim, "Contacting " + UserSettings.ServerUrl + " ...");
            if (CheckServer != null) CheckServer((ok, msg) => SetStatus(ok ? Palette.Good : Palette.Danger, msg));
        }

        void SetStatus(Color c, string text)
        {
            if (statusDot == null) return;
            statusDot.color = c;
            statusText.text = text;
        }

        void OnStart()
        {
            if (StartClicked != null) StartClicked(cfg);
        }

        public override bool OnEscape()
        {
            if (BackClicked != null) BackClicked();
            return true;
        }
    }
}
