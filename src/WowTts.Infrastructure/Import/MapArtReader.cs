using System.Globalization;

namespace WowTts.Infrastructure.Import;

// One tile of a map image: its place in the grid and the game file (FileDataID) that holds it.
public sealed record MapArtTile(int Row, int Column, int FileDataId);

// A detail layer of a map (towns, roads, forests) that the game reveals as you explore that area.
// Its image is Width x Height, split into 256 x 256 tiles, and drawn at (Left, Top) on the map.
public sealed record MapOverlay(int Left, int Top, int Width, int Height, IReadOnlyList<MapArtTile> Tiles);

// How one world map image (UiMap) is put together from tiles. Width and Height is the visible part,
// e.g. 1002 x 668 for a zone, made from a 4 x 3 grid of 256 x 256 tiles. The base tiles are the
// unexplored map; drawing every overlay on top gives the fully explored map.
public sealed record MapArt(
    int UiMapId,
    int Width,
    int Height,
    int TileWidth,
    int TileHeight,
    IReadOnlyList<MapArtTile> Tiles,
    IReadOnlyList<MapOverlay> Overlays);

// Reads which tiles make up each world map image, from the game client tables wago.tools exports:
// UiMapXMapArt (map -> art), UiMapArt (art -> style), UiMapArtStyleLayer (sizes), UiMapArtTile (base tiles),
// and WorldMapOverlay with WorldMapOverlayTile (the explored details).
public static class MapArtReader
{
    public static readonly string[] Tables =
        ["UiMapXMapArt", "UiMapArt", "UiMapArtStyleLayer", "UiMapArtTile", "WorldMapOverlay", "WorldMapOverlayTile"];

    public static IReadOnlyList<MapArt> Read(string folder, IEnumerable<int> uiMapIds)
    {
        var mapToArt = CsvFile.Read(Path.Combine(folder, "UiMapXMapArt.csv"))
            .Where(row => ToInt(row["PhaseID"]) == 0)
            .GroupBy(row => ToInt(row["UiMapID"]))
            .ToDictionary(group => group.Key, group => ToInt(group.First()["UiMapArtID"]));

        var artToStyle = CsvFile.Read(Path.Combine(folder, "UiMapArt.csv"))
            .ToDictionary(row => ToInt(row["ID"]), row => ToInt(row["UiMapArtStyleID"]));

        var styleLayers = CsvFile.Read(Path.Combine(folder, "UiMapArtStyleLayer.csv"))
            .Where(row => ToInt(row["LayerIndex"]) == 0)
            .ToDictionary(row => ToInt(row["UiMapArtStyleID"]));

        var tilesByArt = CsvFile.Read(Path.Combine(folder, "UiMapArtTile.csv"))
            .Where(row => ToInt(row["LayerIndex"]) == 0)
            .GroupBy(row => ToInt(row["UiMapArtID"]))
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new MapArtTile(
                    ToInt(row["RowIndex"]), ToInt(row["ColIndex"]), ToInt(row["FileDataID"]))).ToList());

        var overlayTiles = CsvFile.Read(Path.Combine(folder, "WorldMapOverlayTile.csv"))
            .Where(row => ToInt(row["LayerIndex"]) == 0)
            .GroupBy(row => ToInt(row["WorldMapOverlayID"]))
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new MapArtTile(
                    ToInt(row["RowIndex"]), ToInt(row["ColIndex"]), ToInt(row["FileDataID"]))).ToList());

        var overlaysByArt = CsvFile.Read(Path.Combine(folder, "WorldMapOverlay.csv"))
            .Where(row => overlayTiles.ContainsKey(ToInt(row["ID"])))
            .GroupBy(row => ToInt(row["UiMapArtID"]))
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => new MapOverlay(
                    ToInt(row["OffsetX"]),
                    ToInt(row["OffsetY"]),
                    ToInt(row["TextureWidth"]),
                    ToInt(row["TextureHeight"]),
                    overlayTiles[ToInt(row["ID"])])).ToList());

        var result = new List<MapArt>();
        foreach (int uiMapId in uiMapIds)
        {
            if (!mapToArt.TryGetValue(uiMapId, out int artId)
                || !artToStyle.TryGetValue(artId, out int styleId)
                || !styleLayers.TryGetValue(styleId, out var layer)
                || !tilesByArt.TryGetValue(artId, out var tiles))
                continue;

            result.Add(new MapArt(
                uiMapId,
                ToInt(layer["LayerWidth"]),
                ToInt(layer["LayerHeight"]),
                ToInt(layer["TileWidth"]),
                ToInt(layer["TileHeight"]),
                tiles,
                overlaysByArt.GetValueOrDefault(artId) ?? []));
        }

        return result;
    }

    private static int ToInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
