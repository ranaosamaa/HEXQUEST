using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Factory helpers: every menu and HUD element is built in code from the art in Resources/Art.</summary>
    public static class UIKit
    {
        static Font titleFont, bodyFont, bodyBold, fallback;

        static Font Fallback()
        {
            if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback;
        }

        public static Font TitleFont
        {
            get
            {
                if (titleFont == null) titleFont = Resources.Load<Font>("Fonts/CinzelDecorative-Bold");
                return titleFont != null ? titleFont : Fallback();
            }
        }

        public static Font BodyFont
        {
            get
            {
                if (bodyFont == null) bodyFont = Resources.Load<Font>("Fonts/Philosopher-Regular");
                return bodyFont != null ? bodyFont : Fallback();
            }
        }

        public static Font BodyBold
        {
            get
            {
                if (bodyBold == null) bodyBold = Resources.Load<Font>("Fonts/Philosopher-Bold");
                return bodyBold != null ? bodyBold : Fallback();
            }
        }

        /// <summary>Font for world-space text and numbers: Philosopher has unmistakable digits (the title font's "1" looks like "I").</summary>
        public static Font Font { get { return BodyBold; } }

        // ------------------------------------------------------------------ layout

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform StretchXY(this RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Band(this RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static readonly Vector2 TL = new Vector2(0f, 1f), TC = new Vector2(0.5f, 1f), TR = new Vector2(1f, 1f);
        public static readonly Vector2 ML = new Vector2(0f, 0.5f), MC = new Vector2(0.5f, 0.5f), MR = new Vector2(1f, 0.5f);
        public static readonly Vector2 BL = new Vector2(0f, 0f), BC = new Vector2(0.5f, 0f), BR = new Vector2(1f, 0f);

        // ------------------------------------------------------------------ images and text

        public static Image Img(RectTransform rt, string sprite, Color color, bool raycast = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Art.Get(sprite);
            img.color = color;
            img.type = (img.sprite != null && img.sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Icon(Transform parent, string icon, Color color, float size, Vector2 anchor, Vector2 pos)
        {
            var rt = NewRect("Icon", parent).Place(anchor, anchor, pos, new Vector2(size, size));
            return Img(rt, "icon_" + icon, color, false);
        }

        public static Text AddText(Transform parent, string content, int size, Color color,
                                   TextAnchor anchor = TextAnchor.MiddleCenter, bool title = false)
        {
            var rt = NewRect("Text", parent).Stretch();
            var t = rt.gameObject.AddComponent<Text>();
            t.font = title ? TitleFont : BodyBold;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        public static Text Shadow(this Text t, float dist = 2f)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0.04f, 0.03f, 0.08f, 0.85f);
            s.effectDistance = new Vector2(dist, -dist);
            return t;
        }

        // ------------------------------------------------------------------ panels and buttons

        public static RectTransform Panel(Transform parent, string name, string sprite = "ui_panel_dark")
        {
            var rt = NewRect(name, parent);
            Img(rt, sprite, Color.white);
            return rt;
        }

        public static RectTransform Divider(Transform parent, float width, Vector2 anchor, Vector2 pos)
        {
            var rt = NewRect("Divider", parent).Place(anchor, anchor, pos, new Vector2(width, width * 32f / 512f));
            Img(rt, "ui_divider", Palette.Gold, false);
            return rt;
        }

        public static UiButton Button(Transform parent, string name, string label, Action onClick,
                                      bool primary = false, int fontSize = 34, string icon = null)
        {
            var rt = NewRect(name, parent);
            var img = Img(rt, primary ? "ui_button" : "ui_button_dark", Color.white);
            var b = rt.gameObject.AddComponent<UiButton>();
            b.Target = img;
            b.Clicked = onClick;
            var t = AddText(rt, label, fontSize, Palette.Cream, TextAnchor.MiddleCenter, true).Shadow();
            if (icon != null)
            {
                Icon(rt, icon, Palette.Cream, fontSize * 1.3f, ML, new Vector2(fontSize * 0.9f, 0f));
                t.rectTransform.offsetMin = new Vector2(fontSize * 1.1f, 0f);
            }
            b.Label = t;
            return b;
        }

        /// <summary>Round bronze-framed button with an icon. faceColor.a > 0 paints a coloured face behind it.</summary>
        public static UiButton RoundButton(Transform parent, string name, string icon, Action onClick, float size,
                                           Color faceColor, string label = null)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var frame = Img(rt, "ui_round", Color.white);
            var b = rt.gameObject.AddComponent<UiButton>();
            b.Target = frame;
            b.Clicked = onClick;
            if (faceColor.a > 0f)
            {
                var face = NewRect("Face", rt).Stretch(size * 0.07f);
                Img(face, "ui_round_face", faceColor, false);
            }
            float iconSize = label == null ? size * 0.52f : size * 0.40f;
            Vector2 iconPos = label == null ? Vector2.zero : new Vector2(0f, size * 0.10f);
            var ic = NewRect("Icon", rt).Place(MC, MC, iconPos, new Vector2(iconSize, iconSize));
            Img(ic, "icon_" + icon, Palette.Cream, false);
            if (label != null)
            {
                var t = AddText(rt, label, Mathf.RoundToInt(size * 0.17f), Palette.Cream, TextAnchor.LowerCenter, true).Shadow(1.5f);
                t.rectTransform.offsetMin = new Vector2(0f, size * 0.12f);
                b.Label = t;
            }
            return b;
        }

        public static UiSlider Slider(Transform parent, string name, float value, Action<float> changed, Vector2 size)
        {
            var track = NewRect(name, parent);
            track.sizeDelta = size;
            Img(track, "ui_slider_track", Color.white);
            var fill = NewRect("Fill", track);
            Img(fill, "ui_slider_fill", Color.white, false);
            var knob = NewRect("Knob", track);
            knob.sizeDelta = new Vector2(size.y * 1.35f, size.y * 1.35f);
            Img(knob, "ui_knob", Color.white, false);
            var s = track.gameObject.AddComponent<UiSlider>();
            s.Track = track; s.Fill = fill; s.Knob = knob;
            s.Changed = changed;
            s.SetValue(value, false);
            return s;
        }

        public static UiToggle Toggle(Transform parent, string name, bool value, Action<bool> changed, float size = 64f)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var box = Img(rt, "ui_chip", Color.white);
            var check = NewRect("Check", rt).Stretch(size * 0.16f);
            var ci = Img(check, "icon_check", Palette.Gold, false);
            var btn = rt.gameObject.AddComponent<UiButton>();
            btn.Target = box;
            var tg = rt.gameObject.AddComponent<UiToggle>();
            tg.Check = ci;
            tg.Changed = changed;
            tg.Set(value, false);
            btn.Clicked = () => tg.Set(!tg.Value, true);
            return tg;
        }

        public static UiCycler Cycler(Transform parent, string name, string[] options, int index, Action<int> changed, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            var bg = Img(rt, "ui_chip", Color.white, false);
            var cyc = rt.gameObject.AddComponent<UiCycler>();
            cyc.Changed = changed;
            cyc.ValueText = AddText(rt, "", Mathf.RoundToInt(size.y * 0.46f), Palette.Cream, TextAnchor.MiddleCenter, false).Shadow();
            cyc.ValueText.rectTransform.StretchXY(size.y, 0f, size.y, 0f);
            var left = Button(rt, "Prev", "<", () => cyc.Step(-1), false, Mathf.RoundToInt(size.y * 0.5f));
            ((RectTransform)left.transform).Place(ML, ML, Vector2.zero, new Vector2(size.y, size.y));
            var right = Button(rt, "Next", ">", () => cyc.Step(1), false, Mathf.RoundToInt(size.y * 0.5f));
            ((RectTransform)right.transform).Place(MR, MR, Vector2.zero, new Vector2(size.y, size.y));
            cyc.SetOptions(options, index);
            return cyc;
        }

        public static InputField TextInput(Transform parent, string name, string text, string placeholder, Vector2 size)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            var bg = Img(rt, "ui_chip", Color.white);
            var input = rt.gameObject.AddComponent<InputField>();
            var t = AddText(rt, "", Mathf.RoundToInt(size.y * 0.46f), Palette.Cream, TextAnchor.MiddleLeft, false);
            t.rectTransform.StretchXY(18f, 4f, 18f, 4f);
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = AddText(rt, placeholder, Mathf.RoundToInt(size.y * 0.46f), Palette.Dim, TextAnchor.MiddleLeft, false);
            ph.rectTransform.StretchXY(18f, 4f, 18f, 4f);
            ph.fontStyle = FontStyle.Italic;
            input.targetGraphic = bg;
            input.textComponent = t;
            input.placeholder = ph;
            input.text = text;
            input.caretColor = Palette.Gold;
            input.selectionColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.35f);
            return input;
        }
    }
}
