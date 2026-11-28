using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexQuest
{
    /// <summary>
    /// Persistent player settings (PlayerPrefs) and the code that applies them:
    /// audio volumes, fullscreen/resolution, anti-aliasing and render scale (the "resolution" quality
    /// presets), vsync and UI scale, plus the last lobby configuration.
    /// </summary>
    public static class UserSettings
    {
        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };
        static readonly int[] QualityMsaa = { 0, 2, 4, 8 };
        static readonly float[] QualityScale = { 0.85f, 1.0f, 1.0f, 1.25f };
        public static readonly string[] QualityNotes =
        {
            "No anti-aliasing, 85% render scale. Fastest.",
            "2x anti-aliasing, native resolution.",
            "4x anti-aliasing, native resolution. Recommended.",
            "8x anti-aliasing, 125% supersampling. Sharpest, needs a strong GPU.",
        };

        public static event Action Changed;

        // ------------------------------------------------------------------ simple values

        static float GetF(string k, float d) { return PlayerPrefs.GetFloat("hq_" + k, d); }
        static int GetI(string k, int d) { return PlayerPrefs.GetInt("hq_" + k, d); }
        static string GetS(string k, string d) { return PlayerPrefs.GetString("hq_" + k, d); }
        static void Set(string k, float v) { PlayerPrefs.SetFloat("hq_" + k, v); }
        static void Set(string k, int v) { PlayerPrefs.SetInt("hq_" + k, v); }
        static void Set(string k, string v) { PlayerPrefs.SetString("hq_" + k, v); }

        public static float Master { get { return GetF("master", 0.8f); } set { Set("master", Mathf.Clamp01(value)); AudioManager.RefreshVolumes(); Raise(); } }
        public static float Music  { get { return GetF("music", 0.6f); }  set { Set("music", Mathf.Clamp01(value)); AudioManager.RefreshVolumes(); Raise(); } }
        public static float Sfx    { get { return GetF("sfx", 0.9f); }    set { Set("sfx", Mathf.Clamp01(value)); Raise(); } }

        public static bool Fullscreen { get { return GetI("fullscreen", Screen.fullScreen ? 1 : 0) == 1; } set { Set("fullscreen", value ? 1 : 0); Raise(); } }
        public static int ResWidth  { get { return GetI("resw", Screen.width); }  set { Set("resw", value); } }
        public static int ResHeight { get { return GetI("resh", Screen.height); } set { Set("resh", value); } }
        public static int Quality   { get { return Mathf.Clamp(GetI("quality", 2), 0, 3); } set { Set("quality", Mathf.Clamp(value, 0, 3)); Raise(); } }
        public static bool VSync    { get { return GetI("vsync", 1) == 1; } set { Set("vsync", value ? 1 : 0); Raise(); } }
        public static float UiScale { get { return Mathf.Clamp(GetF("uiscale", 1f), 0.8f, 1.3f); } set { Set("uiscale", Mathf.Clamp(value, 0.8f, 1.3f)); Raise(); } }

        public static string ServerUrl { get { return GetS("server", "ws://127.0.0.1:8765/ws"); } set { Set("server", value.Trim()); Raise(); } }
        public static bool UseMock     { get { return GetI("mock", 0) == 1; } set { Set("mock", value ? 1 : 0); Raise(); } }

        static void Raise() { if (Changed != null) Changed(); }
        public static void Save() { PlayerPrefs.Save(); }

        // ------------------------------------------------------------------ lobby memory

        public static MatchConfig LoadMatchConfig()
        {
            var cfg = new MatchConfig();
            cfg.player_count = Mathf.Clamp(GetI("lobby_players", 3), 2, 6);
            cfg.board_radius = GetI("lobby_radius", 4) >= 5 ? 5 : 4;
            cfg.rounds = GetI("lobby_rounds", 40);
            cfg.map_seed = GetI("lobby_seed", 12345);
            var csv = GetS("lobby_agents", "planner,greedy,random,greedy,random");
            cfg.agents = new List<string>(csv.Split(','));
            while (cfg.agents.Count < 5) cfg.agents.Add("greedy");
            return cfg;
        }

        public static void SaveMatchConfig(MatchConfig cfg)
        {
            Set("lobby_players", cfg.player_count);
            Set("lobby_radius", cfg.board_radius);
            Set("lobby_rounds", cfg.rounds);
            Set("lobby_seed", cfg.map_seed);
            Set("lobby_agents", string.Join(",", cfg.agents.ToArray()));
            Save();
        }

        // ------------------------------------------------------------------ resolutions

        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (r.width >= 1024 && !list.Contains(v)) list.Add(v);
            }
            var cur = new Vector2Int(Screen.width, Screen.height);
            if (!list.Contains(cur)) list.Add(cur);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        // ------------------------------------------------------------------ applying

        public static void ApplyGraphics()
        {
            int q = Quality;
            QualitySettings.antiAliasing = QualityMsaa[q];
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 144;
            // Universal RP keeps MSAA / render scale on the pipeline asset; set them by reflection so the
            // project also compiles on the built-in pipeline. NOTE: in the Editor this edits the URP asset.
            SetPipelineProperty("msaaSampleCount", QualityMsaa[q] == 0 ? 1 : QualityMsaa[q]);
            SetPipelineProperty("renderScale", QualityScale[q]);
        }

        public static void ApplyDisplay()
        {
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = ResWidth, h = ResHeight;
            if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode)
                Screen.SetResolution(w, h, mode);
        }

        static void SetPipelineProperty(string name, object value)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null) return;
            try
            {
                var p = rp.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite) p.SetValue(rp, Convert.ChangeType(value, p.PropertyType), null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("HexQuest: could not set " + name + ": " + e.Message);
            }
        }
    }
}
