using WowTts.Application.Abstractions;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;

namespace WowTts.Application.Maps;

// A continent or zone for the world map, with how many quest givers stand in it.
// RectOnParent is where a zone sits on its continent's map, in percent. Null for continents.
public sealed record MapZoneDto(
    int Id,
    string Name,
    MapZoneType Type,
    int? ParentId,
    MapRect? RectOnParent,
    int QuestCount,
    int Parts,
    int PartsWithAudio);

public sealed class MapService(IGameDataRepository gameData, IQuestRepository quests, IAudioStore audioStore)
{
    public async Task<IReadOnlyList<MapZoneDto>> GetZonesAsync()
    {
        var zones = await gameData.GetMapZonesAsync();
        var all = await quests.GetAllAsync();
        var saved = audioStore.ListAll();
        var byId = zones.ToDictionary(zone => zone.Id);

        // Count every quest once, in the zone where its giver stands.
        var counts = new Dictionary<int, (int Quests, int Parts, int WithAudio)>();
        foreach (Quest quest in all)
        {
            if (quest.GiverLocation is not WorldLocation location || MapZone.FindZone(zones, location) is not MapZone zone)
                continue;

            var parts = Enum.GetValues<QuestPart>().Where(quest.HasText).ToList();
            int withAudio = parts.Count(part => saved.Contains(new AudioKey(quest.Id, part)));

            foreach (int id in new[] { zone.Id, zone.ParentId ?? 0 }.Where(id => id != 0))
            {
                var current = counts.GetValueOrDefault(id);
                counts[id] = (current.Quests + 1, current.Parts + parts.Count, current.WithAudio + withAudio);
            }
        }

        return zones
            .OrderBy(zone => zone.Type)
            .ThenBy(zone => zone.Name)
            .Select(zone =>
            {
                MapRect? rect = zone.ParentId is int parentId
                    && byId.TryGetValue(parentId, out MapZone? parent)
                    && parent.Type == MapZoneType.Continent
                    ? Round(parent.ToMapRect(zone))
                    : null;
                var count = counts.GetValueOrDefault(zone.Id);
                return new MapZoneDto(zone.Id, zone.Name, zone.Type, zone.ParentId, rect,
                    count.Quests, count.Parts, count.WithAudio);
            })
            .ToList();
    }

    private static MapRect Round(MapRect rect) => new(
        Math.Round(rect.X, 2), Math.Round(rect.Y, 2), Math.Round(rect.Width, 2), Math.Round(rect.Height, 2));
}
