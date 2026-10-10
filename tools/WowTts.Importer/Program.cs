using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WowTts.Application;
using WowTts.Application.Exceptions;
using WowTts.Application.Import;
using WowTts.Domain.Maps;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;
using WowTts.Infrastructure;
using WowTts.Infrastructure.Imaging;
using WowTts.Infrastructure.Import;
using WowTts.Infrastructure.Persistence;

// Imports data into the quest database (data/wowtts.db). Run from the repo folder:
//   dotnet run --project tools/WowTts.Importer -- gamedata [--refresh]
//   dotnet run --project tools/WowTts.Importer -- cmangos [path to ClassicDB_*.sql.gz]
//   dotnet run --project tools/WowTts.Importer -- samples [path to sample-quests.json]
//   dotnet run --project tools/WowTts.Importer -- voiceover [path to the extracted sound pack] [--replace]
//   dotnet run --project tools/WowTts.Importer -- timings [--zone NAME] [--limit N] [--redo]

string repoRoot = FindFolderContaining(AppContext.BaseDirectory, "WowTts.sln") ?? Environment.CurrentDirectory;
string command = args.FirstOrDefault()?.ToLowerInvariant() ?? "";
string? argument = args.Skip(1).FirstOrDefault();

if (command is not ("gamedata" or "cmangos" or "samples" or "mapimages" or "voiceover" or "timings"))
{
    PrintUsage();
    return 1;
}

if (command == "mapimages")
{
    // Doesn't need the database: everything comes from the game tables and goes into the web app's folder.
    try
    {
        await BuildMapImagesAsync(repoRoot, refresh: argument == "--refresh");
        return 0;
    }
    catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or HttpRequestException)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    // appsettings.json paths such as "../../data/wowtts.db" are relative to this project's folder,
    // the same way the Api's are relative to src/WowTts.Api.
    ContentRootPath = Path.Combine(repoRoot, "tools", "WowTts.Importer"),
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

using IHost host = builder.Build();
await host.Services.EnsureDatabaseAsync();
using var scope = host.Services.CreateScope();
var stopwatch = Stopwatch.StartNew();

