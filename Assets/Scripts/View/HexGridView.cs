using System.Collections.Generic;
using UnityEngine;

namespace HexQuest
{
    /// <summary>Owns every CellView and LeaderView. Logical data lives in StateMessage; this only mirrors it.</summary>
    public class HexGridView : MonoBehaviour
    {
        public const float HexSize = 1f;

        readonly Dictionary<HexCoord, CellView> cells = new Dictionary<HexCoord, CellView>();
        readonly Dictionary<int, LeaderView> leaders = new Dictionary<int, LeaderView>();
        Font font;

        public bool IsBuilt { get { return cells.Count > 0; } }
        public Bounds WorldBounds { get; private set; }

        /// <summary>Stable cosmetic seed: the part of the match id before the first '-'.</summary>
        public static int SaltFor(string matchId)
        {
            string key = matchId ?? "";
            int dash = key.IndexOf('-');
            if (dash > 0) key = key.Substring(0, dash);
            unchecked
            {
                int h = 17;
                foreach (char c in key) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        public void Build(StateMessage s, Font uiFont)
        {
            Clear();
            font = uiFont;
            int salt = SaltFor(s.match_id);
            Bounds b = default(Bounds);
            bool first = true;
            foreach (var c in s.cells)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<CellView>();
                view.Init(c.coord, salt, font);
                cells[c.coord] = view;

                Vector3 p = c.coord.ToWorld(HexSize);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
            b.Expand(new Vector3(2.4f, 2.8f, 0f));
            WorldBounds = b;
        }

        public void Clear()
        {
            foreach (var kv in cells) if (kv.Value != null) Destroy(kv.Value.gameObject);
            foreach (var kv in leaders) if (kv.Value != null) Destroy(kv.Value.gameObject);
            cells.Clear();
            leaders.Clear();
        }

        public void Refresh(StateMessage s, bool instant)
        {
            foreach (var c in s.cells)
            {
                CellView view;
                if (cells.TryGetValue(c.coord, out view)) view.Refresh(c);
            }

            foreach (var p in s.players)
            {
                LeaderView lv;
                if (!leaders.TryGetValue(p.player_id, out lv))
                {
                    var go = new GameObject();
                    go.transform.SetParent(transform, false);
                    lv = go.AddComponent<LeaderView>();
                    lv.Init(p.player_id);
                    leaders[p.player_id] = lv;
                }
                lv.gameObject.SetActive(p.active);
                lv.SetTurn(!s.game_over && s.active_player == p.player_id);
                lv.SetTarget(WorldPosition(p.leader), instant);
            }
        }

        public Vector3 WorldPosition(HexCoord c) { return (Vector3)c.ToWorld(HexSize); }

        public Vector3 LeaderPosition(int playerId, Vector3 fallback)
        {
            LeaderView lv;
            return leaders.TryGetValue(playerId, out lv) && lv != null ? lv.transform.position : fallback;
        }

        public bool TryPick(Vector2 world, out HexCoord coord)
        {
            coord = HexCoord.FromWorld(world, HexSize);
            return cells.ContainsKey(coord);
        }

        public void ClearHighlights()
        {
            foreach (var kv in cells) kv.Value.SetHighlight(Highlight.None);
        }

        public void SetHighlight(HexCoord c, Highlight h)
        {
            CellView view;
            if (cells.TryGetValue(c, out view)) view.SetHighlight(h);
        }
    }
}
