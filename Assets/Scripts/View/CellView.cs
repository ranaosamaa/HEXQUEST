using UnityEngine;

namespace HexQuest
{
    public enum Highlight { None, Legal, Attack, Selected }

    /// <summary>
    /// One hex tile, drawn from sprites. It decides nothing about the game: it shows whatever
    /// CellData it is given. Terrain and decorations are cosmetic and derived from the match id.
    /// Draw order: shadow 0, side 1, ground 2, ownership glaze 3, border 4, highlight 5-6,
    /// decorations 200+row, badges 600+row (rows further down the screen draw on top).
    /// </summary>
    public class CellView : MonoBehaviour
    {
        public HexCoord Coord { get; private set; }

        SpriteRenderer side, ground, tint, border, glow, select, decor, ore, rubble, shieldFace, shieldRim, coin;
        TextMesh garrisonText, garrisonShade, depositText;
        Highlight hl;
        float phase;
        Color glowColor = Color.white;

        public static int Hash(int q, int r, int salt)
        {
            unchecked
            {
                int h = salt * 73856093 ^ q * 19349663 ^ r * 83492791;
                h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }

        SpriteRenderer Make(string objName, string sprite, int order, float x, float y, float scale = 1f)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get(sprite);
            sr.sortingOrder = order;
            return sr;
        }

        public void Init(HexCoord coord, int salt, Font font)
        {
            Coord = coord;
            name = "Cell " + coord;
            transform.position = coord.ToWorld(HexGridView.HexSize);
            int row = (coord.r + 24) * 8;

            Make("Shadow", "hex_shadow", 0, 0.05f, -0.22f);
            side = Make("Side", "hex_side", 1, 0f, -0.156f);

            // ---- cosmetic terrain, deterministic per match
            int h = Hash(coord.q, coord.r, salt);
            int roll = h % 100;
            string groundName, decorName = null;
            float decorScale = 0.62f;
            if (roll < 40) groundName = "ground_grass_" + ((h / 100) % 3);
            else if (roll < 58) { groundName = "ground_forest"; decorName = ((h / 7) % 2 == 0) ? "pine_a" : "tree_b"; }
            else if (roll < 68) { groundName = "ground_sand"; if ((h / 13) % 3 == 0) { decorName = "ruins"; decorScale = 0.5f; } }
            else if (roll < 78) { groundName = "ground_rock"; decorName = "rocks"; decorScale = 0.5f; }
            else { groundName = "ground_grass_" + ((h / 100) % 3); if ((h / 11) % 3 == 0) decorName = ((h / 5) % 2 == 0) ? "tree_a" : "pine_b"; }

            ground = Make("Ground", groundName, 2, 0f, 0f);
            tint = Make("Glaze", "hex_fill", 3, 0f, 0f);
            border = Make("Border", "hex_ring", 4, 0f, 0f);
            glow = Make("Glow", "hex_glow", 5, 0f, 0f);
            select = Make("Select", "hex_select", 6, 0f, 0f);
            tint.enabled = border.enabled = glow.enabled = select.enabled = false;

            decor = decorName != null ? Make("Decor", decorName, 200 + row, -0.30f, 0.20f, decorScale) : null;
            ore = Make("Ore", "ore", 201 + row, 0.34f, 0.12f, 0.36f);
            rubble = Make("Rubble", "rubble", 205 + row, 0f, -0.05f, 0.62f);
            ore.enabled = false; rubble.enabled = false;

            shieldFace = Make("ShieldFace", "badge_face", 600 + row, 0f, -0.50f, 0.74f);
            shieldRim = Make("ShieldRim", "badge_rim", 601 + row, 0f, -0.50f, 0.74f);
            garrisonShade = ProceduralArt.MakeText(transform, "NumShade", font, "", 0.06f, new Color(0f, 0f, 0f, 0.7f), 602 + row, new Vector3(0.012f, -0.488f, 0f));
            garrisonText = ProceduralArt.MakeText(transform, "Num", font, "", 0.06f, Color.white, 603 + row, new Vector3(0f, -0.476f, 0f));
            coin = Make("Coin", "coin", 602 + row, 0.34f, -0.06f, 0.30f);
            depositText = ProceduralArt.MakeText(transform, "DepositNum", font, "", 0.034f, new Color(0.25f, 0.14f, 0.02f), 603 + row, new Vector3(0.34f, -0.075f, 0f));
            shieldFace.enabled = shieldRim.enabled = coin.enabled = false;
            garrisonText.GetComponent<MeshRenderer>().enabled = false;
            garrisonShade.GetComponent<MeshRenderer>().enabled = false;
            depositText.GetComponent<MeshRenderer>().enabled = false;
        }

        public void Refresh(CellData d)
        {
            bool normal = d.status == CellStatus.Normal;
            bool owned = normal && d.owner >= 0;
            var blockedTone = new Color(0.42f, 0.38f, 0.55f);
            ground.color = normal ? Color.white : blockedTone;
            side.color = normal ? Color.white : blockedTone;

            tint.enabled = border.enabled = owned;
            if (owned)
            {
                var pc = Palette.ForPlayer(d.owner);
                tint.color = Palette.WithAlpha(pc, 0.40f);
                border.color = Palette.WithAlpha(Palette.Lighten(pc, 0.12f), 0.95f);
                shieldFace.color = Palette.Lighten(pc, 0.05f);
            }

            if (decor != null) decor.enabled = normal;
            rubble.enabled = !normal;
            ore.enabled = normal && d.deposit > 0;
            coin.enabled = ore.enabled;
            depositText.GetComponent<MeshRenderer>().enabled = ore.enabled;
            depositText.text = d.deposit.ToString();

            bool showShield = owned && d.garrison > 0;
            shieldFace.enabled = shieldRim.enabled = showShield;
            garrisonText.GetComponent<MeshRenderer>().enabled = showShield;
            garrisonShade.GetComponent<MeshRenderer>().enabled = showShield;
            garrisonText.text = garrisonShade.text = d.garrison.ToString();
        }

        public void SetHighlight(Highlight h)
        {
            hl = h;
            glow.enabled = h != Highlight.None;
            select.enabled = h == Highlight.Selected;
            switch (h)
            {
                case Highlight.Legal:    glowColor = new Color(1f, 0.96f, 0.62f); break;
                case Highlight.Attack:   glowColor = new Color(1f, 0.32f, 0.26f); break;
                case Highlight.Selected: glowColor = new Color(1f, 1f, 1f); select.color = Palette.Gold; break;
            }
            phase = 0f;
        }

        void Update()
        {
            if (hl == Highlight.None) return;
            phase += Time.unscaledDeltaTime * 4.2f;
            float k = 0.5f + 0.5f * Mathf.Sin(phase);
            float a = hl == Highlight.Selected ? 0.35f : Mathf.Lerp(0.30f, 0.85f, k);
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, a);
            if (hl == Highlight.Selected)
            {
                float s = 1f + 0.015f * Mathf.Sin(phase * 1.3f);
                select.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
