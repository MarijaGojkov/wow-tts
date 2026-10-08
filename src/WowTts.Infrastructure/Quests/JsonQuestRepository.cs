using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Quests;

// Reads quests from a JSON file. The file is read on every call, so edits show up without restarting the API.
public sealed class JsonQuestRepository(IOptions<QuestDataOptions> options, IHostEnvironment environment)
    : IQuestRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _filePath =
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.DataFile));

    public async Task<IReadOnlyList<Quest>> GetAllAsync()
    {
        if (!File.Exists(_filePath))
            throw new InvalidOperationException($"The quest file wasn't found at {_filePath}.");

        await using var stream = File.OpenRead(_filePath);
        var records = await JsonSerializer.DeserializeAsync<List<QuestRecord>>(stream, JsonOptions) ?? [];

        return records.Select(ToQuest).ToList();
    }

    public async Task<Quest?> GetByIdAsync(int id)
    {
        var all = await GetAllAsync();
        return all.FirstOrDefault(quest => quest.Id == id);
    }

    private Quest ToQuest(QuestRecord record)
    {
        try
        {
            return new Quest(
                record.Id, record.Title, record.NpcName, record.NpcRace, record.NpcGender,
                record.AcceptText, record.ProgressText, record.CompleteText);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"The quest file {_filePath} has an invalid quest: {ex.Message}", ex);
        }
    }

    // One quest as it's stored in the JSON file. Kept separate from the Domain's Quest,
    // so the file format can change without touching the Domain.
    private sealed record QuestRecord(
        int Id,
        string Title,
        string NpcName,
        string NpcRace,
        string NpcGender,
        string AcceptText,
        string? ProgressText,
        string? CompleteText);
}
