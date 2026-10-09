# wow-tts

An ASP.NET Core Web API that turns World of Warcraft quest text into speech using Azure AI Speech, plus a small WoW addon that plays the audio in game. See [PLAN.md](PLAN.md) for the full plan.

## Solution

Onion architecture: dependencies point inward.

| Project | Layer | References |
|---|---|---|
| `src/WowTts.Domain` | WoW rules: quests, text cleaning | nothing |
| `src/WowTts.Application` | Services (use cases) and the interfaces they need | Domain |
| `src/WowTts.Infrastructure` | Azure Speech, audio files, SQLite quest database, importers | Application |
| `src/WowTts.Api` | Controllers, OpenAPI, wiring | Application, Infrastructure |
| `tools/WowTts.Importer` | Console tool that imports quests into the database | Application, Infrastructure |

## Requirements

- .NET 9 SDK
- An Azure Speech resource
- These environment variables:

```powershell
setx SPEECH_KEY "<your key>"
setx SPEECH_REGION "eastus"
```

Open a new terminal after running `setx`.

## Quest data

Quests live in a SQLite database, `data/wowtts.db` (git-ignored). Fill it with the importer, run from the repo folder:

```powershell
# zone names and the world map (Classic Era client tables from wago.tools, downloaded into data/gamedata once)
dotnet run --project tools/WowTts.Importer -- gamedata

# ~4,200 vanilla quests from the cMaNGOS classic-db dump
# (download ClassicDB_*.sql.gz from https://github.com/cmangos/classic-db/tree/master/Full_DB into data/cmangos)
dotnet run --project tools/WowTts.Importer -- cmangos

# the 5 hand-written test quests
dotnet run --project tools/WowTts.Importer -- samples
```

If the API or importer says the database was created by an older version, delete `data/wowtts.db` and run `gamedata` and `cmangos` again.

Running an import again only changes what's different. Text captured in game beats cMaNGOS, which beats the samples. When a quest's text changes, its old MP3s are deleted so they get regenerated.

## Run

```powershell
dotnet run --project src/WowTts.Api
```

Then open http://localhost:5219/scalar to see and try the endpoints. The sample requests in `src/WowTts.Api/WowTts.Api.http` can be run from Visual Studio or VS Code.

## Quest viewer

`web/` is a small React + TypeScript site (Vite) that lists the quests, shows each quest's text and NPC, plays the MP3s, and generates missing audio. Requires Node.js 20 or later.

With the API running:

```powershell
cd web
npm install
npm run dev
```

Then open http://localhost:5173. Requests to `/api` are forwarded to the API on port 5219 by Vite.

### World map

The map shows the continents and zones from the game's map data, with every quest giver as a dot. Click a zone to list its quests; hover a dot to see an NPC's quests. On screens 1400 px or wider the map and the quest sit side by side; on narrower ones, tabs switch between them.

The real WoW map images come from the importer, run from the repo folder:

```powershell
dotnet run --project tools/WowTts.Importer -- mapimages
```

It reads which tiles make up each map from the game's Classic Era tables, downloads the tiles (Blizzard's BLP texture files) from [wago.tools](https://wago.tools) once into `data/gamedata/tiles`, decodes them, adds every explored-area overlay, and saves one image per map as `web/public/maps/<map id>.png` (51 maps, about 50 MB). The images are Blizzard's art, so they're git-ignored. Without them, the map draws the zones itself.

The game's own rectangle for the Eastern Kingdoms continent doesn't match its map art, so `GameDataReader` applies a measured correction for it (Kalimdor fits as is). After changing it, run `gamedata` again.

A few subzone names are partly covered where two of the game's overlays overlap (e.g. "Tower of Azora" in Elwynn Forest); that's how the game's own map pieces fit together.
