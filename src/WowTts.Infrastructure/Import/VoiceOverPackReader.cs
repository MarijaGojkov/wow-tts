using System.Globalization;
using System.Text.RegularExpressions;
using WowTts.Application.Import;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Import;

// Reads the quest audio of an AI VoiceOver sound pack (https://github.com/mrthinger/wow-voiceover), such as the
// AI_VoiceOverData_Vanilla addon folder. Its quest files are generated\sounds\quests\<quest id>-<part>.mp3, and lines
// that change with the player's gender come twice, as m-<id>-<part>.mp3 and f-<id>-<part>.mp3.
// The pack doesn't voice progress text. A pack split into parts can be extracted into one folder.
public static partial class VoiceOverPackReader
{
    [GeneratedRegex(@"^(?:(?<gender>[mf])-)?(?<id>\d+)-(?<part>accept|progress|complete)\.mp3$", RegexOptions.IgnoreCase)]
    private static partial Regex QuestFileName();

    public static IReadOnlyList<ImportedAudio> Read(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"{folder} doesn't exist. Extract the AI VoiceOver sound pack there.");

        var questFolders = Directory.EnumerateDirectories(folder, "quests", SearchOption.AllDirectories)
            .Where(dir => Path.GetFileName(Path.GetDirectoryName(dir))?.Equals("sounds", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (questFolders.Count == 0)
            throw new DirectoryNotFoundException(
                $"There's no sounds\\quests folder in {folder}. Extract the AI_VoiceOverData_Vanilla addon there.");

        // Our text reads $G with its first (male) form, so the m- file is used when a line has both.
        var best = new Dictionary<(int QuestId, QuestPart Part), (int Rank, string Path)>();
        foreach (string file in questFolders.SelectMany(dir => Directory.EnumerateFiles(dir, "*.mp3")))
        {
            var match = QuestFileName().Match(Path.GetFileName(file));
            if (!match.Success)
                continue;

            int rank = match.Groups["gender"].Value.ToLowerInvariant() switch { "" => 0, "m" => 1, _ => 2 };
            var key = (int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture),
                       Enum.Parse<QuestPart>(match.Groups["part"].Value, ignoreCase: true));

            if (!best.TryGetValue(key, out var current) || rank < current.Rank)
                best[key] = (rank, file);
        }

        return best
            .OrderBy(pair => pair.Key.QuestId).ThenBy(pair => pair.Key.Part)
            .Select(pair => new ImportedAudio(pair.Key.QuestId, pair.Key.Part, () => File.ReadAllBytesAsync(pair.Value.Path)))
            .ToList();
    }
}
