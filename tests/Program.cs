using System;
using System.Linq;
using HsFriendSearch;

internal static class Program
{
    private static int s_failed;

    private static void Check(string what, bool ok)
    {
        if (!ok) s_failed++;
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + what);
    }

    private static int Score(string query, params string[] names) =>
        FriendMatch.Score(FriendMatch.Normalize(query), names, Array.Empty<string>());

    private static int Main()
    {
        Check("levenshtein kerigan/kerrigan = 1", FriendMatch.Levenshtein("kerigan", "kerrigan") == 1);
        Check("levenshtein rouge/rogue = 2", FriendMatch.Levenshtein("rouge", "rogue") == 2);
        Check("levenshtein empty = length", FriendMatch.Levenshtein("", "abc") == 3);
        Check("levenshtein longer than buffer", FriendMatch.Levenshtein(new string('a', 40), new string('a', 39) + "b") == 1);

        Check("typo budget: 4-5 letters 1, 6-8 2, 9 3",
            FriendMatch.TypoLimit(4) == 1 && FriendMatch.TypoLimit(5) == 1 && FriendMatch.TypoLimit(6) == 2
            && FriendMatch.TypoLimit(8) == 2 && FriendMatch.TypoLimit(9) == 3);

        int starts = Score("tant", "TantrumUA");
        int contains = Score("trum", "TantrumUA");
        Check("direct: case-insensitive start", starts > 0 && !FriendMatch.IsTypo(starts));
        Check("direct: anywhere", contains > 0 && !FriendMatch.IsTypo(contains));
        Check("direct: starts ranks above anywhere", starts > contains);
        Check("direct: spaces ignored", Score("john sm", "John Smith") > 0 && Score("johnsm", "John Smith") > 0);
        Check("direct: matches through the tag", FriendMatch.Score("#1234", new[] { "Tantrum" }, new[] { "Tantrum#1234" }) > 0);
        Check("tag is direct only, never typo",
            FriendMatch.Score("#1235", new[] { "Tantrum" }, new[] { "Tantrum#1234" }) == FriendMatch.None);

        int typo = Score("tantrm", "TantrumUA");
        Check("typo: start of the name with 1 letter off", FriendMatch.IsTypo(typo));
        Check("typo ranks below every direct hit", typo < contains);
        Check("typo: whole name", FriendMatch.IsTypo(Score("kerigan", "Kerrigan")));
        Check("typo: one word of a Real ID name", FriendMatch.IsTypo(Score("smitj", "John Smith")));
        // Plain Levenshtein, as hsbg.cards: a swap of two letters costs 2.
        Check("a swap is 2 off: refused at 5 letters", Score("smiht", "John Smith") == FriendMatch.None);
        Check("typo: fewer letters off ranks higher", Score("kerigan", "Kerrigan") > Score("kerrrgn", "Kerrigan"));

        Check("no typos under 4 letters", Score("tnt", "TantrumUA") == FriendMatch.None);
        Check("4 letters: 1 off allowed", FriendMatch.IsTypo(Score("tamt", "TantrumUA")));
        Check("4 letters: 2 off refused", Score("tbmt", "TantrumUA") == FriendMatch.None);
        Check("no typo match from the middle of a name", Score("trym", "TantrumUA") == FriendMatch.None);
        Check("unrelated name: no match", Score("kerrigan", "Thrall") == FriendMatch.None);
        Check("empty query: no score", Score("", "Thrall") == FriendMatch.None);

        string[] list = { "Zeratul", "Thrall", "Rexxar", "Rexar", "Jaina", "Tirion", "Rexxarbot" };
        var order = list
            .Select((name, index) => (name, index, score: Score("rexxar", name)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score).ThenBy(x => x.index)
            .Select(x => x.name).ToArray();
        Console.WriteLine("      rexxar -> " + string.Join(", ", order));
        Check("flat order: direct hits first, then typo", order.SequenceEqual(new[] { "Rexxar", "Rexxarbot", "Rexar" }));

        Console.WriteLine(s_failed == 0 ? "all passed" : s_failed + " failed");
        return s_failed == 0 ? 0 : 1;
    }
}
