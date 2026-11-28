using System.Collections;
using UnityEngine;

namespace HexQuest
{
    /// <summary>Short-lived world effects: floating numbers, rings, sparks, slashes, coin bursts.</summary>
    public class FxManager : MonoBehaviour
    {
        Font font;

        public void Init(Font f) { font = f; }

        SpriteRenderer NewSprite(string sprite, Vector3 pos, Color color, float scale, int order)
        {
            var go = new GameObject("fx_" + sprite);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get(sprite);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public void FloatText(Vector3 pos, string text, Color color, float size = 1f)
        {
            StartCoroutine(RunText(pos, text, color, size));
        }

        IEnumerator RunText(Vector3 pos, string text, Color color, float size)
        {
            var tm = ProceduralArt.MakeText(transform, "float", font, text, 0.11f * size, color, 900, Vector3.zero);
            var shade = ProceduralArt.MakeText(transform, "floatShade", font, text, 0.11f * size, new Color(0f, 0f, 0f, 0.8f), 899, Vector3.zero);
            float t = 0f;
            while (t < 1.4f)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 1.4f;
                float rise = 0.15f + 0.9f * (1f - (1f - k) * (1f - k));
                float pop = k < 0.12f ? Mathf.Lerp(0.6f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((k - 0.12f) / 0.2f));
                var p = pos + new Vector3(0f, rise, 0f);
                tm.transform.position = p;
                shade.transform.position = p + new Vector3(0.025f, -0.025f, 0f);
                tm.transform.localScale = shade.transform.localScale = Vector3.one * pop;
                float a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                tm.color = new Color(color.r, color.g, color.b, a);
                shade.color = new Color(0f, 0f, 0f, 0.8f * a);
                yield return null;
            }
            Destroy(tm.gameObject);
            Destroy(shade.gameObject);
        }

        public void Ring(Vector3 pos, Color color, float from = 0.5f, float to = 2.4f, float dur = 0.65f)
        {
            StartCoroutine(RunSprite("fx_ring", pos, color, from, to, dur, 0f, Vector3.zero));
        }

        public void Spark(Vector3 pos, Color color, float scale = 1.4f)
        {
            StartCoroutine(RunSprite("fx_spark", pos, color, scale * 0.4f, scale, 0.55f, 120f, Vector3.zero));
            StartCoroutine(RunSprite("fx_soft", pos, Palette.WithAlpha(color, 0.8f), scale * 0.8f, scale * 2f, 0.5f, 0f, Vector3.zero));
        }

        public void Slash(Vector3 pos, Color color)
        {
            StartCoroutine(RunSprite("fx_slash", pos, color, 0.9f, 2.6f, 0.38f, -60f, Vector3.zero));
            StartCoroutine(RunSprite("fx_spark", pos, Color.white, 0.5f, 1.8f, 0.3f, 0f, Vector3.zero));
        }

        public void CoinBurst(Vector3 pos, int count = 7)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(35f, 145f) * Mathf.Deg2Rad;
                var vel = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * Random.Range(1.2f, 2.4f);
                StartCoroutine(RunCoin(pos, vel));
            }
        }

        IEnumerator RunCoin(Vector3 pos, Vector3 vel)
        {
            var sr = NewSprite("coin", pos, Color.white, 0.32f, 880);
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                vel.y -= 5.5f * Time.unscaledDeltaTime;
                sr.transform.position += vel * Time.unscaledDeltaTime;
                sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1.4f - t / 0.9f * 1.4f + 0.4f));
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        IEnumerator RunSprite(string sprite, Vector3 pos, Color color, float s0, float s1, float dur, float spin, Vector3 drift)
        {
            var sr = NewSprite(sprite, pos, color, s0, 870);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - k) * (1f - k);
                float s = Mathf.Lerp(s0, s1, e);
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.transform.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
                sr.transform.position = pos + drift * k;
                sr.color = new Color(color.r, color.g, color.b, color.a * (1f - k * k));
                yield return null;
            }
            Destroy(sr.gameObject);
        }
    }
}
