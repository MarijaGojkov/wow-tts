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

### Two voice engines

Each quest part in the viewer has two buttons: **Generate · Dragon HD Omni** (Azure Speech, as above) and **Generate · GPT-4o mini TTS**. The second uses OpenAI's `gpt-4o-mini-tts` on Azure OpenAI, which follows written instructions, so NPCs get accents and characters (a dwarf with a Scottish brogue, a slow, deep tauren). The voice director (gpt-5-mini) casts each NPC once (a GPT voice and a character, saved and shown in the viewer with **Recast GPT**) and writes how each line is delivered. It reports no word timings; the importer's `timings` command adds them later with Whisper.

To set it up:

1. In the Azure portal, create a **Foundry** resource in **East US 2** (gpt-4o-mini-tts isn't offered in East US), in the `wow-tts` resource group.
2. In the Foundry portal, deploy **gpt-4o-mini-tts** (Global Standard). The deployment name is up to you, e.g. `gpt-4o-mini-tts`.
3. From the deployment's page, set its endpoint, key and name:

   ```powershell
   setx AZURE_OPENAI_TTS_ENDPOINT "<endpoint from the deployment page>"
   setx AZURE_OPENAI_TTS_API_KEY "<key>"
   setx AZURE_OPENAI_TTS_DEPLOYMENT "gpt-4o-mini-tts"
   ```

4. Restart the API (and Visual Studio if you run it from there). `GET /api/voices/engines` then shows GPT-4o mini TTS as available, and its buttons work.

It's billed per length of audio on Foundry, about $0.015 a minute of speech (roughly 1.5–2 cents a quest); there's no free tier. Without these variables the GPT buttons stay greyed out.

## Setting up on a new PC

Step by step, so it can be followed by hand or by a coding agent. Commands run from the repo folder in PowerShell. Never paste keys into a chat; set them with `setx` yourself.

### 1. Tools

```powershell
winget install Microsoft.DotNet.SDK.9
winget install OpenJS.NodeJS.LTS
npm install --prefix web
```

Check: `dotnet --version` shows 9.x, `node --version` shows a version.

### 2. Keys (environment variables)

From the Azure portal:

- Speech resource `wow-speech-marija2000` → **Keys and Endpoint**: Key 1 and the region (`eastus`)
- Foundry resource `wow-agents-marija2000` → deployment `gpt-5-mini`: its key and endpoint

```powershell
setx SPEECH_KEY "<speech key>"
setx SPEECH_REGION "eastus"
setx AZURE_OPENAI_ENDPOINT "https://wow-agents-marija2000.services.ai.azure.com/openai/v1/responses"
setx AZURE_OPENAI_API_KEY "<foundry key>"
setx AZURE_OPENAI_DEPLOYMENT "gpt-5-mini"
```

If you use the second engine (GPT-4o mini TTS), also set `AZURE_OPENAI_TTS_ENDPOINT`, `AZURE_OPENAI_TTS_API_KEY` and `AZURE_OPENAI_TTS_DEPLOYMENT` (see "Two voice engines").

Then open a **new** terminal (and restart Visual Studio or the Claude app), because programs only see variables that existed when they started.

### 3. Data

Everything below is git-ignored. Either:

- **From the old PC:** extract `wow-tts-data.zip` into the repo folder, so its `data/`, `addon/` and `web/` folders merge with the repo's. It holds the database (quests, NPC voice castings, performances, word timings), the quest audio, the map images, the cMaNGOS dump, the game tables and the AI VoiceOver pack. Nothing to import then.
- **Or rebuild it:** download the cMaNGOS dump into `data/cmangos` and run the importer's `gamedata`, `cmangos`, `mapimages`, then download the AI VoiceOver pack into `data/voiceover` and run `voiceover` (see Quest data below). NPC voice castings and AI performances can't be rebuilt; the voice director makes new ones.

Check: `dotnet run --project src/WowTts.Api`, then http://localhost:5219/api/quests/stats shows about 4,221 quests and 6,300 parts with audio.

### 4. Whisper (only for making new word timings)

Word timings already in the database work without it. Whisper is only needed to time audio that has none yet (`timings`).

1. **Graphics card:** `nvidia-smi` lists an NVIDIA card and a driver of 580 or newer. Without an NVIDIA card, skip to step 3: Whisper then runs on the processor, which is many times slower.
2. **CUDA Toolkit 13** (about 3 GB): `winget install Nvidia.CUDA`, or the installer from https://developer.nvidia.com/cuda-downloads (Windows, x86_64, exe local). Afterwards a **new** terminal must show `nvcc --version` with release 13.x.
3. **The model** (1,624,555,275 bytes) into `data/models` (git-ignored):

   ```powershell
   New-Item -ItemType Directory -Force data/models
   curl.exe -L -o data/models/ggml-large-v3-turbo.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin
   (Get-Item data/models/ggml-large-v3-turbo.bin).Length   # 1624555275
   ```

4. **Test** on 20 lines: `dotnet run --project tools/WowTts.Importer -- timings --limit 20`. It prints `Whisper runs on: Cuda` with an NVIDIA card (`Cpu` without), and `Timed now: 20`, `Failed: 0`.
5. **Then zone by zone:** `dotnet run --project tools/WowTts.Importer -- timings --zone "Darkshore"`. A zone takes a few minutes on the graphics card. Ctrl+C stops after the current line; running it again carries on. Lines that already have timings are skipped.

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

# word timings (karaoke) for audio that has none, with Whisper; one zone at a time. See "Whisper" below
dotnet run --project tools/WowTts.Importer -- timings --zone Teldrassil
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
