using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Layered dusk landscape with mouse parallax and drifting embers, shown behind the menus.</summary>
    public class TitleBackground : MonoBehaviour
    {
        RectTransform root, layerHolder;
        readonly List<RectTransform> layers = new List<RectTransform>();
        readonly float[] depth = { 0f, 8f, 20f, 38f };
        readonly List<RectTransform> embers = new List<RectTransform>();
        readonly List<Vector3> emberData = new List<Vector3>();   // x speed, y sway phase, z sway amount
        Image dim;
        float dimGoal, dimShown;
        Vector2 mouse;
        float t;

        public void Build(Transform canvasRoot)
        {
            root = UIKit.NewRect("TitleBackground", canvasRoot).Stretch();
            layerHolder = UIKit.NewRect("Layers", root).Stretch();
            string[] names = { "title_sky", "title_far", "title_hills", "title_fore" };
            for (int i = 0; i < names.Length; i++)
            {
                // The holder is moved by the parallax; the AspectRatioFitter owns the child's size/position.
                var holder = UIKit.NewRect(names[i] + "Holder", layerHolder).Stretch();
                holder.localScale = Vector3.one * 1.07f;
                var rt = UIKit.NewRect(names[i], holder);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1920f, 1080f);
                UIKit.Img(rt, names[i], Color.white, false);
                var fit = rt.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = 2560f / 1440f;
                layers.Add(holder);
            }

            var ember = Art.Get("fx_soft");
            var rng = new System.Random(5);
            for (int i = 0; i < 34; i++)
            {
                var e = UIKit.NewRect("Ember", root);
                float size = 8f + (float)rng.NextDouble() * 18f;
                e.sizeDelta = new Vector2(size, size);
                e.anchorMin = e.anchorMax = new Vector2(0f, 0f);
                var img = e.gameObject.AddComponent<Image>();
                img.sprite = ember;
                img.raycastTarget = false;
                img.color = new Color(1f, 0.62f + (float)rng.NextDouble() * 0.2f, 0.3f, 0.35f + (float)rng.NextDouble() * 0.4f);
                e.anchoredPosition = new Vector2((float)rng.NextDouble() * 1920f, (float)rng.NextDouble() * 1080f);
                embers.Add(e);
                emberData.Add(new Vector3(18f + (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 6.28f, 14f + (float)rng.NextDouble() * 30f));
            }

            var dimRt = UIKit.NewRect("Dim", root).Stretch();
            dim = UIKit.Img(dimRt, "ui_vignette", new Color(1f, 1f, 1f, 0.9f), false);
            dim.type = Image.Type.Simple;
            var shade = UIKit.NewRect("Shade", root).Stretch();
            var shadeImg = shade.gameObject.AddComponent<Image>();
            shadeImg.color = new Color(0.02f, 0.03f, 0.09f, 0f);
            shadeImg.raycastTarget = false;
            shadeRef = shadeImg;
            root.gameObject.SetActive(false);
        }

        Image shadeRef;

        public void Show(bool visible) { root.gameObject.SetActive(visible); }
        public void SetDim(float amount) { dimGoal = amount; }

        void Update()
        {
            if (root == null || !root.gameObject.activeInHierarchy) return;
            t += Time.unscaledDeltaTime;
            Vector2 m = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f,
                                    Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f) * 2f;
            mouse = Vector2.Lerp(mouse, m, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            for (int i = 0; i < layers.Count; i++)
                layers[i].anchoredPosition = new Vector2(-mouse.x * depth[i], -mouse.y * depth[i] * 0.5f + Mathf.Sin(t * 0.3f + i) * 2f * i);

            float w = ((RectTransform)root.parent).rect.width, h = ((RectTransform)root.parent).rect.height;
            for (int i = 0; i < embers.Count; i++)
            {
                var d = emberData[i];
                var p = embers[i].anchoredPosition;
                p.y += d.x * Time.unscaledDeltaTime;
                p.x += Mathf.Sin(t * 0.8f + d.y) * d.z * Time.unscaledDeltaTime;
                if (p.y > h + 30f) { p.y = -30f; p.x = Random.value * w; }
                embers[i].anchoredPosition = p;
            }

            dimShown = Mathf.Lerp(dimShown, dimGoal, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            shadeRef.color = new Color(0.02f, 0.03f, 0.09f, dimShown);
        }
    }
}
