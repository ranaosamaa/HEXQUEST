using UnityEngine;

namespace HexQuest
{
    /// <summary>A player's leader: cyan pedestal + coloured token, like the units in the reference UI.</summary>
    public class LeaderView : MonoBehaviour
    {
        Vector3 target;
        bool snap = true;

        public void Init(int playerId, Font font)
        {
            name = "Leader " + (playerId + 1);
            var hex = ProceduralArt.HexMesh(0.55f);
            ProceduralArt.MakePart(transform, "PedestalSide", hex, Palette.Darken(Palette.Pedestal, 0.6f), 6, new Vector3(0f, -0.08f, 0f), out _);
            ProceduralArt.MakePart(transform, "PedestalTop", hex, Palette.Pedestal, 7, Vector3.zero, out _);
            ProceduralArt.MakePart(transform, "TokenRim", ProceduralArt.CircleMesh(0.27f), Color.white, 8, new Vector3(0f, 0.12f, 0f), out _);
            ProceduralArt.MakePart(transform, "Token", ProceduralArt.CircleMesh(0.22f), Palette.ForPlayer(playerId), 9, new Vector3(0f, 0.12f, 0f), out _);
            ProceduralArt.MakeText(transform, "Number", font, (playerId + 1).ToString(), 0.05f, Color.white, 10, new Vector3(0f, 0.12f, 0f));
        }

        public void SetTarget(Vector3 worldPos, bool instant)
        {
            target = worldPos;
            if (instant || snap) { transform.position = worldPos; snap = false; }
        }

        void Update()
        {
            // Cosmetic only: slides toward the position the server already confirmed.
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }
}
