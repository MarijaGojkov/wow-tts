using System.Text.Json;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Import;

// Reads the hand-written test quests in data/sample-quests.json.
public static class SampleQuestReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<Quest>> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var records = await JsonSerializer.DeserializeAsync<List<SampleQuest>>(stream, JsonOptions) ?? [];

        return records.Select(record => new Quest(
            record.Id, record.Title, record.NpcName, record.NpcRace, record.NpcGender,
            record.AcceptText, record.ProgressText, record.CompleteText, QuestSource.Sample)).ToList();
    }

    // One quest as it's written in the JSON file.
    private sealed record SampleQuest(
        int Id,
        string Title,
        string NpcName,
        string NpcRace,
        string NpcGender,
        string? AcceptText,
        string? ProgressText,
        string? CompleteText);
}
