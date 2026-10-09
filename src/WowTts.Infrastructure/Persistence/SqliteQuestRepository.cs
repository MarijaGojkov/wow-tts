using Microsoft.EntityFrameworkCore;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Persistence;

public sealed class SqliteQuestRepository(WowTtsDbContext db) : IQuestRepository
{
    public async Task<IReadOnlyList<Quest>> GetAllAsync()
    {
        var rows = await db.Quests.AsNoTracking().OrderBy(q => q.Id).ToListAsync();
        return rows.Select(row => row.ToDomain()).ToList();
    }

    public async Task<Quest?> GetByIdAsync(int id)
    {
        var row = await db.Quests.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
        return row?.ToDomain();
    }

    public async Task SaveAsync(IReadOnlyCollection<Quest> quests)
    {
        if (quests.Count == 0)
            return;

        var ids = quests.Select(q => q.Id).ToList();
        var existing = await db.Quests.Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id);

        foreach (Quest quest in quests)
        {
            if (existing.TryGetValue(quest.Id, out QuestEntity? row))
            {
                row.CopyFrom(quest);
            }
            else
            {
                // Fill in the values (including the ID) before adding, so EF Core tracks it under the right key.
                row = new QuestEntity();
                row.CopyFrom(quest);
                db.Quests.Add(row);
            }
        }

        await db.SaveChangesAsync();
    }
}