try
{
    if (command == "gamedata")
    {
        string folder = Path.Combine(repoRoot, "data", "gamedata");
        await DownloadTablesAsync(folder, [.. GameDataReader.Tables, .. NpcRaceReader.Tables], refresh: argument == "--refresh");

        GameDataReadResult read = GameDataReader.Read(folder);
        await scope.ServiceProvider.GetRequiredService<GameDataImportService>().ImportAsync(read.Zones, read.CategoryNames);

        Console.WriteLine();
        Console.WriteLine($"Continents:          {read.Zones.Count(z => z.Type == MapZoneType.Continent)}");
        Console.WriteLine($"Zones and cities:    {read.Zones.Count(z => z.Type == MapZoneType.Zone)}");
        Console.WriteLine($"Quest log headers:   {read.CategoryNames.Count}");
        Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }

    if (command == "timings")
    {
        // Word timings for audio that has none (the imported AI VoiceOver lines), from Whisper on the graphics card.
        string model = Path.Combine(repoRoot, "data", "models", "ggml-large-v3-turbo.bin");
        if (!File.Exists(model))
        {
            Console.Error.WriteLine($"The Whisper model is missing: {model}");
            Console.Error.WriteLine("Download ggml-large-v3-turbo.bin from https://huggingface.co/ggerganov/whisper.cpp into data/models.");
            return 1;
        }

        int? limit = args.SkipWhile(arg => arg != "--limit").Skip(1).Select(arg => int.TryParse(arg, out int n) ? n : (int?)null).FirstOrDefault();
        string? zone = args.SkipWhile(arg => arg != "--zone").Skip(1).FirstOrDefault();
        bool redo = args.Contains("--redo", StringComparer.OrdinalIgnoreCase);

        // Ctrl+C finishes the current line and stops; what's done is saved, so the next run carries on.
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); Console.WriteLine("Stopping after this line…"); };

        Console.WriteLine($"Loading Whisper {Path.GetFileName(model)}");
        using var whisper = new WhisperWordRecognizer(model);
        Console.WriteLine($"Whisper runs on: {whisper.Runtime}");
        if (zone is not null)
            Console.WriteLine($"Only quests in {zone}");

        var progressClock = Stopwatch.StartNew();
        TimingImportResult timed = await scope.ServiceProvider.GetRequiredService<TimingImportService>().ImportAsync(
            whisper, zone, redo, limit,
            (done, total) =>
            {
                if (done % 25 != 0 && done != total)
                    return;
                var left = TimeSpan.FromSeconds(progressClock.Elapsed.TotalSeconds / done * (total - done));
                Console.WriteLine($"  {done,5} of {total}  ({Clock(progressClock.Elapsed)} so far, about {Clock(left)} left)");
            },
            stop.Token);

        Console.WriteLine();
        Console.WriteLine($"Parts with audio:  {timed.WithAudio}");
        Console.WriteLine($"Already timed:     {timed.AlreadyTimed}");
        Console.WriteLine($"Timed now:         {timed.Timed}");
        Console.WriteLine($"Failed:            {timed.Failed}");
        Console.WriteLine($"Poor matches:      {timed.PoorMatches.Count} (below {TimingImportService.PoorMatchBelow:P0} of the words; the audio may say something else)");
        foreach (var poor in timed.PoorMatches.Take(15))
            Console.WriteLine($"  quest {poor.QuestId} {poor.Part.ToString().ToLowerInvariant()}: {poor.Match:P0}");
        if (timed.Stopped)
            Console.WriteLine("Stopped early. Run it again to carry on.");
        Console.WriteLine($"Done in {Clock(stopwatch.Elapsed)}");
        return 0;
    }

    if (command == "voiceover")
    {
        // Ready-made quest audio from the AI VoiceOver addon. Its voices are ElevenLabs clones of the game's own NPC
        // voices, one per race and gender. Personal use only: the audio has no licence, so it stays out of git.
        string folder = Path.GetFullPath(args.Skip(1).FirstOrDefault(arg => !arg.StartsWith("--")) ?? Path.Combine(repoRoot, "data", "voiceover"));
        bool replace = args.Contains("--replace", StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Reading the AI VoiceOver sound pack in {folder}");
        var files = VoiceOverPackReader.Read(folder);
        Console.WriteLine($"Found {files.Count} quest audio files");

        var performance = VoicePerformance.Imported(
            "AI VoiceOver",
            "Imported from the AI VoiceOver addon, whose voices are cloned per race and gender from the game's own NPC voices.");
        AudioImportResult audio = await scope.ServiceProvider.GetRequiredService<AudioImportService>().ImportAsync(files, performance, replace);

        Console.WriteLine();
        Console.WriteLine($"Imported:       {audio.Imported}");
        Console.WriteLine($"Replaced:       {audio.Replaced}");
        Console.WriteLine($"Kept existing:  {audio.KeptExisting} (already had audio; --replace overwrites it)");
        Console.WriteLine($"Unknown quest:  {audio.UnknownQuest} (not in the database)");
        Console.WriteLine($"No such text:   {audio.NoText} (the quest has no text for that part)");
        Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:0.0} s");
        return 0;
    }

    IReadOnlyList<Quest> quests;
    if (command == "cmangos")
    {
        string path = argument is not null
            ? Path.GetFullPath(argument)
            : FindNewestDump(Path.Combine(repoRoot, "data", "cmangos"));

        // cMaNGOS has no NPC races; they come from the game's own tables, by each NPC's model.
        string gameData = Path.Combine(repoRoot, "data", "gamedata");
        await DownloadTablesAsync(gameData, NpcRaceReader.Tables, refresh: false);
        var raceByModel = NpcRaceReader.Read(gameData);

        Console.WriteLine($"Reading the cMaNGOS dump {path}");
        QuestReadResult read = CMangosQuestReader.Read(path, raceByModel);
        quests = read.Quests;

        Console.WriteLine($"Found {quests.Count} quests");
        Console.WriteLine($"  quest givers by race: {string.Join(", ", quests
            .GroupBy(quest => quest.NpcRace).OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}"))}");
        Console.WriteLine($"  handed in to someone else: {quests.Count(quest => quest.Ender is not null)}");
        foreach (var (reason, count) in read.Skipped)
            Console.WriteLine($"  skipped {count} with {reason}");
    }
    else
    {
        string path = Path.GetFullPath(argument ?? Path.Combine(repoRoot, "data", "sample-quests.json"));

        Console.WriteLine($"Reading the sample quests {path}");
        quests = await SampleQuestReader.ReadAsync(path);
        Console.WriteLine($"Found {quests.Count} quests");
    }

    ImportResult result = await scope.ServiceProvider.GetRequiredService<QuestImportService>().ImportAsync(quests);

    Console.WriteLine();
    Console.WriteLine($"Added:          {result.Added}");
    Console.WriteLine($"Updated:        {result.Updated}");
    Console.WriteLine($"Unchanged:      {result.Unchanged}");
    Console.WriteLine($"Kept existing:  {result.KeptExisting} (the database has text from a higher-priority source)");
    Console.WriteLine($"Audio deleted:  {result.AudioDeleted} (its text or speaker changed, so the old MP3 no longer matched)");
    Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:0.0} s");
    return 0;
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or InvalidDataException or HttpRequestException
                               or InvalidRequestException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// Builds web/public/maps/<map id>.png for every continent and zone: downloads the map's tiles
