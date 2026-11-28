using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    public class MainMenuScreen : UiScreen
    {
        public Action PlayClicked, HowToClicked, SettingsClicked, QuitClicked;
        RectTransform logo;
        float t;

        protected override void OnBuild(RectTransform root)
        {
            logo = UIKit.NewRect("Logo", root).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -10f), new Vector2(1000f, 460f));
            var li = UIKit.Img(logo, "logo", Color.white, false);
            li.preserveAspect = true;

            var play = UIKit.Button(root, "Play", "Begin Quest", () => { if (PlayClicked != null) PlayClicked(); }, true, 46, "play");
            ((RectTransform)play.transform).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -520f), new Vector2(560f, 118f));

            string[] labels = { "How to Play", "Settings", "Quit" };
            string[] icons = { "book", "gear", "door" };
            Action[] acts =
            {
                () => { if (HowToClicked != null) HowToClicked(); },
                () => { if (SettingsClicked != null) SettingsClicked(); },
                () => { if (QuitClicked != null) QuitClicked(); },
            };
            for (int i = 0; i < 3; i++)
            {
                var b = UIKit.Button(root, labels[i], labels[i], acts[i], false, 34, icons[i]);
                ((RectTransform)b.transform).Place(UIKit.TC, UIKit.TC, new Vector2(0f, -650f - i * 104f), new Vector2(460f, 92f));
            }

            var ver = UIKit.AddText(root, "HexQuest  -  rules v" + "0.1", 24, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft).Shadow();
            ver.rectTransform.Place(UIKit.BL, UIKit.BL, new Vector2(30f, 20f), new Vector2(600f, 40f));
            var hint = UIKit.AddText(root, "Fonts: Cinzel Decorative & Almendra (SIL OFL)", 20, new Color(1f, 1f, 1f, 0.4f), TextAnchor.LowerRight).Shadow(1.5f);
            hint.rectTransform.Place(UIKit.BR, UIKit.BR, new Vector2(-30f, 20f), new Vector2(800f, 36f));
        }

        void Update()
        {
            if (logo == null || !IsOpen) return;
            t += Time.unscaledDeltaTime;
            logo.anchoredPosition = new Vector2(0f, -10f + Mathf.Sin(t * 0.9f) * 6f);
        }
    }
}
