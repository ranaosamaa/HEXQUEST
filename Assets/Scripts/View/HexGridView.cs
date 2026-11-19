using System.Collections.Generic;
using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// Owns every CellView and LeaderView. Logical data lives in StateMessage;
    /// this class only mirrors it onto GameObjects (proposal section 10).
    /// </summary>
    public class HexGridView : MonoBehaviour
    {
        public const float HexSize = 1f;

        readonly Dictionary<HexCoord, CellView> cells = new Dictionary<HexCoord, CellView>();
        readonly Dictionary<int, LeaderView> leaders = new Dictionary<int, LeaderView>();
        Font font;

        public bool IsBuilt => cells.Count > 0;
        public Bounds WorldBounds { get; private set; }

        public void Build(StateMessage s, Font uiFont)
        {
            Clear();
            font = uiFont;
            Bounds b = default;
            bool first = true;
            foreach (var c in s.cells)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<CellView>();
                view.Init(c.coord, font);
                cells[c.coord] = view;

                Vector3 p = c.coord.ToWorld(HexSize);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
            b.Expand(new Vector3(2f, 2f, 0f));
            WorldBounds = b;
        }

        public void Clear()
        {
            foreach (var kv in cells) if (kv.Value) Destroy(kv.Value.gameObject);
            foreach (var kv in leaders) if (kv.Value) Destroy(kv.Value.gameObject);
            cells.Clear();
            leaders.Clear();
        }

        public void Refresh(StateMessage s, bool instant)
        {
            foreach (var c in s.cells)
                if (cells.TryGetValue(c.coord, out var view))
                    view.Refresh(c, ColorFor(c));

            foreach (var p in s.players)
            {
                if (!leaders.TryGetValue(p.player_id, out var lv))
                {
                    var go = new GameObject();
                    go.transform.SetParent(transform, false);
                    lv = go.AddComponent<LeaderView>();
                    lv.Init(p.player_id, font);
                    leaders[p.player_id] = lv;
                }
                lv.gameObject.SetActive(p.active);
                lv.SetTarget(WorldPosition(p.leader), instant);
            }
        }

        static Color ColorFor(CellData c)
        {
            if (c.status == CellStatus.Blocked) return Palette.Blocked;
            return c.owner >= 0 ? Palette.ForPlayer(c.owner) : Palette.Land;
        }

        public Vector3 WorldPosition(HexCoord c) => (Vector3)c.ToWorld(HexSize);

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
            if (cells.TryGetValue(c, out var view)) view.SetHighlight(h);
        }
    }
}
