using Microsoft.EntityFrameworkCore;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Persistence;

public sealed class SqliteNpcVoiceRepository(WowTtsDbContext db) : INpcVoiceRepository
{
    public async Task<NpcVoice?> GetAsync(string npcName, string npcGender)
    {
        var row = await db.NpcVoices.AsNoTracking()
            .FirstOrDefaultAsync(v => v.NpcName == npcName && v.NpcGender == npcGender);
        return row?.ToDomain();
    }

    public async Task SaveAsync(NpcVoice voice)
    {
        var row = await db.NpcVoices.FirstOrDefaultAsync(v => v.NpcName == voice.NpcName && v.NpcGender == voice.NpcGender);
        if (row is null)
        {
            row = new NpcVoiceEntity { NpcName = voice.NpcName, NpcGender = voice.NpcGender };
            db.NpcVoices.Add(row);
        }

        row.Voice = voice.Voice;
        row.Reason = voice.Reason;
        row.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public Task DeleteAsync(string npcName, string npcGender) =>
        db.NpcVoices.Where(v => v.NpcName == npcName && v.NpcGender == npcGender).ExecuteDeleteAsync();
}

public sealed class SqlitePerformanceRepository(WowTtsDbContext db) : IPerformanceRepository
{
    public async Task<IReadOnlyDictionary<QuestPart, VoicePerformance>> GetForQuestAsync(int questId) =>
        await db.AudioPerformances.AsNoTracking()
            .Where(p => p.QuestId == questId)
            .ToDictionaryAsync(p => p.Part, p => p.ToDomain());

    public async Task SaveAsync(int questId, QuestPart part, VoicePerformance performance)
    {
        await DeleteAsync(questId, part);
        db.AudioPerformances.Add(AudioPerformanceEntity.From(questId, part, performance));
        await db.SaveChangesAsync();
    }

    public Task DeleteAsync(int questId, QuestPart part) =>
        db.AudioPerformances.Where(p => p.QuestId == questId && p.Part == part).ExecuteDeleteAsync();

    // In one transaction, for imports of thousands of parts.
    public async Task SaveManyAsync(IReadOnlyCollection<(AudioKey Key, VoicePerformance Performance)> performances)
    {
        if (performances.Count == 0)
            return;

        var questIds = performances.Select(item => item.Key.QuestId).Distinct().ToList();
        var existing = await db.AudioPerformances
            .Where(p => questIds.Contains(p.QuestId))
            .ToDictionaryAsync(p => new AudioKey(p.QuestId, p.Part));

        foreach (var (key, performance) in performances)
        {
            if (existing.TryGetValue(key, out var row))
                row.CopyFrom(performance);
            else
                db.AudioPerformances.Add(AudioPerformanceEntity.From(key.QuestId, key.Part, performance));
        }

        await db.SaveChangesAsync();
    }
}
