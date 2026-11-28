using UnityEngine;

namespace HexQuest
{
    /// <summary>Endless drifting ocean: two seamless layers scrolling in different directions (cheap parallax, no shader).</summary>
    public class WaterView : MonoBehaviour
    {
        const int Half = 3;                 // 7 x 7 tiles per layer
        float tileWorld;
        Transform layerA, layerB;
        float t;

        void Awake()
        {
            var a = Art.Get("water_a");
            var b = Art.Get("water_b");
            float natural = a.rect.width / a.pixelsPerUnit;    // 10.24 world units
            float scale = 1.5f;
            tileWorld = natural * scale;
            layerA = BuildLayer("WaterA", a, -100, scale * 1.003f);
            layerB = BuildLayer("WaterB", b, -99, scale * 1.003f);
        }

        Transform BuildLayer(string layerName, Sprite sprite, int order, float scale)
        {
            var root = new GameObject(layerName).transform;
            root.SetParent(transform, false);
            for (int y = -Half; y <= Half; y++)
                for (int x = -Half; x <= Half; x++)
                {
                    var go = new GameObject("t");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(x * tileWorld, y * tileWorld, 0f);
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = order;
                }
            return root;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            layerA.localPosition = new Vector3(Mathf.Repeat(t * 0.22f, tileWorld), Mathf.Repeat(t * 0.07f, tileWorld), 0f);
            layerB.localPosition = new Vector3(-Mathf.Repeat(t * 0.34f, tileWorld), Mathf.Repeat(t * 0.11f, tileWorld) * -1f, 0f);
        }
    }
}
