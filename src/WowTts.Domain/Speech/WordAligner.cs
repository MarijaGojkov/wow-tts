using System.Text.RegularExpressions;

namespace WowTts.Domain.Speech;

// Gives each word of a line's text a time, from the words a speech service spoke or heard in its audio.
// The heard words can differ from the text: Whisper mishears names ("Kaltunk" as "Coltunk"), and audio made
// elsewhere may say a little more or less. So both word lists are lined up like a diff, pairing words that are
// equal or nearly so. Paired words take the heard times; the words between them are spread by their length.
public static partial class WordAligner
{
    // Roughly how long a character takes to say, for words after the last one that was heard.
    private const int MsPerCharacter = 65;

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"[^\p{L}\p{N}]")]
    private static partial Regex NotLetterOrDigit();

    private sealed record TextWord(int Offset, int Length, string Key);

    public static (IReadOnlyList<WordTiming> Words, double Match) Align(string text, IReadOnlyList<HeardWord> heard)
    {
        var words = WordPattern().Matches(text).Select(m => new TextWord(m.Index, m.Length, Normalize(m.Value))).ToList();
        var heardWords = heard.Where(h => Normalize(h.Text).Length > 0).ToList();
        if (words.Count == 0 || heardWords.Count == 0)
            return ([], 0);

        int?[] pairedWith = Pair(words, heardWords.Select(h => Normalize(h.Text)).ToList());
        int paired = pairedWith.Count(j => j is not null);

        int speechEnd = heardWords.Max(h => h.EndMs);
        var starts = new int[words.Count];
        var ends = new int[words.Count];

        int i = 0;
        while (i < words.Count)
        {
            if (pairedWith[i] is int j)
            {
                starts[i] = heardWords[j].StartMs;
                ends[i] = heardWords[j].EndMs;
                i++;
                continue;
            }

            // A run of unpaired words: spread them between the paired words around them, by length.
            int runEnd = i;
            while (runEnd < words.Count && pairedWith[runEnd] is null)
                runEnd++;

            int from = i > 0 ? ends[i - 1] : 0;
            int characters = words.Skip(i).Take(runEnd - i).Sum(word => word.Length);
            int to = runEnd < words.Count
                ? heardWords[pairedWith[runEnd]!.Value].StartMs
                : Math.Max(speechEnd, from + characters * MsPerCharacter);

            int position = from;
            for (int k = i; k < runEnd; k++)
            {
                int length = to <= from ? 0 : (int)Math.Round((double)(to - from) * words[k].Length / characters);
                starts[k] = position;
                ends[k] = position + length;
                position += length;
            }

            i = runEnd;
        }

        // Keep the starts in order, in case the source reported an overlap.
        for (int k = 1; k < words.Count; k++)
            starts[k] = Math.Max(starts[k], starts[k - 1]);

        var timings = words.Select((word, k) => new WordTiming(word.Offset, word.Length, starts[k], Math.Max(ends[k], starts[k]))).ToList();
        return (timings, Math.Round((double)paired / words.Count, 3));
    }

    // Lines the two word lists up with the fewest edits (like a diff): equal words cost nothing, similar words
    // a little, and anything else counts as a change. Returns, for each text word, the heard word it pairs with.
    private static int?[] Pair(List<TextWord> words, List<string> heard)
    {
        int n = words.Count, m = heard.Count;
        var cost = new double[n + 1, m + 1];
        for (int a = 0; a <= n; a++) cost[a, 0] = a;
        for (int b = 0; b <= m; b++) cost[0, b] = b;

        for (int a = 1; a <= n; a++)
        for (int b = 1; b <= m; b++)
        {
            cost[a, b] = Math.Min(
                cost[a - 1, b - 1] + Substitution(words[a - 1].Key, heard[b - 1]),
                Math.Min(cost[a - 1, b] + 1, cost[a, b - 1] + 1));
        }

        var pairedWith = new int?[n];
        int x = n, y = m;
        while (x > 0 && y > 0)
        {
            double substitution = Substitution(words[x - 1].Key, heard[y - 1]);
            if (cost[x, y] == cost[x - 1, y - 1] + substitution)
            {
                if (substitution < 1)
                    pairedWith[x - 1] = y - 1;
                x--;
                y--;
            }
            else if (cost[x, y] == cost[x - 1, y] + 1)
                x--;
            else
                y--;
        }

        return pairedWith;
    }

    // 0 for the same word, 0.5 for a near miss such as a misheard name, 1 for a different word.
    private static double Substitution(string word, string heard)
    {
        if (word == heard)
            return 0;

        int longest = Math.Max(word.Length, heard.Length);
        return longest >= 4 && 1.0 - (double)EditDistance(word, heard) / longest >= 0.6 ? 0.5 : 1;
    }

    private static int EditDistance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    private static string Normalize(string word) => NotLetterOrDigit().Replace(word.ToLowerInvariant(), "");
}
