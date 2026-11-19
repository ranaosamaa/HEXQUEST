using UnityEngine;

namespace HexQuest
{
    /// <summary>One place for every colour, so restyling the game is a one-file job.</summary>
    public static class Palette
    {
        static Color C(string html) { ColorUtility.TryParseHtmlString(html, out var c); return c; }

        public static readonly Color Water    = C("#16255F");
        public static readonly Color Land     = C("#C6DBCF");
        public static readonly Color Blocked  = C("#33436E");
        public static readonly Color Panel    = C("#14174A");
        public static readonly Color Silver   = C("#B4BCCB");
        public static readonly Color Gold     = C("#F5B83D");
        public static readonly Color GoldDark = C("#C98A1B");
        public static readonly Color Purple   = C("#7A3FE0");
        public static readonly Color Pedestal = C("#4FC3F0");
        public static readonly Color Green    = C("#59C23A");
        public static readonly Color Danger   = C("#FF8A8A");

        public static readonly Color[] Players =
        {
            C("#8A5BE0"), C("#E8645C"), C("#F2DC4B"), C("#4F66E8"), C("#F0A24A"), C("#3CC3C0"),
        };

        public static Color ForPlayer(int id) => Players[Mathf.Abs(id) % Players.Length];
        public static Color Darken(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
    }
}
