namespace WowTts.Domain.Maps;

public enum MapZoneType
{
    Continent,
    Zone
}

// A position on a map image, in percent: X from the left (west) edge, Y from the top (north) edge.
public readonly record struct MapPoint(double X, double Y);

// A rectangle on a map, in percent of that map's width and height.
public readonly record struct MapRect(double X, double Y, double Width, double Height);

// One map in the game's world map: a continent or a zone (cities count as zones).
// MinX..MaxX and MinY..MaxY is the part of the world the map image shows, from the game's UiMapAssignment table.
public sealed record MapZone(
    int Id,
    string Name,
    MapZoneType Type,
    int? ParentId,
    int MapId,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY)
{
    public bool Contains(WorldLocation location) =>
        location.MapId == MapId
        && location.X >= MinX && location.X <= MaxX
        && location.Y >= MinY && location.Y <= MaxY;

    // Where a world position appears on this map, the way the game converts it.
    // North (high X) is at the top and west (high Y) is on the left.
    public MapPoint ToMapPoint(WorldLocation location) => new(
        (MaxY - location.Y) / (MaxY - MinY) * 100,
        (MaxX - location.X) / (MaxX - MinX) * 100);

    // Where another map (e.g. a zone) sits on this map (e.g. its continent).
    public MapRect ToMapRect(MapZone inner)
    {
        MapPoint topLeft = ToMapPoint(new WorldLocation(MapId, inner.MaxX, inner.MaxY));
        MapPoint bottomRight = ToMapPoint(new WorldLocation(MapId, inner.MinX, inner.MinY));
        return new MapRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    public double WorldArea => (MaxX - MinX) * (MaxY - MinY);

    // The zone a position belongs to. Cities lie inside their zone's rectangle, so the smallest match wins.
    public static MapZone? FindZone(IEnumerable<MapZone> zones, WorldLocation location) =>
        zones.Where(zone => zone.Type == MapZoneType.Zone && zone.Contains(location))
             .MinBy(zone => zone.WorldArea);
}