// (BLP files from the game, via wago.tools) once, decodes them and stitches them into one image.
static async Task BuildMapImagesAsync(string repoRoot, bool refresh)
{
    var stopwatch = Stopwatch.StartNew();
    string gameData = Path.Combine(repoRoot, "data", "gamedata");
    string tileFolder = Path.Combine(gameData, "tiles");
    string mapFolder = Path.Combine(repoRoot, "web", "public", "maps");
    Directory.CreateDirectory(tileFolder);
    Directory.CreateDirectory(mapFolder);

    await DownloadTablesAsync(gameData, [.. GameDataReader.Tables, .. MapArtReader.Tables], refresh);

    var zones = GameDataReader.Read(gameData).Zones;
    var arts = MapArtReader.Read(gameData, zones.Select(zone => zone.Id));
    string TilePath(int fileDataId) => Path.Combine(tileFolder, $"{fileDataId}.blp");

    var allTiles = arts
        .SelectMany(art => art.Tiles.Concat(art.Overlays.SelectMany(overlay => overlay.Tiles)))
        .Select(tile => tile.FileDataId)
        .Distinct()
        .ToList();
    var missing = allTiles.Where(id => refresh || !File.Exists(TilePath(id))).ToList();
    Console.WriteLine($"{arts.Count} maps, {arts.Sum(art => art.Overlays.Count)} overlays, {allTiles.Count} tiles. " +
                      $"Downloading {missing.Count} from wago.tools…");

    using var http = CreateHttpClient();
    int done = 0;
    // A few at a time, to be gentle on wago.tools.
    await Parallel.ForEachAsync(missing, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (id, cancel) =>
    {
        byte[] blp = await http.GetByteArrayAsync($"https://wago.tools/api/casc/{id}", cancel);
        await File.WriteAllBytesAsync(TilePath(id), blp, cancel);
        int count = Interlocked.Increment(ref done);
        if (count % 100 == 0 || count == missing.Count)
            Console.WriteLine($"  {count} / {missing.Count}");
    });

    foreach (MapArt art in arts)
    {
        var canvas = new RgbaImage(art.Width, art.Height, new byte[art.Width * art.Height * 4]);
        foreach (MapArtTile tile in art.Tiles)
        {
            RgbaImage image = BlpImage.Decode(await File.ReadAllBytesAsync(TilePath(tile.FileDataId)));
            CopyTile(image, canvas, tile.Column * art.TileWidth, tile.Row * art.TileHeight);
        }

        // The explored details, blended on top through their transparency, in the game's order.
        foreach (MapOverlay overlay in art.Overlays)
        {
            foreach (MapArtTile tile in overlay.Tiles)
            {
                RgbaImage image = BlpImage.Decode(await File.ReadAllBytesAsync(TilePath(tile.FileDataId)));
                BlendOverlayTile(image, canvas, overlay, tile);
            }
        }

        PngWriter.WriteRgb(Path.Combine(mapFolder, $"{art.UiMapId}.png"), canvas);
    }

    string names = string.Join(", ", arts.Take(5).Select(art => zones.First(zone => zone.Id == art.UiMapId).Name));
    Console.WriteLine($"Saved {arts.Count} map images to {mapFolder} ({names}, …)");
    Console.WriteLine($"Done in {stopwatch.Elapsed.TotalSeconds:0.0} s");
}

// Copies a tile into the map image at (left, top). The edge tiles reach past the visible map, so they're clipped.
static void CopyTile(RgbaImage tile, RgbaImage canvas, int left, int top)
{
    for (int y = 0; y < tile.Height && top + y < canvas.Height; y++)
    {
        int width = Math.Min(tile.Width, canvas.Width - left);
        if (width <= 0)
            return;
        Array.Copy(tile.Pixels, y * tile.Width * 4, canvas.Pixels, ((top + y) * canvas.Width + left) * 4, width * 4);
    }
}

// Draws one overlay tile with transparency, the way the game's map does it: tiles are 256 x 256, except the
// last row and column, which only show what's left of the overlay (e.g. 50 px of a 306 px overlay). That part
// covers the share of the tile the game expects (50 of 64 px), and a lower-resolution tile file is stretched to fit.
static void BlendOverlayTile(RgbaImage tile, RgbaImage canvas, MapOverlay overlay, MapArtTile place)
{
    const int TileSize = 256;
    var (shownWidth, expectedWidth) = TilePart(overlay.Width, place.Column);
    var (shownHeight, expectedHeight) = TilePart(overlay.Height, place.Row);
    int left = overlay.Left + place.Column * TileSize;
    int top = overlay.Top + place.Row * TileSize;

    for (int y = 0; y < shownHeight; y++)
    {
        int cy = top + y;
        if (cy < 0 || cy >= canvas.Height)
            continue;
        int ty = Math.Min(tile.Height - 1, y * tile.Height / expectedHeight);

        for (int x = 0; x < shownWidth; x++)
        {
            int cx = left + x;
            if (cx < 0 || cx >= canvas.Width)
                continue;
            int tx = Math.Min(tile.Width - 1, x * tile.Width / expectedWidth);

            int source = (ty * tile.Width + tx) * 4;
            int target = (cy * canvas.Width + cx) * 4;
            int alpha = tile.Pixels[source + 3];
            for (int channel = 0; channel < 3; channel++)
            {
                canvas.Pixels[target + channel] = (byte)(
                    (tile.Pixels[source + channel] * alpha + canvas.Pixels[target + channel] * (255 - alpha)) / 255);
            }
        }
    }
}

