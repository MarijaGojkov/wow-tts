using WowTts.Application.Abstractions;
using WowTts.Domain.Maps;

namespace WowTts.Application.Import;

// Stores the world map and the quest log header names that the importer read from the game's client tables.
public sealed class GameDataImportService(IGameDataRepository gameData)
{
    public Task ImportAsync(IReadOnlyCollection<MapZone> zones, IReadOnlyDictionary<int, string> categoryNames) =>
        gameData.ReplaceAsync(zones, categoryNames);
}
