using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Audio, display/resolution, graphics quality, UI scale and server connection. Saved instantly.</summary>
    public class SettingsScreen : UiScreen
    {
        public Action CloseClicked;
        List<Vector2Int> resolutions;
        UiCycler resCycler, qualityCycler;
        UiToggle fullscreen, vsync, offline;
        Text qualityNote, uiScaleText;
        InputField serverInput;
        UiSlider master, music, sfx, uiScale;

        protected override void OnBuild(RectTransform root)
        {
            var dim = UIKit.NewRect("Dim", root).Stretch();
            var di = dim.gameObject.AddComponent<Image>();
            di.color = new Color(0.02f, 0.03f, 0.08f, 0.78f);

            var panel = UIKit.Panel(root, "Panel");
            panel.Place(UIKit.MC, UIKit.MC, Vector2.zero, new Vector2(1540f, 900f));
            var title = UIKit.AddText(panel, "Settings", 60, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow(3f);
            title.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -34f), new Vector2(800f, 80f));
            UIKit.Divider(panel, 700f, UIKit.TC, new Vector2(0f, -120f));

            // ---- left column: sound + interface
            Header(panel, "Sound", 70f, -150f);
            master = SliderRow(panel, "Master volume", 70f, -210f, UserSettings.Master, v => UserSettings.Master = v);
            music = SliderRow(panel, "Music", 70f, -290f, UserSettings.Music, v => UserSettings.Music = v);
            sfx = SliderRow(panel, "Effects", 70f, -370f, UserSettings.Sfx, v => { UserSettings.Sfx = v; AudioManager.PlaySfx("claim", 0.5f); });

            Header(panel, "Interface", 70f, -470f);
            uiScale = SliderRow(panel, "UI size", 70f, -530f, Mathf.InverseLerp(0.8f, 1.3f, UserSettings.UiScale),
                v => { UserSettings.UiScale = Mathf.Lerp(0.8f, 1.3f, v); uiScaleText.text = Mathf.RoundToInt(UserSettings.UiScale * 100f) + "%"; });
            uiScaleText = UIKit.AddText(panel, "", 28, Palette.Gold, TextAnchor.MiddleRight, false);
            uiScaleText.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(600f, -480f), new Vector2(120f, 40f));

            // ---- right column: display + graphics
            Header(panel, "Display", 800f, -150f);
            Label(panel, "Fullscreen", 800f, -222f);
            fullscreen = UIKit.Toggle(panel, "Fullscreen", UserSettings.Fullscreen, v => { UserSettings.Fullscreen = v; UserSettings.ApplyDisplay(); }, 60f);
            ((RectTransform)fullscreen.transform).Place(UIKit.TL, UIKit.TL, new Vector2(1280f, -212f), new Vector2(60f, 60f));

            Label(panel, "Resolution", 800f, -300f);
            resolutions = UserSettings.Resolutions();
            var resNames = new string[resolutions.Count];
            int cur = 0;
            for (int i = 0; i < resNames.Length; i++)
            {
                resNames[i] = resolutions[i].x + " x " + resolutions[i].y;
                if (resolutions[i].x == UserSettings.ResWidth && resolutions[i].y == UserSettings.ResHeight) cur = i;
            }
            resCycler = UIKit.Cycler(panel, "Resolution", resNames, cur, i =>
            {
                UserSettings.ResWidth = resolutions[i].x; UserSettings.ResHeight = resolutions[i].y; UserSettings.ApplyDisplay();
            }, new Vector2(400f, 60f));
            ((RectTransform)resCycler.transform).Place(UIKit.TL, UIKit.TL, new Vector2(1060f, -290f), new Vector2(400f, 60f));

            Label(panel, "Quality", 800f, -380f);
            qualityCycler = UIKit.Cycler(panel, "Quality", UserSettings.QualityNames, UserSettings.Quality, i =>
            {
                UserSettings.Quality = i; UserSettings.ApplyGraphics(); qualityNote.text = UserSettings.QualityNotes[i];
            }, new Vector2(400f, 60f));
            ((RectTransform)qualityCycler.transform).Place(UIKit.TL, UIKit.TL, new Vector2(1060f, -370f), new Vector2(400f, 60f));
            qualityNote = UIKit.AddText(panel, UserSettings.QualityNotes[UserSettings.Quality], 24, Palette.Dim, TextAnchor.UpperLeft, false);
            qualityNote.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(800f, -440f), new Vector2(660f, 60f));

            Label(panel, "Vertical sync", 800f, -520f);
            vsync = UIKit.Toggle(panel, "VSync", UserSettings.VSync, v => { UserSettings.VSync = v; UserSettings.ApplyGraphics(); }, 60f);
            ((RectTransform)vsync.transform).Place(UIKit.TL, UIKit.TL, new Vector2(1280f, -510f), new Vector2(60f, 60f));

            // ---- server
            UIKit.Divider(panel, 1300f, UIKit.TC, new Vector2(0f, -620f));
            Header(panel, "Game server", 70f, -650f);
            Label(panel, "Address", 70f, -720f);
            serverInput = UIKit.TextInput(panel, "Server", UserSettings.ServerUrl, "ws://127.0.0.1:8765/ws", new Vector2(760f, 64f));
            ((RectTransform)serverInput.transform).Place(UIKit.TL, UIKit.TL, new Vector2(260f, -712f), new Vector2(760f, 64f));
            serverInput.onEndEdit.AddListener(s => { if (!string.IsNullOrEmpty(s)) UserSettings.ServerUrl = s; });

            Label(panel, "Offline practice", 1060f, -720f);
            offline = UIKit.Toggle(panel, "Offline", UserSettings.UseMock, v => UserSettings.UseMock = v, 60f);
            ((RectTransform)offline.transform).Place(UIKit.TL, UIKit.TL, new Vector2(1380f, -712f), new Vector2(60f, 60f));

            var close = UIKit.Button(panel, "Done", "Done", () => { UserSettings.Save(); if (CloseClicked != null) CloseClicked(); }, true, 38);
            ((RectTransform)close.transform).Place(UIKit.BC, UIKit.BC, new Vector2(0f, 26f), new Vector2(360f, 84f));
        }

        void Header(Transform parent, string text, float x, float y)
        {
            var t = UIKit.AddText(parent, text, 38, Palette.Gold, TextAnchor.MiddleLeft, true).Shadow();
            t.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(x, y), new Vector2(600f, 54f));
        }

        void Label(Transform parent, string text, float x, float y)
        {
            var t = UIKit.AddText(parent, text, 30, Palette.Cream, TextAnchor.MiddleLeft, false).Shadow(1.5f);
            t.rectTransform.Place(UIKit.TL, UIKit.TL, new Vector2(x, y), new Vector2(380f, 50f));
        }

        UiSlider SliderRow(Transform parent, string label, float x, float y, float value, Action<float> changed)
        {
            Label(parent, label, x, y - 4f);
            var s = UIKit.Slider(parent, label, value, changed, new Vector2(380f, 40f));
            ((RectTransform)s.transform).Place(UIKit.TL, UIKit.TL, new Vector2(x + 290f, y + 6f), new Vector2(380f, 40f));
            return s;
        }

        protected override void OnOpen()
        {
            master.SetValue(UserSettings.Master, false);
            music.SetValue(UserSettings.Music, false);
            sfx.SetValue(UserSettings.Sfx, false);
            fullscreen.Set(UserSettings.Fullscreen, false);
            vsync.Set(UserSettings.VSync, false);
            offline.Set(UserSettings.UseMock, false);
            qualityCycler.SetIndex(UserSettings.Quality, false);
            qualityNote.text = UserSettings.QualityNotes[UserSettings.Quality];
            serverInput.text = UserSettings.ServerUrl;
            uiScaleText.text = Mathf.RoundToInt(UserSettings.UiScale * 100f) + "%";
        }

        public override bool OnEscape()
        {
            UserSettings.Save();
            if (CloseClicked != null) CloseClicked();
            return true;
        }
    }
}
