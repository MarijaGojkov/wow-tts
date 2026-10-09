using WowTts.Domain.Maps;

namespace WowTts.Infrastructure.Persistence;

// One row of the MapZones table: a continent or zone of the world map.
public sealed class MapZoneEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public MapZoneType Type { get; set; }
    public int? ParentId { get; set; }
    public int MapId { get; set; }
    public double MinX { get; set; }
    public double MaxX { get; set; }
    public double MinY { get; set; }
    public double MaxY { get; set; }

    public MapZone ToDomain() => new(Id, Name, Type, ParentId, MapId, MinX, MaxX, MinY, MaxY);

    public static MapZoneEntity From(MapZone zone) => new()
    {
        Id = zone.Id,
        Name = zone.Name,
        Type = zone.Type,
        ParentId = zone.ParentId,
        MapId = zone.MapId,
        MinX = zone.MinX,
        MaxX = zone.MaxX,
        MinY = zone.MinY,
        MaxY = zone.MaxY,
    };
}

// One row of the QuestCategories table: the name of a quest log header.
public sealed class QuestCategoryEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
