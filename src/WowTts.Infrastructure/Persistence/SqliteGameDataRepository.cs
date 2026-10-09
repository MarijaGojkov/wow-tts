using Microsoft.EntityFrameworkCore;
using WowTts.Application.Abstractions;
using WowTts.Domain.Maps;

namespace WowTts.Infrastructure.Persistence;

public sealed class SqliteGameDataRepository(WowTtsDbContext db) : IGameDataRepository
{
    public async Task<IReadOnlyList<MapZone>> GetMapZonesAsync()
    {
        var rows = await db.MapZones.AsNoTracking().ToListAsync();
        return rows.Select(row => row.ToDomain()).ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetCategoryNamesAsync() =>
        await db.QuestCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

    public async Task ReplaceAsync(IReadOnlyCollection<MapZone> zones, IReadOnlyDictionary<int, string> categoryNames)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.MapZones.ExecuteDeleteAsync();
        await db.QuestCategories.ExecuteDeleteAsync();

        db.MapZones.AddRange(zones.Select(MapZoneEntity.From));
        db.QuestCategories.AddRange(categoryNames.Select(pair => new QuestCategoryEntity { Id = pair.Key, Name = pair.Value }));
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
    }
}
