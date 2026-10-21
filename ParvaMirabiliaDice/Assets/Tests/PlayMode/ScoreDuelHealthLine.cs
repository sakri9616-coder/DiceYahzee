using UnityEngine.UI;

namespace ParvaMirabilia.Quality.Tests
{
    /// <summary>
    /// The health line's format, restated here so the suites read a bar the way the player does.
    ///
    /// A bar prints one line — side, current health, then the drop that side is taking in red. The
    /// expectations used to describe two separate numerals (a forecast and a drain figure) parked at
    /// the right end of the track, which is what made a single bar look like it carried two
    /// different figures at once, and what forced the drop into a bolder, larger face. The line is
    /// now one string, so both the format and the figure are asserted from that string.
    /// </summary>
    internal static class ScoreDuelHealthLine
    {
        /// <summary>The red a drop is printed in, spelled the way the rich-text tag spells it.</summary>
        public const string Red = "<color=#C1272D>";
        /// <summary>The deep green a kept-reroll heal is printed in.</summary>
        public const string Green = "<color=#1B5B40>";

        /// <summary>What a bar must print for this side at this health with this heal and drop.</summary>
        public static string Expected(string side, int hp, int max, int damage, int heal = 0)
        {
            string line = side + " " + hp + "/" + max;
            if (heal > 0) line += " " + Green + "+" + heal + "</color>";
            return damage > 0 ? line + " " + Red + "−" + damage + "</color>" : line;
        }

        /// <summary>The drop a printed line carries, or 0 when it carries none.</summary>
        public static int Damage(Text label)
        {
            if (label == null || string.IsNullOrEmpty(label.text)) return 0;
            int open = label.text.IndexOf(Red, System.StringComparison.Ordinal);
            if (open < 0) return 0;
            int start = open + Red.Length;
            int end = label.text.IndexOf('<', start);
            int damage;
            return int.TryParse(label.text.Substring(start, (end < 0 ? label.text.Length : end) - start).TrimStart('−'), out damage)
                ? damage : -1;
        }

        /// <summary>The heal a printed line carries, or 0 when it carries none.</summary>
        public static int Heal(Text label)
        {
            if (label == null || string.IsNullOrEmpty(label.text)) return 0;
            int open = label.text.IndexOf(Green, System.StringComparison.Ordinal);
            if (open < 0) return 0;
            int start = open + Green.Length;
            int end = label.text.IndexOf('<', start);
            int heal;
            return int.TryParse(label.text.Substring(start, (end < 0 ? label.text.Length : end) - start).TrimStart('+'), out heal)
                ? heal : -1;
        }
    }
}
