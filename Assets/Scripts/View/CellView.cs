using UnityEngine;

namespace HexQuest
{
    public enum Highlight { None, Legal, Attack, Selected }

    /// <summary>
    /// Visual for ONE hex. Holds no game rules and never decides anything:
    /// it just draws whatever CellData it is given (server-authoritative).
    /// Layers (draw order): side 0 -> top 1 -> overlay 2 -> ring 3 -> badges 4/5.
    /// </summary>
    public class CellView : MonoBehaviour
    {
        public HexCoord Coord { get; private set; }

        Material topMat, sideMat, overlayMat;
        MeshRenderer ringRenderer;
        GameObject garrisonBadge, depositBadge, blockedMark;
        TextMesh garrisonText, depositText;

        public void Init(HexCoord coord, Font font)
        {
            Coord = coord;
            name = "Cell " + coord;
            transform.position = coord.ToWorld(HexGridView.HexSize);

            var hex = ProceduralArt.HexMesh(0.97f);
            ProceduralArt.MakePart(transform, "Side", hex, Palette.Land, 0, new Vector3(0f, -0.13f, 0f), out sideMat);
            ProceduralArt.MakePart(transform, "Top", hex, Palette.Land, 1, Vector3.zero, out topMat);
            ProceduralArt.MakePart(transform, "Overlay", hex, Color.clear, 2, Vector3.zero, out overlayMat);
            ringRenderer = ProceduralArt.MakePart(transform, "Ring", ProceduralArt.RingMesh(0.97f, 0.86f),
                                                  Palette.Gold, 3, Vector3.zero, out _);
            ringRenderer.enabled = false;

            garrisonBadge = MakeBadge("Garrison", new Vector3(0f, -0.40f, 0f), 0.24f, Palette.Panel, Color.white, font, out garrisonText);
            depositBadge = MakeBadge("Deposit", new Vector3(-0.48f, 0.26f, 0f), 0.19f, Palette.Gold, Palette.Panel, font, out depositText);

            blockedMark = ProceduralArt.MakeText(transform, "BlockedMark", font, "X", 0.12f,
                                                 new Color(1f, 1f, 1f, 0.5f), 5, Vector3.zero).gameObject;
        }

        GameObject MakeBadge(string badgeName, Vector3 pos, float radius, Color bg, Color fg, Font font, out TextMesh text)
        {
            ProceduralArt.MakePart(transform, badgeName, ProceduralArt.CircleMesh(radius), bg, 4, pos, out _);
            var root = transform.Find(badgeName).gameObject;
            text = ProceduralArt.MakeText(root.transform, "Label", font, "", 0.045f, fg, 5, Vector3.zero);
            return root;
        }

        public void Refresh(CellData d, Color topColor)
        {
            topMat.color = topColor;
            sideMat.color = Palette.Darken(topColor, 0.72f);

            bool normal = d.status == CellStatus.Normal;
            garrisonBadge.SetActive(normal && d.owner >= 0 && d.garrison > 0);
            garrisonText.text = d.garrison.ToString();
            depositBadge.SetActive(normal && d.deposit > 0);
            depositText.text = d.deposit.ToString();
            blockedMark.SetActive(!normal);
        }

        public void SetHighlight(Highlight h)
        {
            ringRenderer.enabled = h == Highlight.Selected;
            switch (h)
            {
                case Highlight.Legal:    overlayMat.color = new Color(1f, 1f, 1f, 0.45f); break;
                case Highlight.Attack:   overlayMat.color = new Color(1f, 0.15f, 0.15f, 0.45f); break;
                case Highlight.Selected: overlayMat.color = new Color(1f, 1f, 1f, 0.25f); break;
                default:                 overlayMat.color = Color.clear; break;
            }
        }
    }
}
