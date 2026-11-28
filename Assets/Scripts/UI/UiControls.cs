using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Hover/press animated button with sounds. Works with any Image as its visual.</summary>
    public class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                            IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Action Clicked;
        public Action<bool> HoverChanged;
        public Image Target;
        public Text Label;
        public Color Tint = Color.white;
        public float HoverScale = 1.045f;
        public bool PlaySounds = true;
        public string Tip;

        bool hover, down, interactable = true;
        float scale = 1f;
        float shade = 0.88f;

        public bool Interactable
        {
            get { return interactable; }
            set
            {
                interactable = value;
                if (!value) { hover = false; down = false; }
                shade = Shade();
                Apply();
            }
        }

        float Shade() { return !interactable ? 0.45f : down ? 0.72f : hover ? 1f : 0.9f; }

        void Apply()
        {
            if (Target != null) Target.color = new Color(Tint.r * shade, Tint.g * shade, Tint.b * shade, Tint.a);
            if (Label != null)
            {
                var c = Label.color;
                Label.color = new Color(c.r, c.g, c.b, interactable ? 1f : 0.5f);
            }
        }

        void OnEnable() { scale = 1f; transform.localScale = Vector3.one; shade = Shade(); Apply(); }

        void Update()
        {
            float goal = !interactable ? 1f : down ? 0.96f : hover ? HoverScale : 1f;
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            scale = Mathf.Lerp(scale, goal, k);
            transform.localScale = new Vector3(scale, scale, 1f);
            shade = Mathf.Lerp(shade, Shade(), k);
            Apply();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!interactable) return;
            hover = true;
            if (PlaySounds) AudioManager.PlaySfx("ui_hover", 0.5f, 0.05f);
            if (HoverChanged != null) HoverChanged(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hover = false; down = false;
            if (HoverChanged != null) HoverChanged(false);
        }

        public void OnPointerDown(PointerEventData e) { if (interactable) down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            if (PlaySounds) AudioManager.PlaySfx("ui_click", 0.8f, 0.04f);
            if (Clicked != null) Clicked();
        }
    }

    /// <summary>Horizontal slider 0..1 (click or drag along the track).</summary>
    public class UiSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<float> Changed;
        public RectTransform Track, Fill, Knob;
        public float Padding = 14f;
        float value;

        public float Value { get { return value; } }

        public void SetValue(float v, bool notify)
        {
            value = Mathf.Clamp01(v);
            Layout();
            if (notify && Changed != null) Changed(value);
        }

        void Layout()
        {
            if (Track == null) return;
            float w = Track.rect.width - Padding * 2f;
            if (Fill != null)
            {
                Fill.anchorMin = new Vector2(0f, 0f);
                Fill.anchorMax = new Vector2(0f, 1f);
                Fill.pivot = new Vector2(0f, 0.5f);
                Fill.anchoredPosition = new Vector2(Padding * 0.5f, 0f);
                Fill.sizeDelta = new Vector2(Mathf.Max(8f, w * value + Padding * 0.5f), -Padding * 0.8f);
            }
            if (Knob != null)
            {
                Knob.anchorMin = new Vector2(0f, 0.5f);
                Knob.anchorMax = new Vector2(0f, 0.5f);
                Knob.anchoredPosition = new Vector2(Padding + w * value, 0f);
            }
        }

        void OnEnable() { Layout(); }

        void Handle(PointerEventData e)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Track, e.position, e.pressEventCamera, out local)) return;
            float x0 = Track.rect.xMin + Padding, x1 = Track.rect.xMax - Padding;
            SetValue(Mathf.InverseLerp(x0, x1, local.x), true);
        }

        public void OnPointerDown(PointerEventData e) { Handle(e); }
        public void OnDrag(PointerEventData e) { Handle(e); }
        public void OnPointerUp(PointerEventData e) { AudioManager.PlaySfx("ui_hover", 0.6f, 0.02f); }
    }

    /// <summary>On/off switch with a check mark.</summary>
    public class UiToggle : MonoBehaviour
    {
        public Action<bool> Changed;
        public Image Check;
        bool value;

        public bool Value { get { return value; } }

        public void Set(bool v, bool notify)
        {
            value = v;
            if (Check != null) Check.enabled = v;
            if (notify && Changed != null) Changed(v);
        }
    }

    /// <summary>[ < ]  value  [ > ] selector that cycles through options.</summary>
    public class UiCycler : MonoBehaviour
    {
        public Action<int> Changed;
        public Text ValueText;
        string[] options = new string[0];
        int index;

        public int Index { get { return index; } }

        public void SetOptions(string[] opts, int idx)
        {
            options = opts;
            index = Mathf.Clamp(idx, 0, Mathf.Max(0, opts.Length - 1));
            Refresh();
        }

        public void SetIndex(int idx, bool notify)
        {
            if (options.Length == 0) return;
            index = ((idx % options.Length) + options.Length) % options.Length;
            Refresh();
            if (notify && Changed != null) Changed(index);
        }

        public void Step(int delta) { SetIndex(index + delta, true); }

        void Refresh() { if (ValueText != null) ValueText.text = options.Length > 0 ? options[index] : "-"; }
    }
}
