using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>
    /// Builds and updates the whole HUD, laid out like the reference screenshot:
    /// name + resource panels (top centre), stats/menu buttons (top right), turn-order hexes,
    /// action bar (bottom centre), big round Pass button (bottom right), event feed (bottom left).
    /// It only DISPLAYS state and raises events - GameController decides what happens.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public static readonly ActionType[] CardOrder =
            { ActionType.Move, ActionType.Claim, ActionType.Reinforce, ActionType.Attack, ActionType.Gather, ActionType.Upgrade };
        static readonly string[] CardColors = { "#3FA7D6", "#59C23A", "#D79A22", "#D6483F", "#8A5BE0", "#2FB59B" };

        public event Action<ActionType> ActionClicked;
        public event Action PassClicked, CancelClicked, FocusClicked, StatsClicked, SeatsClicked, MenuClicked, PanelButtonClicked;

        sealed class Card
        {
            public Button button;
            public Image border, face;
            public Color faceColor;
            public GameObject chip;
            public Text costText;
        }

        readonly List<Card> cards = new List<Card>();
        readonly List<RectTransform> turnRings = new List<RectTransform>();
        readonly List<Image> turnRingImages = new List<Image>();
        readonly List<string> feed = new List<string>();

        Text nameText, subText, coinText, territoryText, bannerText, feedText;
        Text sideTitle, sideBody, sideButtonLabel;
        Button passButton, cancelButton, sideButton;
        RectTransform turnRow, sidePanel;

        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        // =================================================================== build

        public void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGo.transform;
            BuildTopBar(root);
            BuildTurnRow(root);
            BuildActionBar(root);
            BuildCornerControls(root);
            BuildFeedAndBanner(root);
            BuildSidePanel(root);
        }

        void BuildTopBar(Transform root)
        {
            // Name panel (left of centre)
            var nameInner = UIKit.MetalPanel(root, "NamePanel", out var nameOuter);
            nameOuter.Place(TopCenter, new Vector2(1f, 1f), new Vector2(-8f, -14f), new Vector2(580f, 100f));
            nameText = UIKit.AddText(nameInner, "", 38, Color.white);
            nameText.rectTransform.Band(0.45f, 1f);
            subText = UIKit.AddText(nameInner, "", 22, new Color(0.75f, 0.8f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
            subText.rectTransform.Band(0f, 0.5f);

            // Resource panel (right of centre): coin = resources, green ring = territory
            var resInner = UIKit.MetalPanel(root, "ResourcePanel", out var resOuter);
            resOuter.Place(TopCenter, new Vector2(0f, 1f), new Vector2(8f, -14f), new Vector2(440f, 100f));

            var coin = UIKit.NewRect("Coin", resInner).Place(new Vector2(0f, 0.5f), Center, new Vector2(50f, 0f), new Vector2(60f, 60f));
            coin.AddImage(ProceduralArt.Circle(), Palette.Gold).raycastTarget = false;
            var coinCore = UIKit.NewRect("Core", coin).Stretch(12f);
            coinCore.AddImage(ProceduralArt.Circle(), Palette.GoldDark).raycastTarget = false;
            coinText = UIKit.AddText(resInner, "0", 42, Color.white, TextAnchor.MiddleLeft);
            coinText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(120f, 70f));

            var ring = UIKit.NewRect("Territory", resInner).Place(new Vector2(0f, 0.5f), Center, new Vector2(250f, 0f), new Vector2(60f, 60f));
            ring.AddImage(ProceduralArt.Circle(), Palette.Green).raycastTarget = false;
            var ringCore = UIKit.NewRect("Core", ring).Stretch(13f);
            ringCore.AddImage(ProceduralArt.Circle(), Palette.Panel).raycastTarget = false;
            territoryText = UIKit.AddText(resInner, "0", 42, Color.white, TextAnchor.MiddleLeft);
            territoryText.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(292f, 0f), new Vector2(120f, 70f));

            // Stats + menu (top right)
            var stats = UIKit.RoundButton(root, "StatsButton", "Stats", 20, Palette.Panel);
            ((RectTransform)stats.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -18f), new Vector2(84f, 84f));
            stats.onClick.AddListener(() => StatsClicked?.Invoke());
            var menu = UIKit.RoundButton(root, "MenuButton", "Menu", 20, Palette.Panel);
            ((RectTransform)menu.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(84f, 84f));
            menu.onClick.AddListener(() => MenuClicked?.Invoke());
        }

        void BuildTurnRow(Transform root)
        {
            turnRow = UIKit.NewRect("TurnOrder", root);
            turnRow.Place(TopCenter, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(700f, 96f));
        }

        void BuildActionBar(Transform root)
        {
            var inner = UIKit.MetalPanel(root, "ActionBar", out var outer);
            outer.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-110f, 12f), new Vector2(800f, 150f));

            string[] labels = { "Move", "Claim", "Reinforce", "Attack", "Gather", "Upgrade" };
            float[] xs = { 20f, 140f, 260f, 380f, 540f, 660f };   // gap before index 4 holds the divider
            for (int i = 0; i < CardOrder.Length; i++)
            {
                ColorUtility.TryParseHtmlString(CardColors[i], out var color);
                var type = CardOrder[i];
                var card = MakeCard(inner, labels[i], color, xs[i]);
                card.button.onClick.AddListener(() => ActionClicked?.Invoke(type));
                cards.Add(card);
            }

            // Diamond divider between targeted actions and instant actions
            var diamond = UIKit.NewRect("Divider", inner).Place(new Vector2(0f, 0.5f), Center, new Vector2(514f, 0f), new Vector2(14f, 14f));
            diamond.AddImage(null, Palette.Silver).raycastTarget = false;
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        Card MakeCard(Transform parent, string label, Color color, float x)
        {
            var rt = UIKit.NewRect("Card " + label, parent)
                .Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(108f, 122f));
            var c = new Card { faceColor = color };
            c.border = rt.AddImage(ProceduralArt.RoundedRect(), Palette.Silver);

            var face = UIKit.NewRect("Face", rt).Stretch(4f);
            c.face = face.AddImage(ProceduralArt.RoundedRect(), color);
            c.face.raycastTarget = false;
            var text = UIKit.AddText(face, label, 22, Color.white);
            text.rectTransform.Band(0.32f, 1f);
            UIKit.AddOutline(text);

            // Cost chip (gold coin + number), like the price tags in the reference
            var chip = UIKit.NewRect("Chip", rt).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(88f, 30f));
            chip.AddImage(ProceduralArt.RoundedRect(), Palette.Panel).raycastTarget = false;
            var coin = UIKit.NewRect("Coin", chip).Place(new Vector2(0f, 0.5f), Center, new Vector2(16f, 0f), new Vector2(20f, 20f));
            coin.AddImage(ProceduralArt.Circle(), Palette.Gold).raycastTarget = false;
            c.costText = UIKit.AddText(chip, "0", 20, Color.white);
            c.costText.rectTransform.offsetMin = new Vector2(30f, 0f);
            c.chip = chip.gameObject;

            c.button = rt.gameObject.AddComponent<Button>();
            c.button.targetGraphic = c.border;
            return c;
        }

        void BuildCornerControls(Transform root)
        {
            // Big round Pass button (the ">>" end-turn button in the reference)
            passButton = UIKit.RoundButton(root, "PassButton", ">>", 70, Palette.Purple);
            var passRt = (RectTransform)passButton.transform;
            passRt.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 22f), new Vector2(190f, 190f));
            passRt.GetComponentInChildren<Text>().rectTransform.Band(0.3f, 0.92f);
            var passLabel = UIKit.AddText(passRt, "PASS", 22, Color.white);
            passLabel.rectTransform.Band(0.1f, 0.34f);
            passButton.onClick.AddListener(() => PassClicked?.Invoke());

            // Cancel (the undo button in the reference)
            cancelButton = UIKit.RoundButton(root, "CancelButton", "Cancel", 18, Palette.Panel);
            ((RectTransform)cancelButton.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-232f, 30f), new Vector2(92f, 92f));
            cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());

            // Vertical side stack: Focus + Seats (the castle / sleep buttons in the reference)
            var inner = UIKit.MetalPanel(root, "SideStack", out var outer);
            outer.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 230f), new Vector2(112f, 240f));
            var focus = UIKit.RoundButton(inner, "FocusButton", "Focus", 20, Palette.Panel);
            ((RectTransform)focus.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(84f, 84f));
            focus.onClick.AddListener(() => FocusClicked?.Invoke());
            var seats = UIKit.RoundButton(inner, "SeatsButton", "Seats", 20, Palette.Panel);
            ((RectTransform)seats.transform).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(84f, 84f));
            seats.onClick.AddListener(() => SeatsClicked?.Invoke());
        }

        void BuildFeedAndBanner(Transform root)
        {
            feedText = UIKit.AddText(root, "", 22, Color.white, TextAnchor.LowerLeft, FontStyle.Bold);
            feedText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(410f, 300f));
            UIKit.AddOutline(feedText);

            bannerText = UIKit.AddText(root, "", 30, Color.white);
            bannerText.rectTransform.Place(TopCenter, new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(1000f, 50f));
            UIKit.AddOutline(bannerText);
        }

        void BuildSidePanel(Transform root)
        {
            var inner = UIKit.MetalPanel(root, "InfoPanel", out sidePanel);
            sidePanel.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-140f, 20f), new Vector2(480f, 480f));
            sideTitle = UIKit.AddText(inner, "", 36, Color.white);
            sideTitle.rectTransform.Band(0.86f, 1f);
            sideBody = UIKit.AddText(inner, "", 24, Color.white, TextAnchor.UpperLeft, FontStyle.Normal);
            sideBody.rectTransform.Band(0.2f, 0.86f);
            sideBody.rectTransform.offsetMin = new Vector2(24f, 0f);
            sideBody.rectTransform.offsetMax = new Vector2(-24f, 0f);

            var btn = UIKit.NewRect("PanelButton", inner).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(300f, 62f));
            var img = btn.AddImage(ProceduralArt.RoundedRect(), Palette.Purple);
            sideButton = btn.gameObject.AddComponent<Button>();
            sideButton.targetGraphic = img;
            sideButtonLabel = UIKit.AddText(btn, "", 26, Color.white);
            sideButton.onClick.AddListener(() => PanelButtonClicked?.Invoke());
            sidePanel.gameObject.SetActive(false);
        }

        // =================================================================== public API

        public bool PanelVisible => sidePanel.gameObject.activeSelf;

        public void ShowPanel(string title, string body, string buttonLabel)
        {
            sideTitle.text = title;
            sideBody.text = body;
            sideButton.gameObject.SetActive(buttonLabel != null);
            if (buttonLabel != null) sideButtonLabel.text = buttonLabel;
            sidePanel.gameObject.SetActive(true);
        }

        public void HidePanel() => sidePanel.gameObject.SetActive(false);
        public void SetBanner(string text) => bannerText.text = text;

        public void AddFeed(string line, bool isError = false)
        {
            feed.Add(isError ? "<color=#FF8A8A>" + line + "</color>" : line);
            if (feed.Count > 8) feed.RemoveAt(0);
            feedText.text = string.Join("\n", feed);
        }

        public void ClearFeed() { feed.Clear(); feedText.text = ""; }

        public void Refresh(StateMessage s, int localId, ActionType? activeAction)
        {
            var me = s.players[localId];
            bool myTurn = !s.game_over && s.active_player == localId;

            nameText.text = s.game_over ? "Match over"
                          : (s.active_player == localId ? "Your turn" : s.players[s.active_player].name + "'s turn");
            subText.text = $"Round {Mathf.Min(s.round, s.max_rounds)}/{s.max_rounds}   |   Gather Lv {me.gather_level}   |   Score {me.score}";

            int owned = 0;
            foreach (var c in s.cells) if (c.owner == localId) owned++;
            coinText.text = me.resources.ToString();
            territoryText.text = owned.ToString();

            for (int i = 0; i < cards.Count; i++)
            {
                var type = CardOrder[i];
                var legal = s.legal_actions.Find(a => a.action_type == type);
                bool available = myTurn && legal != null;
                var card = cards[i];
                card.button.interactable = available;
                card.face.color = available ? card.faceColor : Color.Lerp(card.faceColor, new Color(0.35f, 0.37f, 0.45f), 0.8f);
                card.border.color = (activeAction.HasValue && activeAction.Value == type) ? Palette.Gold : Palette.Silver;
                card.chip.SetActive(available && legal.cost > 0);
                if (available) card.costText.text = legal.cost.ToString();
            }

            passButton.interactable = myTurn;
            cancelButton.interactable = activeAction.HasValue;
            RefreshTurnRow(s);
        }

        void RefreshTurnRow(StateMessage s)
        {
            int n = s.players.Count;
            if (turnRings.Count != n)
            {
                foreach (var r in turnRings) if (r) Destroy(r.gameObject);
                turnRings.Clear(); turnRingImages.Clear();
                for (int i = 0; i < n; i++)
                {
                    float x = (i - (n - 1) / 2f) * 82f;
                    var ring = UIKit.NewRect("Seat " + (i + 1), turnRow).Place(Center, Center, new Vector2(x, 0f), new Vector2(80f, 80f));
                    var ringImg = ring.AddImage(ProceduralArt.HexUi(), Palette.Silver);
                    ringImg.raycastTarget = false;
                    var fill = UIKit.NewRect("Fill", ring).Stretch(8f);
                    fill.AddImage(ProceduralArt.HexUi(), Palette.ForPlayer(i)).raycastTarget = false;
                    UIKit.AddText(fill, (i + 1).ToString(), 28, Color.white);
                    turnRings.Add(ring); turnRingImages.Add(ringImg);
                }
            }
            for (int i = 0; i < n; i++)
            {
                bool active = i == s.active_player;
                turnRingImages[i].color = active ? Palette.Gold : Palette.Silver;
                turnRings[i].localScale = Vector3.one * (active ? 1.25f : 0.9f);
            }
        }
    }
}
