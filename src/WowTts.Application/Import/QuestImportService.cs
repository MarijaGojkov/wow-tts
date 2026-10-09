using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;

namespace WowTts.Application.Import;

// Brings quests from any source (cMaNGOS, samples, later the addon) into the quest store.
public sealed class QuestImportService(IQuestRepository quests, IAudioStore audioStore)
{
    public async Task<ImportResult> ImportAsync(IReadOnlyCollection<Quest> incoming)
    {
        var existing = (await quests.GetAllAsync()).ToDictionary(quest => quest.Id);
        var toSave = new List<Quest>();
        int added = 0, updated = 0, unchanged = 0, keptExisting = 0, audioDeleted = 0;

        foreach (Quest quest in incoming.DistinctBy(quest => quest.Id))
        {
            if (!existing.TryGetValue(quest.Id, out Quest? current))
            {
                toSave.Add(quest);
                added++;
                continue;
            }

            // Text captured in game beats cMaNGOS, which beats the samples.
            if (current.Source > quest.Source)
            {
                keptExisting++;
                continue;
            }

            if (HasSameContent(current, quest))
            {
                unchanged++;
                continue;
            }

            // Saved audio was made from the old text, so remove it. It's regenerated on the next request.
            foreach (QuestPart part in Enum.GetValues<QuestPart>())
            {
                if (current.GetText(part) != quest.GetText(part) && audioStore.Exists(quest.Id, part))
                {
                    audioStore.Delete(quest.Id, part);
                    audioDeleted++;
                }
            }

            toSave.Add(quest);
            updated++;
        }

        await quests.SaveAsync(toSave);

        return new ImportResult(incoming.Count, added, updated, unchanged, keptExisting, audioDeleted);
    }

    private static bool HasSameContent(Quest a, Quest b) =>
        a.Source == b.Source
        && a.Title == b.Title
        && a.NpcName == b.NpcName
        && a.NpcRace == b.NpcRace
        && a.NpcGender == b.NpcGender
        && a.AcceptText == b.AcceptText
        && a.ProgressText == b.ProgressText
        && a.CompleteText == b.CompleteText
        && a.Level == b.Level
        && a.MinLevel == b.MinLevel
        && a.Side == b.Side
        && a.CategoryId == b.CategoryId
        && a.GiverLocation == b.GiverLocation;
}
