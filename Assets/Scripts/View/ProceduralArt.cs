using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// Builds every mesh and sprite in code so the project needs ZERO imported art.
    /// Later you can replace these with real assets without touching game logic.
    /// </summary>
    public static class ProceduralArt
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static Sprite roundedRect, circle, hexUi;
        static Shader spriteShader;

        // ------------------------------------------------------------------ materials

        public static Material NewMaterial(Color color)
        {
            if (spriteShader == null) spriteShader = Shader.Find("Sprites/Default");
            return new Material(spriteShader) { color = color };
        }

        // ------------------------------------------------------------------ meshes (pointy-top)

        static Vector3 Corner(float size, int i)
        {
            float a = Mathf.Deg2Rad * (60f * i - 30f);
            return new Vector3(size * Mathf.Cos(a), size * Mathf.Sin(a), 0f);
        }

        static Mesh Store(string key, List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh { name = key };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            var colors = new Color[verts.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            m.colors = colors;
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }

        public static Mesh HexMesh(float size)
        {
            string key = "hex" + size;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < 6; i++) verts.Add(Corner(size, i));
            var tris = new List<int>();
            for (int i = 0; i < 6; i++) { tris.Add(0); tris.Add((i + 1) % 6 + 1); tris.Add(i + 1); }
            return Store(key, verts, tris);
        }

        public static Mesh RingMesh(float outer, float inner)
        {
            string key = "ring" + outer + "_" + inner;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3>();
            for (int i = 0; i < 6; i++) verts.Add(Corner(outer, i));
            for (int i = 0; i < 6; i++) verts.Add(Corner(inner, i));
            var tris = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int o = i, oNext = (i + 1) % 6, n = 6 + i, nNext = 6 + (i + 1) % 6;
                tris.Add(o); tris.Add(n); tris.Add(nNext);
                tris.Add(o); tris.Add(nNext); tris.Add(oNext);
            }
            return Store(key, verts, tris);
        }

        public static Mesh CircleMesh(float radius, int segments = 28)
        {
            string key = "circle" + radius + "_" + segments;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var verts = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                verts.Add(new Vector3(radius * Mathf.Cos(a), radius * Mathf.Sin(a), 0f));
            }
            var tris = new List<int>();
            for (int i = 0; i < segments; i++) { tris.Add(0); tris.Add((i + 1) % segments + 1); tris.Add(i + 1); }
            return Store(key, verts, tris);
        }

        // ------------------------------------------------------------------ world-space parts

        /// <summary>Creates a child object with a coloured mesh. 'order' = draw order (higher = on top).</summary>
        public static MeshRenderer MakePart(Transform parent, string name, Mesh mesh, Color color,
                                            int order, Vector3 localPos, out Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            material = NewMaterial(color);
            r.sharedMaterial = material;
            r.sortingOrder = order;
            return r;
        }

        public static TextMesh MakeText(Transform parent, string name, Font font, string text,
                                        float charSize, Color color, int order, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = order;
            return tm;
        }

        // ------------------------------------------------------------------ UI sprites

        public static Sprite RoundedRect()
        {
            if (roundedRect == null)
            {
                const int size = 64; const float radius = 16f; const float border = 22f;
                var tex = MakeTexture(size, size, (x, y) =>
                {
                    float half = size / 2f;
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude
                              + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    return Mathf.Clamp01(0.5f - d);
                });
                roundedRect = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            }
            return roundedRect;
        }

        public static Sprite Circle()
        {
            if (circle == null)
            {
                const int size = 128;
                var tex = MakeTexture(size, size, (x, y) =>
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude - (size / 2f - 1f);
                    return Mathf.Clamp01(0.5f - d);
                });
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
            return circle;
        }

        public static Sprite HexUi()
        {
            if (hexUi == null)
            {
                const int size = 128;
                const float R = 62f;
                float apothem = R * 0.8660254f;
                var tex = MakeTexture(size, size, (x, y) =>
                {
                    float px = x + 0.5f - size / 2f, py = y + 0.5f - size / 2f;
                    float d = Mathf.Max(Mathf.Abs(px),
                              Mathf.Max(Mathf.Abs(0.5f * px + 0.8660254f * py), Mathf.Abs(-0.5f * px + 0.8660254f * py))) - apothem;
                    return Mathf.Clamp01(0.5f - d);
                });
                hexUi = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
            return hexUi;
        }

        static Texture2D MakeTexture(int w, int h, Func<int, int, float> alpha)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(1f, 1f, 1f, alpha(x, y));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