// For a tile at this index along an overlay of this size: how many pixels it shows, and the tile size the game
// expects for it (256, or for the last tile the next power of two of what's left, at least 16).
static (int Shown, int Expected) TilePart(int overlaySize, int index)
{
    const int TileSize = 256;
    int count = (overlaySize + TileSize - 1) / TileSize;
    if (index < count - 1)
        return (TileSize, TileSize);

    int shown = overlaySize % TileSize == 0 ? TileSize : overlaySize % TileSize;
    int expected = 16;
    while (expected < shown)
        expected *= 2;
    return (shown, expected);
}

static HttpClient CreateHttpClient()
{
    var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("wow-tts-importer");
    return http;
}

// Downloads Classic Era client tables from wago.tools as CSV, unless they're already there.
static async Task DownloadTablesAsync(string folder, IEnumerable<string> tables, bool refresh)
{
    Directory.CreateDirectory(folder);
    using var http = CreateHttpClient();

    foreach (string table in tables)
    {
        string file = Path.Combine(folder, $"{table}.csv");
        if (File.Exists(file) && !refresh)
        {
            Console.WriteLine($"Using {file}");
            continue;
        }

        string url = $"https://wago.tools/db2/{table}/csv?product=wow_classic_era";
        Console.WriteLine($"Downloading {url}");
        byte[] csv = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(file, csv);
    }
}

// "01:05:09"
static string Clock(TimeSpan time) => time.ToString(@"hh\:mm\:ss");

static void PrintUsage()
{
    Console.WriteLine("Imports data into data/wowtts.db.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- gamedata [--refresh]");
    Console.WriteLine("      Zone names and the world map, from the game's Classic Era tables on wago.tools.");
    Console.WriteLine("      Downloads them into data/gamedata once; --refresh downloads them again.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- cmangos [path]");
    Console.WriteLine("      Vanilla quests from the cMaNGOS classic-db dump (.sql.gz or .sql), with NPC races");
    Console.WriteLine("      from the game's tables on wago.tools (downloaded into data/gamedata once).");
    Console.WriteLine("      Without a path, uses the newest dump in data/cmangos.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- samples [path]");
    Console.WriteLine("      The hand-written test quests. Without a path, uses data/sample-quests.json.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- voiceover [path] [--replace]");
    Console.WriteLine("      Quest audio from the AI VoiceOver addon's sound pack (accept and complete, no progress).");
    Console.WriteLine("      Without a path, uses data/voiceover. Parts that already have audio are kept;");
    Console.WriteLine("      --replace overwrites them.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- timings [--zone NAME] [--limit N] [--redo]");
    Console.WriteLine("      Word timings (for karaoke) for audio that has none, from Whisper large-v3-turbo on the");
    Console.WriteLine("      graphics card. Needs data/models/ggml-large-v3-turbo.bin. Ctrl+C stops; running it again");
    Console.WriteLine("      carries on. --zone does one zone, e.g. --zone Teldrassil (quote names with spaces);");
    Console.WriteLine("      --limit N does only N lines; --redo times them again.");
    Console.WriteLine();
    Console.WriteLine("  dotnet run --project tools/WowTts.Importer -- mapimages [--refresh]");
    Console.WriteLine("      The game's world map images, from wago.tools, into web/public/maps/<map id>.png.");
    Console.WriteLine("      Tiles are cached in data/gamedata/tiles; --refresh downloads them again.");
}

static string FindNewestDump(string folder)
{
    string? newest = Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "*.sql*")
            .Where(file => file.EndsWith(".sql") || file.EndsWith(".sql.gz"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
        : null;

    return newest ?? throw new FileNotFoundException(
        $"No cMaNGOS dump found in {folder}. Download ClassicDB_*.sql.gz from " +
        "https://github.com/cmangos/classic-db/tree/master/Full_DB into that folder, or pass its path.");
}

static string? FindFolderContaining(string start, string fileName)
{
    for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
    {
        if (File.Exists(Path.Combine(folder.FullName, fileName)))
            return folder.FullName;
    }
    return null;
}
