using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    public class PauseScreen : UiScreen
    {
        public Action ResumeClicked, SettingsClicked, HowToClicked, RestartClicked, LobbyClicked, MenuClicked;

        protected override void OnBuild(RectTransform root)
        {
            var dim = UIKit.NewRect("Dim", root).Stretch();
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.7f);

            var panel = UIKit.Panel(root, "Panel");
            panel.Place(UIKit.MC, UIKit.MC, Vector2.zero, new Vector2(700f, 880f));
            var title = UIKit.AddText(panel, "Paused", 66, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow(3f);
            title.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -40f), new Vector2(600f, 90f));
            UIKit.Divider(panel, 520f, UIKit.TC, new Vector2(0f, -140f));

            string[] labels = { "Resume", "Settings", "How to Play", "Restart Match", "Back to Lobby", "Main Menu" };
            string[] icons = { "play", "gear", "book", "dice", "seats", "door" };
            Action[] acts =
            {
                () => Fire(ResumeClicked), () => Fire(SettingsClicked), () => Fire(HowToClicked),
                () => Fire(RestartClicked), () => Fire(LobbyClicked), () => Fire(MenuClicked),
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIKit.Button(panel, labels[i], labels[i], acts[i], i == 0, 32, icons[i]);
                ((RectTransform)b.transform).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -180f - i * 106f), new Vector2(520f, 92f));
            }
        }

        static void Fire(Action a) { if (a != null) a(); }

        public override bool OnEscape() { Fire(ResumeClicked); return true; }
    }
}
