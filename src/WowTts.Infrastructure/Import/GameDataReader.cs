using System.Globalization;
using WowTts.Domain.Maps;

namespace WowTts.Infrastructure.Import;

public sealed record GameDataReadResult(IReadOnlyList<MapZone> Zones, IReadOnlyDictionary<int, string> CategoryNames);

// Reads the game client tables that wago.tools exports as CSV (https://wago.tools/db2):
//   AreaTable and QuestSort: the names of quest log headers (zones, dungeons and categories)
//   UiMap and UiMapAssignment: the world map's continents and zones, and the part of the world each map shows
public static class GameDataReader
{
    public static readonly string[] Tables = ["AreaTable", "QuestSort", "UiMap", "UiMapAssignment"];

    // The game's rectangle for the Eastern Kingdoms continent map (UiMap 1415) doesn't line up with its Classic map
    // art: drawn with it, quest givers land in the sea and zones sit too high. Measured by fitting all 1,979 Eastern
    // Kingdoms quest givers onto the land in the art: map positions scaled by 0.908 and moved 2.7% right and 10.2% down
    // put 98% of them on land, up from 79%. Kalimdor's rectangle already fits (98% on land), so it isn't corrected.
    private static readonly Dictionary<int, (double Scale, double ShiftX, double ShiftY)> MapArtCorrections = new()
    {
        [1415] = (0.908, 2.70, 10.20),
    };

    public static GameDataReadResult Read(string folder)
    {
        var areas = CsvFile.Read(Path.Combine(folder, "AreaTable.csv"));
        var questSorts = CsvFile.Read(Path.Combine(folder, "QuestSort.csv"));
        var uiMaps = CsvFile.Read(Path.Combine(folder, "UiMap.csv"));
        var assignments = CsvFile.Read(Path.Combine(folder, "UiMapAssignment.csv"));

        // Quest log headers: a quest's ZoneOrSort is an area ID when positive, or minus a QuestSort ID.
        var categoryNames = new Dictionary<int, string>();
        foreach (var row in areas)
            categoryNames[ToInt(row["ID"])] = row["AreaName_lang"];
        foreach (var row in questSorts)
            categoryNames[-ToInt(row["ID"])] = row["SortName_lang"];

        // The world rectangle each map shows. Region_0..2 are the minimum X, Y, Z and Region_3..5 the maximum.
        var regionByMap = assignments
            .GroupBy(row => ToInt(row["UiMapID"]))
            .ToDictionary(group => group.Key, group => group.OrderBy(row => ToInt(row["OrderIndex"])).First());

        var zones = new List<MapZone>();
        foreach (var row in uiMaps)
        {
            int id = ToInt(row["ID"]);
            int type = ToInt(row["Type"]);

            // System 0 is the normal world map (1 is the flight map). Type 2 is a continent, 3 a zone or city.
            if (ToInt(row["System"]) != 0 || type is not (2 or 3) || !regionByMap.TryGetValue(id, out var region))
                continue;

            int parentId = ToInt(row["ParentUiMapID"]);
            var zone = new MapZone(
                id,
                row["Name_lang"],
                type == 2 ? MapZoneType.Continent : MapZoneType.Zone,
                parentId == 0 ? null : parentId,
                ToInt(region["MapID"]),
                MinX: ToDouble(region["Region_0"]),
                MaxX: ToDouble(region["Region_3"]),
                MinY: ToDouble(region["Region_1"]),
                MaxY: ToDouble(region["Region_4"]));

            zones.Add(MapArtCorrections.TryGetValue(id, out var correction) ? Correct(zone, correction) : zone);
        }

        return new GameDataReadResult(zones, categoryNames);
    }

    // Changes the map's world rectangle so that every map position p becomes shift + scale * p (in percent).
    // A smaller scale means the art shows a bigger part of the world, so the rectangle grows by 1 / scale.
    private static MapZone Correct(MapZone zone, (double Scale, double ShiftX, double ShiftY) correction)
    {
        double width = (zone.MaxY - zone.MinY) / correction.Scale;    // map x runs along world Y
        double height = (zone.MaxX - zone.MinX) / correction.Scale;   // map y runs along world X
        double maxY = zone.MaxY + correction.ShiftX * width / 100;
        double maxX = zone.MaxX + correction.ShiftY * height / 100;

        return zone with { MinX = maxX - height, MaxX = maxX, MinY = maxY - width, MaxY = maxY };
    }

    private static int ToInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static double ToDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}
