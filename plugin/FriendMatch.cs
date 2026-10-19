using System;
using System.Text.RegularExpressions;

namespace HsFriendSearch
{
    /// <summary>
    /// The friend search's match rule. A direct hit is the query anywhere in a name (case and spaces
    /// ignored), names starting with it first; a query of 4+ letters may also be off by length/3
    /// letters from a whole name, one of its words or a word's start (the hsbg.cards rule, ported
    /// from HsbgCardLookup's SearchEngine.FuzzyNameScore). Typo hits always rank below direct ones.
    /// Unity-free, so tests/ runs it outside the game.
    /// </summary>
    internal static class FriendMatch
    {
        public const int None = 0;
        private const int Starts = 3000, Contains = 2000, Typo = 1000;

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex WordSplit = new Regex(@"[\s\-,']+", RegexOptions.Compiled);

        public static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? "" : Whitespace.Replace(s.ToLowerInvariant(), "");

        public static int TypoLimit(int queryLength) => queryLength / 3;

        public static bool IsTypo(int score) => score > None && score < Contains;

        /// <summary>
        /// The best score of a normalized query against the player's names: Starts or Contains
        /// for a direct hit, Typo minus the letters off for a typo hit, None for no match.
        /// <paramref name="direct"/> are matched directly only (the full "Name#1234" tag), <paramref name="names"/>
        /// directly and with typos.
        /// </summary>
        public static int Score(string query, string[] names, string[] direct)
        {
            if (query.Length == 0) return None;
            int best = None;
            foreach (var name in names) best = Math.Max(best, DirectScore(query, name));
            foreach (var name in direct) best = Math.Max(best, DirectScore(query, name));
            if (best != None || query.Length < 4) return best;

            int limit = TypoLimit(query.Length);
            int fewest = int.MaxValue;
            foreach (var name in names)
                if (!string.IsNullOrEmpty(name)) fewest = Math.Min(fewest, TypoDistance(query, name, limit));
            return fewest <= limit ? Typo - fewest : None;
        }

        private static int DirectScore(string query, string name)
        {
            string n = Normalize(name);
            if (n.StartsWith(query, StringComparison.Ordinal)) return Starts;
            return n.IndexOf(query, StringComparison.Ordinal) >= 0 ? Contains : None;
        }

        /// <summary>The fewest letters the query is off from the whole name, one of its words or a word's start.</summary>
        private static int TypoDistance(string query, string name, int limit)
        {
            int q = query.Length;
            int best = int.MaxValue;
            string whole = Normalize(name);
            if (Math.Abs(whole.Length - q) <= limit + 2) best = Levenshtein(query, whole);
            if (best <= limit) return best;

            foreach (var word in WordSplit.Split(name.ToLowerInvariant()))
            {
                if (word.Length == 0) continue;
                if (Math.Abs(word.Length - q) <= limit + 2) best = Math.Min(best, Levenshtein(query, word));
                if (word.Length > q)
                {
                    int kMax = Math.Min(q + 2, word.Length);
                    for (int k = Math.Max(3, q - 1); k <= kMax; k++)
                        best = Math.Min(best, Levenshtein(query, word.Substring(0, k)));
                }
            }
            return best;
        }

        private static int[] s_prev = new int[32], s_row = new int[32];

        /// <summary>Edit distance (insert, delete, replace); two reused rows, so a keystroke allocates nothing here.</summary>
        public static int Levenshtein(string a, string b)
        {
            if (s_prev.Length <= b.Length)
            {
                s_prev = new int[b.Length + 1];
                s_row = new int[b.Length + 1];
            }
            int[] prev = s_prev, row = s_row;
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    row[j] = a[i - 1] == b[j - 1]
                        ? prev[j - 1]
                        : 1 + Math.Min(prev[j], Math.Min(row[j - 1], prev[j - 1]));
                var swap = prev; prev = row; row = swap;
            }
            return prev[b.Length];
        }
    }
}
