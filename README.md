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

### AI voice director (optional)

When you generate a quest's audio, a GPT model on Azure OpenAI picks the voice for the NPC and how each line is spoken (style such as angry or whispering, its strength, speed and pitch). Set these three variables to turn it on:

```powershell
setx AZURE_OPENAI_ENDPOINT "https://<your-resource>.openai.azure.com/"
setx AZURE_OPENAI_API_KEY "<key>"
setx AZURE_OPENAI_DEPLOYMENT "<deployment name>"
```

Any URL Microsoft Foundry shows for the resource works as the endpoint: the Azure OpenAI endpoint, the project endpoint, or a deployment's Target URI (which also gives the deployment name, so `AZURE_OPENAI_DEPLOYMENT` can be left out).

Without them, quest lines use the default voices (`DefaultMaleVoice` and `DefaultFemaleVoice` in `appsettings.json`) and no style. Restart the API, and Visual Studio if you run it from there, after setting them.

### Standard or HD voices

`Speech:VoiceModel` in `appsettings.json` picks which voices quest lines use:

| Value | Voices |
|---|---|
| `Neural` | The standard voices. A few have styles, and speed and pitch can be changed. $15 per million characters, or free within F0's monthly allowance |
| `DragonHDOmni` | Dragon HD Omni versions of the same voices. Much more natural, and every voice has the same ~60 styles, but speed, pitch and style strength can't be changed. $22 per million characters (East US) |

An NPC keeps the same person when you switch, e.g. `en-US-TonyNeural` becomes `en-US-Tony:DragonHDOmniLatestNeural`. Existing MP3s stay as they are until you regenerate them. Restart the API after changing it.

## Quest data

Quests live in a SQLite database, `data/wowtts.db` (git-ignored). Fill it with the importer, run from the repo folder:

```powershell
# zone names and the world map (Classic Era client tables from wago.tools, downloaded into data/gamedata once)
dotnet run --project tools/WowTts.Importer -- gamedata

# ~4,200 vanilla quests from the cMaNGOS classic-db dump, with who gives and who takes back each quest
# (download ClassicDB_*.sql.gz from https://github.com/cmangos/classic-db/tree/master/Full_DB into data/cmangos).
# NPC races come from the game's model tables on wago.tools, downloaded into data/gamedata once
dotnet run --project tools/WowTts.Importer -- cmangos

# the 5 hand-written test quests
dotnet run --project tools/WowTts.Importer -- samples

# ready-made quest audio from the AI VoiceOver addon's English vanilla sound pack (personal use, git-ignored):
# extract AI_VoiceOverData_Vanilla into data/voiceover first. --replace overwrites audio you already have
dotnet run --project tools/WowTts.Importer -- voiceover
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
