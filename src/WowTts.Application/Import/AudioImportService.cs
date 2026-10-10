using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Import;

// One ready-made audio file for a part of a quest. ReadAsync loads it only when it's imported,
// so a pack of thousands of files isn't held in memory.
public sealed record ImportedAudio(int QuestId, QuestPart Part, Func<Task<byte[]>> ReadAsync);

// Found: files in the pack. Imported: parts that had no audio. Replaced: parts whose audio was overwritten.
// KeptExisting: parts that already had audio, left alone. UnknownQuest and NoText: files that don't fit a quest here.
public sealed record AudioImportResult(int Found, int Imported, int Replaced, int KeptExisting, int UnknownQuest, int NoText);

// Brings ready-made quest audio (e.g. from the AI VoiceOver addon) into the audio folder, with a performance that
// says where it came from. Parts that already have audio keep it, unless replace is set.
public sealed class AudioImportService(
    IQuestRepository quests, IAudioStore audioStore, IPerformanceRepository performances, ITimingsRepository timings)
{
    public async Task<AudioImportResult> ImportAsync(
        IReadOnlyCollection<ImportedAudio> files, VoicePerformance performance, bool replace)
    {
        var questsById = (await quests.GetAllAsync()).ToDictionary(quest => quest.Id);
        var saved = audioStore.ListAll();
        var importedParts = new List<(AudioKey, VoicePerformance)>();
        int imported = 0, replaced = 0, keptExisting = 0, unknownQuest = 0, noText = 0;

        foreach (ImportedAudio file in files)
        {
            if (!questsById.TryGetValue(file.QuestId, out Quest? quest))
            {
                unknownQuest++;
                continue;
            }

            if (!quest.HasText(file.Part))
            {
                noText++;
                continue;
            }

            var key = new AudioKey(file.QuestId, file.Part);
            bool exists = saved.Contains(key);
            if (exists && !replace)
            {
                keptExisting++;
                continue;
            }

            await audioStore.SaveAsync(file.QuestId, file.Part, await file.ReadAsync());
            importedParts.Add((key, performance));
            if (exists)
                replaced++;
            else
                imported++;
        }

        await performances.SaveManyAsync(importedParts);
        // Timings belonged to the audio that was there before.
        await timings.DeleteManyAsync(importedParts.Select(item => item.Item1).ToList());

        return new AudioImportResult(files.Count, imported, replaced, keptExisting, unknownQuest, noText);
    }
}
