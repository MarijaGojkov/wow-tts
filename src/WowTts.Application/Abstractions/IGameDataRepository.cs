using WowTts.Domain.Maps;

namespace WowTts.Application.Abstractions;

// Data from the game client: the world map's continents and zones, and the names of quest log headers.
public interface IGameDataRepository
{
    Task<IReadOnlyList<MapZone>> GetMapZonesAsync();

    // Quest log header names by ID: zones and dungeons (positive IDs) and categories (negative IDs).
    Task<IReadOnlyDictionary<int, string>> GetCategoryNamesAsync();

    // Replaces all game data with a fresh import.
    Task ReplaceAsync(IReadOnlyCollection<MapZone> zones, IReadOnlyDictionary<int, string> categoryNames);
}
