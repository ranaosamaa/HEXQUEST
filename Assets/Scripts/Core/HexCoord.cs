using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexQuest
{
    /// <summary>
    /// Axial hex coordinate, pointy-top layout. Cube axis s is derived (s = -q - r).
    /// This is pure data/math: it knows nothing about GameObjects.
    /// </summary>
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public int q;
        public int r;

        static readonly float Sqrt3 = Mathf.Sqrt(3f);

        public static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1),
        };

        public HexCoord(int q, int r) { this.q = q; this.r = r; }

        public HexCoord Neighbor(int direction) => this + Directions[direction];

        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = a.q - b.q, dr = a.r - b.r;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
        }

        /// <summary>Centre of this hex in world space. +r goes DOWN the screen.</summary>
        public Vector2 ToWorld(float size = 1f)
            => new Vector2(size * Sqrt3 * (q + r * 0.5f), -size * 1.5f * r);

        /// <summary>Which hex contains this world point?</summary>
        public static HexCoord FromWorld(Vector2 p, float size = 1f)
        {
            float x = p.x / size;
            float y = -p.y / size;
            float fq = (Sqrt3 / 3f) * x - (1f / 3f) * y;
            float fr = (2f / 3f) * y;
            return Round(fq, fr);
        }

        static HexCoord Round(float fq, float fr)
        {
            float fs = -fq - fr;
            int rq = Mathf.RoundToInt(fq), rr = Mathf.RoundToInt(fr), rs = Mathf.RoundToInt(fs);
            float dq = Mathf.Abs(rq - fq), dr = Mathf.Abs(rr - fr), ds = Mathf.Abs(rs - fs);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;
            return new HexCoord(rq, rr);
        }

        /// <summary>All hexes within 'radius' of the origin. Radius 4 = 61 cells, radius 5 = 91 cells.</summary>
        public static List<HexCoord> InRadius(int radius)
        {
            var list = new List<HexCoord>();
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Mathf.Max(-radius, -q - radius);
                int r2 = Mathf.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++) list.Add(new HexCoord(q, r));
            }
            return list;
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.q + b.q, a.r + b.r);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.q - b.q, a.r - b.r);
        public static bool operator ==(HexCoord a, HexCoord b) => a.q == b.q && a.r == b.r;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        public bool Equals(HexCoord other) => this == other;
        public override bool Equals(object obj) => obj is HexCoord h && this == h;
        public override int GetHashCode() => (q * 397) ^ r;
        public override string ToString() => $"({q},{r})";
    }
}
