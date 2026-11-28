using UnityEngine;

namespace HexQuest
{
    /// <summary>A player's hero: stone pedestal with a rune ring, armoured knight and a banner in the player's colour.</summary>
    public class LeaderView : MonoBehaviour
    {
        SpriteRenderer pedestal, glow, bodyShadow, body, pole, flag;
        Transform bob;
        Color color;
        Vector3 start, target;
        float moveT = 1f, phase;
        bool myTurn, snap = true;

        SpriteRenderer Make(Transform parent, string objName, string sprite, float x, float y, float scale, int order)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Get(sprite);
            sr.sortingOrder = order;
            return sr;
        }

        public void Init(int playerId)
        {
            name = "Hero " + Palette.NameForPlayer(playerId);
            color = Palette.ForPlayer(playerId);
            var root = new GameObject("Bob").transform;
            root.SetParent(transform, false);
            bob = root;
            pedestal = Make(transform, "Pedestal", "pedestal", 0f, 0.06f, 0.62f, 0);
            glow = Make(transform, "RuneGlow", "pedestal_glow", 0f, 0.06f, 0.62f, 0);
            glow.color = Palette.WithAlpha(color, 0.5f);
            body = Make(bob, "Body", "hero_body", -0.06f, -0.10f, 0.60f, 0);
            pole = Make(bob, "Pole", "hero_pole", 0.20f, -0.10f, 0.60f, 0);
            flag = Make(bob, "Flag", "hero_flag", 0.20f, -0.10f, 0.60f, 0);
            flag.color = color;
            ApplyOrder();
        }

        void ApplyOrder()
        {
            int row = Mathf.RoundToInt(-transform.position.y / 1.5f + 24f) * 8;
            int b = 203 + row;
            pedestal.sortingOrder = b;
            glow.sortingOrder = b + 1;
            body.sortingOrder = b + 2;
            pole.sortingOrder = b + 2;
            flag.sortingOrder = b + 3;
        }

        public void SetTurn(bool isMyTurn) { myTurn = isMyTurn; }

        public void SetTarget(Vector3 worldPos, bool instant)
        {
            if (instant || snap)
            {
                transform.position = worldPos;
                target = worldPos;
                moveT = 1f;
                snap = false;
                ApplyOrder();
                return;
            }
            if ((worldPos - target).sqrMagnitude < 0.0001f) return;
            start = transform.position;
            target = worldPos;
            moveT = 0f;
        }

        void Update()
        {
            phase += Time.unscaledDeltaTime;
            float hop = 0f;
            if (moveT < 1f)
            {
                moveT = Mathf.Min(1f, moveT + Time.unscaledDeltaTime / 0.42f);
                float e = moveT * moveT * (3f - 2f * moveT);
                transform.position = Vector3.Lerp(start, target, e);
                hop = Mathf.Sin(moveT * Mathf.PI) * 0.28f;
                ApplyOrder();
            }
            float idle = Mathf.Sin(phase * 2.2f) * 0.012f;
            bob.localPosition = new Vector3(0f, hop + idle, 0f);
            float a = myTurn ? Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Sin(phase * 4f)) : 0.45f;
            glow.color = Palette.WithAlpha(color, a);
        }
    }
}
