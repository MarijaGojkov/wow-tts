using Microsoft.EntityFrameworkCore;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Persistence;

public sealed class SqliteTimingsRepository(WowTtsDbContext db) : ITimingsRepository
{
    public async Task<IReadOnlyDictionary<QuestPart, AudioTimings>> GetForQuestAsync(int questId) =>
        await db.AudioTimings.AsNoTracking()
            .Where(t => t.QuestId == questId)
            .ToDictionaryAsync(t => t.Part, t => t.ToDomain());

    public async Task<IReadOnlySet<AudioKey>> ListAllAsync()
    {
        var keys = await db.AudioTimings.AsNoTracking().Select(t => new { t.QuestId, t.Part }).ToListAsync();
        return keys.Select(k => new AudioKey(k.QuestId, k.Part)).ToHashSet();
    }

    public async Task SaveAsync(int questId, QuestPart part, AudioTimings timings)
    {
        var row = await db.AudioTimings.FirstOrDefaultAsync(t => t.QuestId == questId && t.Part == part);
        if (row is null)
        {
            row = new AudioTimingsEntity { QuestId = questId, Part = part };
            db.AudioTimings.Add(row);
        }

        row.CopyFrom(timings);
        await db.SaveChangesAsync();

        // A timings run saves thousands of parts through one context; don't keep them all tracked.
        db.Entry(row).State = EntityState.Detached;
    }

    public Task DeleteAsync(int questId, QuestPart part) =>
        db.AudioTimings.Where(t => t.QuestId == questId && t.Part == part).ExecuteDeleteAsync();

    public async Task DeleteManyAsync(IReadOnlyCollection<AudioKey> parts)
    {
        if (parts.Count == 0)
            return;

        var keys = parts.ToHashSet();
        var questIds = keys.Select(key => key.QuestId).Distinct().ToList();
        var rows = await db.AudioTimings.Where(t => questIds.Contains(t.QuestId)).ToListAsync();
        db.AudioTimings.RemoveRange(rows.Where(row => keys.Contains(new AudioKey(row.QuestId, row.Part))));
        await db.SaveChangesAsync();
    }
}
