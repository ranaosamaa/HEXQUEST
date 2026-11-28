using UnityEngine;

namespace HexQuest
{
    /// <summary>One place for every colour. Restyling the game is a one-file job.</summary>
    public static class Palette
    {
        static Color C(string html) { Color c; ColorUtility.TryParseHtmlString(html, out c); return c; }

        public static readonly Color Water    = C("#123058");
        public static readonly Color Cream    = C("#F6E7C1");
        public static readonly Color Gold     = C("#F5C451");
        public static readonly Color GoldDark = C("#B8832A");
        public static readonly Color Ink      = C("#3B2A14");   // text on parchment
        public static readonly Color Panel    = C("#141C33");
        public static readonly Color Dim      = C("#B7A98B");
        public static readonly Color Danger   = C("#FF8A7A");
        public static readonly Color Good     = C("#9BE38C");
        public static readonly Color Purple   = C("#8A4FE0");

        public static readonly Color[] Players =
        {
            C("#4A86E8"), C("#E0524A"), C("#F2B63C"), C("#3FBF7A"), C("#A468E8"), C("#35C2C9"),
        };
        public static readonly string[] PlayerNames = { "Azure", "Crimson", "Amber", "Emerald", "Violet", "Teal" };

        public static Color ForPlayer(int id) { return Players[Mathf.Abs(id) % Players.Length]; }
        public static string NameForPlayer(int id) { return PlayerNames[Mathf.Abs(id) % PlayerNames.Length]; }
        public static Color Darken(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a); }
        public static Color Lighten(Color c, float t) { return Color.Lerp(c, Color.white, t); }
        public static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
        public static string Hex(Color c) { return ColorUtility.ToHtmlStringRGB(c); }
    }
}
