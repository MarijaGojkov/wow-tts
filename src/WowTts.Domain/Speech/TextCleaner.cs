using System.Text.RegularExpressions;

namespace WowTts.Domain.Speech;

// WoW quest text contains placeholders the game fills in for your character, e.g. "Greetings, $N."
// Audio is generated once for everyone, so the placeholders become generic words that fit any character.
public static partial class TextCleaner
{
    public const string PlayerName = "adventurer";   // $N
    public const string PlayerRace = "traveler";     // $R
    public const string PlayerClass = "hero";        // $C

    public static string Clean(string text)
    {
        // $Gmale:female; picks a word by the player's gender, e.g. "$Glad:lass;". We use the first form.
        string cleaned = GenderChoice().Replace(text, match => match.Groups["male"].Value.Trim());

        cleaned = Placeholder().Replace(cleaned, match => char.ToUpperInvariant(match.Value[1]) switch
        {
            'N' => PlayerName,
            'R' => PlayerRace,
            'C' => PlayerClass,
            'B' => "\n",   // $B is a line break in the quest window
            _ => match.Value
        });

        // Tidy the spacing: single spaces inside lines, at most one blank line between paragraphs.
        cleaned = SpacesAndTabs().Replace(cleaned, " ");
        cleaned = SpaceAroundNewlines().Replace(cleaned, "\n");
        cleaned = ManyNewlines().Replace(cleaned, "\n\n");

        return cleaned.Trim();
    }

    [GeneratedRegex(@"\$[Gg]\s*(?<male>[^:;]*):(?<female>[^;]*);")]
    private static partial Regex GenderChoice();

    [GeneratedRegex(@"\$[NnRrCcBb]")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex SpacesAndTabs();

    [GeneratedRegex(@" ?\n ?")]
    private static partial Regex SpaceAroundNewlines();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlines();
}
