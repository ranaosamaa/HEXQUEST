using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>
    /// In-game HUD built from the fantasy UI art. It only DISPLAYS state and raises events;
    /// GameController decides what happens.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public static readonly ActionType[] CardOrder =
            { ActionType.Move, ActionType.Claim, ActionType.Reinforce, ActionType.Attack, ActionType.Gather, ActionType.Upgrade };
        static readonly string[] CardLabels = { "Move", "Claim", "Reinforce", "Attack", "Gather", "Upgrade" };
        static readonly string[] CardIcons = { "move", "claim", "reinforce", "attack", "gather", "upgrade" };
        static readonly string[] CardColors = { "#3FA7D6", "#4FB04A", "#D79A22", "#D6483F", "#8A5BE0", "#2FB59B" };
        static readonly string[] CardTips =
        {
            "Move your hero to a neighbouring hex.",
            "Spend resources to claim a neutral hex next to your land.",
            "Add one soldier to a hex you own.",
            "Strike an adjacent enemy hex. Dice decide; ties favour the defender.",
            "Collect resources where your hero stands. Deposits and upgrades add more.",
            "Spend resources to gather more every time you gather.",
        };

        public event Action<ActionType> ActionClicked;
        public event Action PassClicked, CancelClicked, FocusClicked, StatsClicked, SeatsClicked, MenuClicked;

        sealed class Card
        {
            public UiButton button;
            public Image face;
            public Color faceColor;
            public GameObject chip;
            public Text cost;
        }

        readonly List<Card> cards = new List<Card>();
        readonly List<RectTransform> turnHexes = new List<RectTransform>();
        readonly List<Image> turnFaces = new List<Image>();
        readonly List<Image> turnRims = new List<Image>();
        readonly List<string> feed = new List<string>();

        RectTransform root, turnRow, infoPanel, progressFill;
        Text nameText, subText, coinText, landText, roundText, bannerText, tipText, feedText, infoTitle, infoBody;
        Image portraitFace;
        UiButton passButton, cancelButton;
        CanvasGroup group;
        float coinShown, coinGoal, landShown, landGoal;
        float bannerPop;

        public bool InfoVisible { get { return infoPanel.gameObject.activeSelf; } }

        // =================================================================== build

        public void Build(Transform canvasRoot)
        {
            root = UIKit.NewRect("HUD", canvasRoot).Stretch();
            group = root.gameObject.AddComponent<CanvasGroup>();
            BuildPlayerPlate();
            BuildTurnRow();
            BuildResources();
            BuildActionBar();
            BuildCorners();
            BuildFeedAndBanner();
            BuildInfoPanel();
            root.gameObject.SetActive(false);
        }

        public void Show(bool visible) { root.gameObject.SetActive(visible); }
        public void SetInteractable(bool on) { group.blocksRaycasts = on; }

        void BuildPlayerPlate()
        {
            var plate = UIKit.Panel(root, "PlayerPlate");
            plate.Place(UIKit.TL, UIKit.TL, new Vector2(24f, -20f), new Vector2(470f, 124f));
            var hexBox = UIKit.NewRect("Portrait", plate).Place(UIKit.ML, UIKit.ML, new Vector2(24f, 0f), new Vector2(92f, 106f));
            UIKit.Img(hexBox, "ui_hex_rim", Color.white, false);
            var face = UIKit.NewRect("Face", hexBox).Stretch();
            portraitFace = UIKit.Img(face, "ui_hex_face", Palette.Players[0], false);
            UIKit.Icon(hexBox, "crown", Color.white, 46f, UIKit.MC, Vector2.zero);

            nameText = UIKit.AddText(plate, "", 40, Palette.Cream, TextAnchor.MiddleLeft, false).Shadow();
            nameText.rectTransform.StretchXY(136f, 58f, 20f, 14f);
            subText = UIKit.AddText(plate, "", 26, Palette.Dim, TextAnchor.MiddleLeft).Shadow(1.5f);
            subText.rectTransform.StretchXY(136f, 18f, 20f, 70f);
        }

        void BuildTurnRow()
        {
            turnRow = UIKit.NewRect("TurnOrder", root).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -22f), new Vector2(620f, 100f));
            roundText = UIKit.AddText(root, "", 30, Palette.Cream, TextAnchor.MiddleCenter, false).Shadow();
            roundText.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -122f), new Vector2(520f, 34f));

            var track = UIKit.NewRect("RoundBar", root).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -160f), new Vector2(420f, 24f));
            UIKit.Img(track, "ui_slider_track", Color.white, false);
            progressFill = UIKit.NewRect("Fill", track);
            UIKit.Img(progressFill, "ui_slider_fill", Color.white, false);
            progressFill.anchorMin = new Vector2(0f, 0f);
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.pivot = new Vector2(0f, 0.5f);
            progressFill.anchoredPosition = new Vector2(5f, 0f);
            progressFill.sizeDelta = new Vector2(10f, -10f);
        }

        void BuildResources()
        {
            var plate = UIKit.Panel(root, "ResourcePlate");
            plate.Place(UIKit.TR, UIKit.TR, new Vector2(-236f, -20f), new Vector2(410f, 104f));
            var coin = UIKit.NewRect("Coin", plate).Place(UIKit.ML, UIKit.MC, new Vector2(60f, 0f), new Vector2(66f, 66f));
            UIKit.Img(coin, "coin", Color.white, false);
            coinText = UIKit.AddText(plate, "0", 48, Palette.Cream, TextAnchor.MiddleLeft, false).Shadow();
            coinText.rectTransform.StretchXY(104f, 0f, 200f, 0f);
            var land = UIKit.NewRect("Land", plate).Place(UIKit.ML, UIKit.MC, new Vector2(232f, 0f), new Vector2(60f, 60f));
            UIKit.Img(land, "icon_territory", Palette.Good, false);
            landText = UIKit.AddText(plate, "0", 48, Palette.Cream, TextAnchor.MiddleLeft, false).Shadow();
            landText.rectTransform.StretchXY(274f, 0f, 10f, 0f);

            var stats = UIKit.RoundButton(root, "Stats", "stats", () => { if (StatsClicked != null) StatsClicked(); }, 98f, new Color(0, 0, 0, 0));
            ((RectTransform)stats.transform).Place(UIKit.TR, UIKit.TR, new Vector2(-126f, -20f), new Vector2(98f, 98f));
            stats.Tip = "Scoreboard";
            WireTip(stats);
            var menu = UIKit.RoundButton(root, "Menu", "menu", () => { if (MenuClicked != null) MenuClicked(); }, 98f, new Color(0, 0, 0, 0));
            ((RectTransform)menu.transform).Place(UIKit.TR, UIKit.TR, new Vector2(-20f, -20f), new Vector2(98f, 98f));
            menu.Tip = "Menu (Esc)";
            WireTip(menu);
        }

        void BuildActionBar()
        {
            var bar = UIKit.Panel(root, "ActionBar");
            bar.Place(UIKit.BC, UIKit.BC, new Vector2(-40f, 16f), new Vector2(846f, 180f));
            float[] xs = { 28f, 150f, 272f, 394f, 566f, 688f };
            for (int i = 0; i < CardOrder.Length; i++)
            {
                var type = CardOrder[i];
                Color col;
                ColorUtility.TryParseHtmlString(CardColors[i], out col);
                var card = MakeCard(bar, i, col, xs[i]);
                card.button.Clicked = () => { if (ActionClicked != null) ActionClicked(type); };
                cards.Add(card);
            }
            var divider = UIKit.NewRect("Divider", bar).Place(UIKit.ML, UIKit.MC, new Vector2(537f, 0f), new Vector2(18f, 18f));
            UIKit.Img(divider, "ui_hex_face", Palette.Gold, false);
        }

        Card MakeCard(Transform parent, int index, Color color, float x)
        {
            var rt = UIKit.NewRect("Card " + CardLabels[index], parent)
                .Place(UIKit.ML, UIKit.ML, new Vector2(x, 0f), new Vector2(112f, 144f));
            var c = new Card { faceColor = color };

            var faceRt = UIKit.NewRect("Face", rt).Stretch(7f);
            c.face = UIKit.Img(faceRt, "ui_face", color, false);
            var frame = UIKit.Img(UIKit.NewRect("Frame", rt).Stretch(), "ui_card_frame", Color.white);
            c.button = rt.gameObject.AddComponent<UiButton>();
            c.button.Target = frame;
            c.button.HoverScale = 1.07f;
            c.button.Tip = CardTips[index];
            WireTip(c.button);

            UIKit.Icon(rt, CardIcons[index], Palette.Cream, 64f, UIKit.TC, new Vector2(0f, -22f));
            var label = UIKit.AddText(rt, CardLabels[index], 19, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow(1.5f);
            label.rectTransform.StretchXY(2f, 38f, 2f, 92f);
            var key = UIKit.AddText(rt, (index + 1).ToString(), 22, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperLeft, false).Shadow(1.5f);
            key.rectTransform.StretchXY(14f, 0f, 0f, 12f);

            var chip = UIKit.NewRect("Chip", rt).Place(UIKit.BC, UIKit.BC, new Vector2(0f, 10f), new Vector2(86f, 32f));
            UIKit.Img(chip, "ui_chip", Color.white, false);
            var coin = UIKit.NewRect("Coin", chip).Place(UIKit.ML, UIKit.MC, new Vector2(18f, 0f), new Vector2(24f, 24f));
            UIKit.Img(coin, "coin", Color.white, false);
            c.cost = UIKit.AddText(chip, "0", 22, Palette.Cream, TextAnchor.MiddleCenter, false);
            c.cost.rectTransform.offsetMin = new Vector2(28f, 0f);
            c.chip = chip.gameObject;
            return c;
        }

        void BuildCorners()
        {
            var empty = new Color(0, 0, 0, 0);
            passButton = UIKit.RoundButton(root, "Pass", "pass", () => { if (PassClicked != null) PassClicked(); }, 200f, Palette.Purple, "PASS");
            ((RectTransform)passButton.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-26f, 26f), new Vector2(200f, 200f));
            passButton.Tip = "End your turn without acting (Space).";
            WireTip(passButton);

            cancelButton = UIKit.RoundButton(root, "Cancel", "cancel", () => { if (CancelClicked != null) CancelClicked(); }, 96f, empty);
            ((RectTransform)cancelButton.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-246f, 30f), new Vector2(96f, 96f));
            cancelButton.Tip = "Cancel the selected action (Esc).";
            WireTip(cancelButton);

            var focus = UIKit.RoundButton(root, "Focus", "focus", () => { if (FocusClicked != null) FocusClicked(); }, 92f, empty);
            ((RectTransform)focus.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-30f, 250f), new Vector2(92f, 92f));
            focus.Tip = "Centre the camera on your hero.";
            WireTip(focus);

            var seats = UIKit.RoundButton(root, "Seats", "seats", () => { if (SeatsClicked != null) SeatsClicked(); }, 92f, empty);
            ((RectTransform)seats.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-30f, 352f), new Vector2(92f, 92f));
            seats.Tip = "Who controls each seat.";
            WireTip(seats);
        }

        void BuildFeedAndBanner()
        {
            feedText = UIKit.AddText(root, "", 24, Palette.Cream, TextAnchor.LowerLeft, false).Shadow();
            feedText.rectTransform.Place(UIKit.BL, UIKit.BL, new Vector2(30f, 30f), new Vector2(480f, 300f));

            bannerText = UIKit.AddText(root, "", 38, Color.white, TextAnchor.MiddleCenter, false).Shadow(2.5f);
            bannerText.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -200f), new Vector2(1200f, 56f));

            tipText = UIKit.AddText(root, "", 26, Palette.Gold, TextAnchor.MiddleCenter, false).Shadow();
            tipText.rectTransform.Place(UIKit.BC, UIKit.BC, new Vector2(-40f, 204f), new Vector2(1000f, 40f));
        }

        void BuildInfoPanel()
        {
            infoPanel = UIKit.Panel(root, "InfoPanel", "ui_panel_parchment");
            infoPanel.Place(UIKit.MR, UIKit.MR, new Vector2(-150f, 30f), new Vector2(560f, 560f));
            infoTitle = UIKit.AddText(infoPanel, "", 40, Palette.Ink, TextAnchor.MiddleCenter, true);
            infoTitle.rectTransform.StretchXY(40f, 460f, 40f, 40f);
            infoBody = UIKit.AddText(infoPanel, "", 28, Palette.Ink, TextAnchor.UpperLeft, false);
            infoBody.rectTransform.StretchXY(50f, 70f, 50f, 120f);
            var close = UIKit.Button(infoPanel, "Close", "Close", HideInfo, true, 28);
            ((RectTransform)close.transform).Place(UIKit.BC, UIKit.BC, new Vector2(0f, 30f), new Vector2(220f, 70f));
            infoPanel.gameObject.SetActive(false);
        }

        void WireTip(UiButton b)
        {
            b.HoverChanged = on => tipText.text = on ? b.Tip : "";
        }

        // =================================================================== public API

        public void ShowInfo(string title, string body)
        {
            infoTitle.text = title;
            infoBody.text = body;
            infoPanel.gameObject.SetActive(true);
        }

        public void HideInfo() { infoPanel.gameObject.SetActive(false); }

        public void SetBanner(string text, bool pop = false)
        {
            bannerText.text = text;
            if (pop) bannerPop = 1f;
        }

        public void AddFeed(string line, bool isError = false)
        {
            feed.Add(isError ? "<color=#" + Palette.Hex(Palette.Danger) + ">" + line + "</color>" : line);
            if (feed.Count > 7) feed.RemoveAt(0);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < feed.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                float a = Mathf.Lerp(0.35f, 1f, (i + 1f) / feed.Count);
                sb.Append("<color=#F6E7C1").Append(Mathf.RoundToInt(a * 255f).ToString("X2")).Append('>').Append(feed[i]).Append("</color>");
            }
            feedText.text = sb.ToString();
        }

        public void ClearFeed() { feed.Clear(); feedText.text = ""; tipText.text = ""; bannerText.text = ""; }

        public void Refresh(StateMessage s, int localId, ActionType? activeAction)
        {
            var me = s.players[localId];
            bool myTurn = !s.game_over && s.active_player == localId;
            var actor = s.active_player >= 0 ? s.players[s.active_player] : me;

            portraitFace.color = Palette.ForPlayer(actor.player_id);
            nameText.text = s.game_over ? "Quest complete" : (s.active_player == localId ? "Your turn" : actor.name + "'s turn");
            subText.text = "Gather Lv " + me.gather_level + "   Score " + me.score;
            roundText.text = "ROUND " + Mathf.Min(s.round, s.max_rounds) + " / " + s.max_rounds;
            float frac = Mathf.Clamp01((s.round - 1f) / Mathf.Max(1, s.max_rounds));
            progressFill.sizeDelta = new Vector2(Mathf.Max(10f, 400f * frac), -10f);

            int owned = 0;
            foreach (var c in s.cells) if (c.owner == localId) owned++;
            coinGoal = me.resources;
            landGoal = owned;

            for (int i = 0; i < cards.Count; i++)
            {
                var type = CardOrder[i];
                var legal = s.legal_actions.Find(a => a.action_type == type);
                bool available = myTurn && legal != null;
                var card = cards[i];
                card.button.Interactable = available;
                card.face.color = available ? card.faceColor : Color.Lerp(card.faceColor, new Color(0.3f, 0.3f, 0.36f), 0.82f);
                card.button.Tint = (activeAction.HasValue && activeAction.Value == type) ? Palette.Lighten(Palette.Gold, 0.15f) : Color.white;
                card.chip.SetActive(available && legal.cost > 0);
                if (available) card.cost.text = legal.cost.ToString();
            }

            passButton.Interactable = myTurn;
            cancelButton.Interactable = activeAction.HasValue;
            RefreshTurnRow(s);
        }

        void RefreshTurnRow(StateMessage s)
        {
            int n = s.players.Count;
            if (turnHexes.Count != n)
            {
                foreach (var r in turnHexes) if (r != null) Destroy(r.gameObject);
                turnHexes.Clear(); turnFaces.Clear(); turnRims.Clear();
                for (int i = 0; i < n; i++)
                {
                    float x = (i - (n - 1) / 2f) * 84f;
                    var hex = UIKit.NewRect("Seat " + (i + 1), turnRow).Place(UIKit.MC, UIKit.MC, new Vector2(x, 0f), new Vector2(78f, 90f));
                    var rim = UIKit.Img(hex, "ui_hex_rim", Color.white, false);
                    var face = UIKit.Img(UIKit.NewRect("Face", hex).Stretch(), "ui_hex_face", Palette.ForPlayer(i), false);
                    UIKit.AddText(hex, (i + 1).ToString(), 32, Color.white, TextAnchor.MiddleCenter, false).Shadow();
                    turnHexes.Add(hex); turnFaces.Add(face); turnRims.Add(rim);
                }
            }
            for (int i = 0; i < n; i++)
            {
                bool active = i == s.active_player;
                bool alive = s.players[i].active;
                turnHexes[i].localScale = Vector3.one * (active ? 1.22f : 0.88f);
                turnRims[i].color = active ? Palette.Lighten(Palette.Gold, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
                turnFaces[i].color = alive ? Palette.ForPlayer(i) : new Color(0.3f, 0.3f, 0.3f);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            coinShown = Mathf.MoveTowards(coinShown, coinGoal, Mathf.Max(8f, Mathf.Abs(coinGoal - coinShown) * 6f) * dt);
            landShown = Mathf.MoveTowards(landShown, landGoal, Mathf.Max(8f, Mathf.Abs(landGoal - landShown) * 6f) * dt);
            if (coinText != null) coinText.text = Mathf.RoundToInt(coinShown).ToString();
            if (landText != null) landText.text = Mathf.RoundToInt(landShown).ToString();
            if (bannerPop > 0f && bannerText != null)
            {
                bannerPop = Mathf.Max(0f, bannerPop - dt * 3f);
                float s = 1f + 0.18f * bannerPop * bannerPop;
                bannerText.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
