using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Tiny helpers so the HUD can be built entirely in code (no manual uGUI clicking).</summary>
    public static class UIKit
    {
        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchor to a single point of the parent, then set pivot, offset and size.</summary>
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

        /// <summary>Fill the full width, between two vertical fractions (0 = bottom, 1 = top).</summary>
        public static RectTransform Band(this RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image AddImage(this RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        public static Text AddText(Transform parent, string content, int size, Color color,
                                   TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = NewRect("Text", parent).Stretch();
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.06f, 0.2f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>The silver-framed navy panel from the reference screenshot. Returns the inner (content) rect.</summary>
        public static RectTransform MetalPanel(Transform parent, string name, out RectTransform outer)
        {
            outer = NewRect(name, parent);
            outer.AddImage(ProceduralArt.RoundedRect(), Palette.Silver);
            var inner = NewRect("Inner", outer).Stretch(6f);
            inner.AddImage(ProceduralArt.RoundedRect(), Palette.Panel);
            return inner;
        }

        public static Button RoundButton(Transform parent, string name, string label, int fontSize, Color fill)
        {
            var rt = NewRect(name, parent);
            rt.AddImage(ProceduralArt.Circle(), Palette.Silver);
            var face = NewRect("Face", rt).Stretch(5f);
            var faceImg = face.AddImage(ProceduralArt.Circle(), fill);
            AddText(rt, label, fontSize, Color.white);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = faceImg;
            return b;
        }
    }
}
