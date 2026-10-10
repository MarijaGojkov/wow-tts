# wow-tts implementation plan

**Updated:** 10 October 2026
**Status:** Phases 0–9, 9.2 (NPC races, who hands in) and 9.3 (AI VoiceOver audio, 6,289 parts) are done. Dragon HD Omni voices (9.1) are being compared. Next is Phase 9.4 (word timings and karaoke), then Phase 10, the addon.
**Scope for starters:** speech, quests, a quest viewer and a small addon. No tests, batch jobs or voices endpoint yet.
**Game:** World of Warcraft: Forever (launches 4 November 2026). About 5,200 quests: roughly 3,600 unchanged vanilla quests, 616 changed and 1,001 new.

## Goal

A small ASP.NET Core Web API, built with onion (clean) architecture, that turns World of Warcraft quest text into speech with Azure AI Speech, plus a small WoW addon that plays it.

When it's finished:

- `POST /api/speech` turns any text into an MP3.
- `GET /api/quests` lists the quests, and `GET /api/quests/{id}` shows one.
- `GET /api/quests/783/audio/accept` returns a quest's audio. It's generated on the first request and saved, so Azure is only paid once per text.
- A small local website, Quest Voices, shows every quest with its text, the NPC and an audio player.
- The quests live in a SQLite database: about 4,000 vanilla quests imported from cMaNGOS, plus Forever's new and changed quests captured in game.
- The audio is saved straight into the addon's folder. When you open a quest in game, the addon plays it.

## How the API and the addon work together

WoW addons can't make network calls. WoW's Lua has no HTTP or sockets, so the addon can never call the API. It can only play sound files that are already in its own folder. So the work is split:

```
 Before playing                                  In game
 ─────────────                                   ───────
 you → API → Azure Speech                        quest window opens
        │                                               │
        └─ saves  Interface\AddOns\WowTts\audio\        addon builds the path from the quest ID
                  783-accept.mp3  ───────────────────→  PlaySoundFile(...\783-accept.mp3)
```

1. Before playing, you call the API (from Scalar or the `.http` file). It generates the MP3s straight into the addon's `audio` folder.
2. In game, the addon listens for the quest window events, takes the quest ID and plays the matching file. If there's no file, nothing happens.
3. WoW only finds files that existed when the game started. After generating new audio, restart the game. A `/reload` isn't enough.

## Architecture

### Layers

Four projects. Dependencies point inward: outer layers know about inner layers, never the other way round. Project references enforce this, because a project can only use what it references.

```
        ┌──────────────────────────────────────────┐
        │   Api                 Infrastructure     │   outer ring
        │   ┌──────────────────────────────────┐   │
        │   │   Application                    │   │
        │   │   ┌──────────────────────────┐   │   │
        │   │   │   Domain                 │   │   │   core
        │   │   └──────────────────────────┘   │   │
        │   └──────────────────────────────────┘   │
        └──────────────────────────────────────────┘
```

| Layer | Holds | Depends on | Knows about Azure? |
|---|---|---|---|
| **Domain** | `Quest`, `QuestPart`, `TextCleaner`: the WoW rules | Nothing | No |
| **Application** | `SpeechService`, `QuestService`, the interfaces they need, DTOs | Domain | No |
| **Infrastructure** | Azure Speech SDK, audio files in the addon folder, SQLite database (EF Core), cMaNGOS and sample file readers | Application, Domain | Yes |
| **Api** | Controllers, error handling, OpenAPI, wiring everything together | Application, Infrastructure | Only wires it up |

The services in Application only know interfaces such as `ISpeechSynthesizer` and `IAudioStore`. Infrastructure provides the real versions, and `Program.cs` connects the two. Switching from files on disk to Azure Blob Storage later would only touch Infrastructure.

### Endpoints

| Method | Route | What it does | Returns | Phase |
|---|---|---|---|---|
| POST | `/api/speech` | Turns text into speech, with an optional voice | 200 `audio/mpeg`, 400, 502 | 3 |
| GET | `/api/quests` | Lists all quests | 200 JSON | 4 |
| GET | `/api/quests/{id}` | One quest's details | 200, 404 | 4 |
| GET | `/api/quests/{id}/audio/{part}` | Audio for `accept`, `progress` or `complete`, generated on first request | 200 `audio/mpeg`, 404 | 4 |

### Request flow: `GET /api/quests/783/audio/accept`

1. **Api**: `QuestsController` calls `QuestService.GetOrCreateAudioAsync(783, Accept)`.
2. **Application**: asks `IAudioStore` whether `783-accept.mp3` exists. If yes, it returns the file and stops here.
3. **Application**: loads the quest through `IQuestRepository` and takes its accept text.
4. **Domain**: `TextCleaner` replaces `$N`, `$B`, `$G` and the other placeholders.
5. **Infrastructure**: `AzureSpeechSynthesizer` calls Azure Speech and gets MP3 audio back.
6. **Infrastructure**: `FileAudioStore` saves `783-accept.mp3` in the audio folder from `appsettings.json`, which is the addon's `audio` folder.
7. **Api**: the controller returns the audio as `audio/mpeg`.

## Solution layout

This is where the solution ends up after Phase 8.

```
wow-tts/
├─ WowTts.sln
├─ src/
│  ├─ WowTts.Domain/
│  │  ├─ Quests/            Quest, QuestPart, QuestSource
│  │  └─ Speech/            TextCleaner
│  ├─ WowTts.Application/
│  │  ├─ Abstractions/      ISpeechSynthesizer, IQuestRepository, IAudioStore
│  │  ├─ Speech/            SpeechService, SpeechRequest
│  │  ├─ Quests/            QuestService, QuestDto
│  │  ├─ Import/            QuestImportService, ImportResult
│  │  └─ DependencyInjection.cs
│  ├─ WowTts.Infrastructure/
│  │  ├─ Speech/            AzureSpeechSynthesizer, SpeechOptions
│  │  ├─ Persistence/       WowTtsDbContext, SqliteQuestRepository
│  │  ├─ Import/            MySqlDumpReader, CMangosQuestReader, SampleQuestReader
│  │  ├─ Storage/           FileAudioStore
│  │  └─ DependencyInjection.cs
│  └─ WowTts.Api/
│     ├─ Controllers/       SpeechController, QuestsController
│     ├─ ErrorHandling/     GlobalExceptionHandler
│     ├─ Program.cs
│     ├─ appsettings.json
│     └─ WowTts.Api.http    sample requests you can click to run
├─ tools/
│  └─ WowTts.Importer/      console tool: imports quests into the database
├─ web/                     Quest Voices: React + Vite quest viewer
│  └─ src/                  App, QuestList, QuestDetail, api.ts
├─ addon/
│  └─ WowTts/               the WoW addon, linked into Interface\AddOns\WowTts
│     ├─ WowTts.toc         addon name, version, files to load
│     ├─ WowTts.lua         plays audio when a quest window opens
│     └─ audio/             generated MP3s (git-ignored)
├─ data/
│  ├─ wowtts.db             SQLite database with all quests (git-ignored)
│  ├─ cmangos/              downloaded cMaNGOS database dump (git-ignored)
│  └─ sample-quests.json    5 hand-written test quests
├─ docs/plan.html
├─ PLAN.md
└─ README.md
```

## Quest data

### Where it comes from

| Source | What it gives us | Covers |
|---|---|---|
| **[cMaNGOS classic-db](https://github.com/cmangos/classic-db)** (GPL-3, `ClassicDB_1_12_1_z2815.sql.gz`, 12 MB) | Full quest text: accept (`Details`), progress (`RequestItemsText`) and complete (`OfferRewardText`). Quest givers from `creature_questrelation` and `gameobject_questrelation`, NPC names, and gender from `creature_model_info` | All vanilla quests, which is most of Forever |
| **The game itself** (Phase 8) | The exact Forever text, captured by the addon when you open a quest | Forever's 1,001 new and 616 changed quests |
| **`data/sample-quests.json`** | 5 hand-written test quests | Testing |

cMaNGOS has no NPC race, so race is "Unknown" for imported quests until we add it from the game's client data.

### Where it's stored

All quests live in one SQLite file, `data/wowtts.db`, through Entity Framework Core. It's git-ignored, so the repo never holds Blizzard's text. Audio stays as MP3 files in `addon/WowTts/audio/`, because the game can only play files.

| Column | Example | Notes |
|---|---|---|
| `Id` | `783` | Quest ID, used in routes, file names and by the addon |
| `Title` | A Threat Within | Not read aloud |
| `NpcName` | Deputy Willem | Quest giver |
| `NpcRace` | Human, or Unknown | Used later for NPC voices |
| `NpcGender` | Male, Female or Unknown | Used later for NPC voices |
| `AcceptText` | What the NPC says when you take the quest | Required |
| `ProgressText` | What the NPC says if you return before finishing | Optional |
| `CompleteText` | What the NPC says when you hand the quest in | Optional |
| `Source` | Sample, CMangos or Captured | Where the text came from |
| `UpdatedAt` | 2026-10-08 20:15 | Last import that changed it |

**Which text wins:** Captured beats CMangos, and CMangos beats Sample. So Forever's own text always replaces the vanilla text for the same quest ID. When an import changes a quest's text, its old MP3s are deleted, so they're regenerated from the new text.

## Phases

Every phase ends with an API you can call from the Scalar page, a commit and a push.

### Phase 0: Setup ✅

- [x] .NET 9 SDK, Speech SDK package, `SPEECH_KEY` and `SPEECH_REGION=eastus`
- [x] Private GitHub repo: `MarijaGojkov/wow-tts`
- [x] Speech resource and key confirmed

### Phase 1: First words ✅ (console app, replaced in Phase 2)

- [x] A console app spoke one quest line with `en-US-GuyNeural`.
- [x] It proved the key, region and Speech SDK work. Its code moves into Infrastructure in Phase 3.

### Phase 2: Solution skeleton ✅

**Goal:** a layered solution that builds and runs, with no features yet.

- [x] Create `WowTts.sln` with a `src/` folder.
- [x] Create the class libraries `WowTts.Domain`, `WowTts.Application` and `WowTts.Infrastructure`, and the Web API `WowTts.Api` with controllers.
- [x] Add project references so dependencies point inward:
  - Application → Domain
  - Infrastructure → Application
  - Api → Application and Infrastructure
- [x] Move the Speech SDK package to Infrastructure. Remove the `WowQuestTts` console project and the `SpeachGenerator` library.
- [x] Add `AddApplication()` and `AddInfrastructure()` extension methods, called from `Program.cs`.
- [x] Add the built-in OpenAPI document and a Scalar page at `http://localhost:5219/scalar`.
- [x] Update the README and `.gitignore`.

The API runs on HTTP only (`http://localhost:5219`), because it's local and has no login.

**Done when:** `dotnet build` passes, the API starts, and the Scalar page opens.
**Skills:** ASP.NET Core hosting, dependency injection, project references, OpenAPI.
**Commit:** `Create layered solution skeleton`

### Phase 3: Speak any text ✅

**Goal:** `POST /api/speech` with some text returns an MP3.

- [x] **Application:**
  - the `ISpeechSynthesizer` interface
  - `SpeechService`, which checks the text isn't blank or over 3,000 characters (so one request can't cost much)
  - the `SpeechRequest` DTO, with an optional voice
  - `InvalidRequestException` and `SpeechSynthesisException`
- [x] **Infrastructure:**
  - `AzureSpeechSynthesizer`: the Phase 1 code, now returning MP3 bytes instead of playing through speakers. Uses the default voice when none is given
  - `SpeechOptions`: key and region from `SPEECH_KEY` and `SPEECH_REGION`, default voice from `appsettings.json`. The API won't start if the key or region is missing
- [x] **Api:**
  - `SpeechController`, which returns `audio/mpeg`
  - `GlobalExceptionHandler`, which turns errors into ProblemDetails: 400 for bad input, 502 when Azure fails
  - `WowTts.Api.http` with sample requests

**Done when:**
- [x] A request returns an MP3 that plays.
- [x] Blank or missing text gives 400. An unknown voice gives 400 too, because Azure rejects it as a bad request.
- [x] A wrong key gives 502 with Azure's reason (authentication error 401).

**Skills:** Speech SDK in a web app, MP3 output format, options pattern, ProblemDetails.
**Commit:** `Add speech endpoint`

### Phase 4: Quests ✅

**Goal:** list quests and get each quest's audio, generated once and then served from disk.

- [x] **Domain:**
  - `Quest` and `QuestPart` (Accept, Progress, Complete), plus `Quest.GetText(part)`. A quest without an ID, title or accept text is refused
  - `TextCleaner`: `$N` → adventurer, `$R` → traveler, `$C` → hero, `$B` → line break, `$Gmale:female;` → the first form
- [x] **Application:**
  - `IQuestRepository`, `IAudioStore` and `NotFoundException`
  - `QuestService`: list quests, get one, and `GetOrCreateAudioAsync`, which uses the saved file if it exists and otherwise cleans the text, synthesizes it and saves it
  - `QuestDto` lists each part's spoken text and whether its audio exists yet
- [x] **Infrastructure:**
  - `JsonQuestRepository` reading `data/sample-quests.json` on every request, so edits show up without a restart
  - `FileAudioStore` writing `{id}-{part}.mp3` to `addon/WowTts/audio/`
- [x] **Api:** `QuestsController` with `GET /api/quests`, `GET /api/quests/{id}` and `GET /api/quests/{id}/audio/{part}`. Not found → 404, and quest parts are written as text in JSON

**Done when:**
- [x] `GET /api/quests` lists the 5 sample quests.
- [x] The first audio request creates the file (about 1.2 s).
- [x] The second request returns from the file without calling Azure (about 60 ms).
- [x] An unknown quest, or a part without text, gives 404.
- [x] No placeholder like `$N` gets read aloud.

**Skills:** domain modelling, repository pattern, caching on disk, dependency injection.
**Commit:** `Add quest endpoints with audio cache`

### Phase 5: Quest viewer ✅

**Goal:** a small local website to see all quests, read their text, see the NPC, and play each MP3.

- [x] React + TypeScript app in `web/`, built with Vite. It talks to the API through Vite's proxy (`/api` → `http://localhost:5219`), so no CORS setup is needed
- [x] Quest list with search (title, NPC, race or ID) and filters: all, with audio, missing audio. Dots show which parts have audio
- [x] Quest page: title, quest ID, a Wowhead link, quest giver name, race and gender
- [x] Each part (accept, progress, complete) shows the spoken text, its character count and an audio player
- [x] Parts without audio have a **Generate audio** button, which calls the API and shows the player once it's done
- [x] Header shows how many quests and parts with audio there are. Clear message when the API isn't running

**Done when:** ✓ you can browse the quests, read the text, play the audio, and generate missing audio from the page.
**Run:** `npm run dev` in `web/` with the API running, then open `http://localhost:5173`.
**Commit:** `Add quest viewer web app`

### Phase 6: Quest database and cMaNGOS importer ✅

**Goal:** about 4,000 real quests in a SQLite database, imported from cMaNGOS with one command.

**Result:** 4,221 quests imported in about 1.3 seconds into a 3.4 MB database. 24 quests with no text at all were skipped. A second run reports all 4,221 as unchanged. The full quest list takes about 0.4 s from the API and 1.2 s to show in the viewer.

- **Domain:**
  - `QuestSource` (Sample, CMangos, Captured), stored on each `Quest`
  - A quest now needs at least one text instead of always needing accept text: 536 vanilla turn-in quests only have completion text
- **Application:**
  - `IQuestRepository` can also save quests. `IAudioStore` can delete a quest part's audio
  - `QuestImportService`: adds new quests, updates changed ones, keeps text from a higher-priority source, and deletes MP3s whose text changed. Returns counts of what happened
- **Infrastructure:**
  - `WowTtsDbContext` and `SqliteQuestRepository` (EF Core with SQLite), replacing `JsonQuestRepository`. The database is created on first start
  - `MySqlDumpReader`: reads tables straight from the `.sql.gz` dump, without installing MySQL
  - `CMangosQuestReader`: turns quest, quest-giver, NPC and model rows into `Quest` objects. Skips unused and test quests
  - `SampleQuestReader`: reads `data/sample-quests.json`
- **Importer:** `tools/WowTts.Importer`, a console tool with two commands:
  - `cmangos [path]`: imports the dump (default: the newest file in `data/cmangos/`)
  - `samples [path]`: imports the sample quests
- **Api and viewer:** quests now come from the database, and the viewer shows each quest's source

**Done when:** one importer run fills the database, the API and viewer show the imported quests, and running it again changes nothing.
**Skills:** EF Core with SQLite, parsing a SQL dump, console tools with the .NET Generic Host, upserts.
**Commit:** `Add SQLite quest database and cMaNGOS importer`

### Phase 7: Zones, levels and paging ✅

**Goal:** every quest knows its zone, level and faction, and Quest Voices stays fast with 4,000+ quests.

**Result:** 2 continents, 49 zones and cities and 1,248 quest log header names imported in 0.2 s. Searches take 50–180 ms. Quest 783 lands at 48.2, 42.9 in Elwynn Forest, Level 1, Alliance. The viewer's zone filter uses the zone where the quest giver stands (e.g. Elwynn Forest: 73 quests), because quest log headers split zones into sub-areas such as "Northshire Valley".

- **Game data:** the importer's new `gamedata` command downloads four Classic Era client tables from [wago.tools](https://wago.tools) into `data/gamedata/` (git-ignored) and stores them in the database:
  - `AreaTable` and `QuestSort`: names for the quest log header, e.g. "Elwynn Forest", "The Deadmines", "Seasonal"
  - `UiMap` and `UiMapAssignment`: the 2 continents and 47 zone and city maps, with each map's rectangle in world coordinates
- **cMaNGOS import adds:** quest level, minimum level, faction (from `RequiredRaces`), quest log category (`ZoneOrSort`) and the quest giver's world position (`creature` and `gameobject` spawns)
- **Domain:** `WorldLocation`, `QuestSide` and `MapZone`. `MapZone` turns a world position into a map position (0–100), the same way the game does. Checked: Deputy Willem lands at 48.2, 42.0 in Elwynn Forest, as on Wowhead
- **API:**
  - `GET /api/quests` returns short summaries (no text) with search, filters (category, map zone, audio, source, faction) and paging
  - `GET /api/quests/{id}` keeps returning the full quest
  - `GET /api/map/zones` lists the continents and zones with their quest counts
- **Viewer:** filters for zone, faction and level, and loads more quests as you scroll

The database layout changes, so delete `data/wowtts.db` and run the importer again (`gamedata`, then `cmangos`).

**Commit:** `Add zones, levels and paging`

### Phase 8: Interactive world map ✅

**Goal:** a clickable map in Quest Voices. Click a zone to list its quests, and see each quest giver where they stand.

- [x] **Full-width layout:** quest list, map and quest side by side from 1400 px wide; tabs switch between map and quest on narrower screens
- [x] **API:** `GET /api/map/markers?mapZoneId=…` returns every matching quest giver as a point on that map (continent or zone), with the same filters as the list. Eastern Kingdoms: 1,979 quests at 596 spots in 0.1 s
- [x] **Continent view** (Eastern Kingdoms, Kalimdor): cropped to the area the zones cover. Every zone is clickable and shows how many quests match; cities are diamonds whose name shows on hover. All quest givers are small dots in faction colours
- [x] **Zone view:** quest givers as dots at their real map position, grouped per spot. Hover a dot for the NPC and their quests, click to open one. The selected quest gets a ring. Clicking a zone also filters the list
- [x] Zones and dots are an SVG layer (1500 × 1000, the game's 3:2 map shape), so they line up with any background image
- [x] **Map images:** the importer's `mapimages` command builds the real WoW maps as `web/public/maps/<map id>.png` (git-ignored). It reads the tile layout from the Classic Era tables (`UiMapXMapArt`, `UiMapArt`, `UiMapArtStyleLayer`, `UiMapArtTile`, `WorldMapOverlay`, `WorldMapOverlayTile`), downloads the 1,424 BLP tiles from wago.tools once, decodes them (DXT1/3/5 and palette, own decoder) and adds all 526 explored-area overlays the way the game does. 51 maps in about a minute, about 50 MB. Without the images, the map draws the zones itself
- A few subzone names are partly covered where two of the game's overlays overlap; no draw order avoids it, so it's in the game data itself
- [x] **Eastern Kingdoms alignment:** the game's rectangle for the Eastern Kingdoms continent doesn't match its Classic art (zones too high, quest givers in the sea). Fitting all 1,979 quest givers onto the land in the art gave a correction (scale 0.908, 2.7% right, 10.2% down), applied in `GameDataReader`. Quest givers on land: 79% → 96%. Kalimdor already fit (98%) and is unchanged

**Commit:** `Add interactive world map`

### Phase 9: AI voice director ✅

**Goal:** when you click **Generate audio**, an AI decides which voice fits the NPC and how the line is spoken.

- **How it decides:**
  - The first time an NPC speaks, a GPT model on **Azure OpenAI** casts their voice from the English neural voices of their gender (126 in total, 21 with styles), using their race, name and words. The choice is saved, so the NPC keeps that voice for all their quests
  - For every line it picks a **style** the voice supports (angry, cheerful, sad, whispering, shouting, hopeful…), its **strength** (0.5–2), **speed** and **pitch** (±30%), and a one-sentence **reason**
  - The answer must match a strict JSON schema. It's checked against the real voice list: an unknown voice or a style the voice doesn't have is replaced, and numbers are kept within limits
- **Domain:** `VoiceInfo`, `VoicePerformance` (with its limits) and `NpcVoice`
- **Application:** `IVoiceDirector`, `IVoiceCatalog`, `INpcVoiceRepository`, `IPerformanceRepository` and `VoiceCastingService`. `QuestService` generates audio through it, and can regenerate a part and recast an NPC
- **Infrastructure:**
  - `AzureOpenAiVoiceDirector` (OpenAI SDK 2.8.0 on Azure OpenAI's v1 API, structured output), used when `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` and `AZURE_OPENAI_DEPLOYMENT` are set. Any endpoint Foundry shows works. Reasoning effort is `low` (`VoiceDirector:ReasoningEffort` in `appsettings.json`)
  - `DefaultVoiceDirector`: the default voice per gender (Guy, Jenny), read plainly. Used without an AI, or when the AI call fails. Default voices aren't saved as castings, so the AI can still cast those NPCs later
  - `AzureVoiceCatalog`, `SsmlBuilder` (`mstts:express-as`, `prosody`, pauses between paragraphs), and the `NpcVoices` and `AudioPerformances` tables
- **API:** `DELETE /api/quests/{id}/audio/{part}` (regenerate) and `DELETE /api/quests/{id}/voice` (recast)
- **Viewer:** each part shows its voice, style, speed, pitch, whether AI or the default chose it, and the reason. **Regenerate** per part, **Recast** for the NPC
- **Checked:** Azure accepts the SSML (Davis, angry ×1.5, 10% slower, 15% lower, with `&` and `<` escaped). Without an AI, generating falls back to the default voice
- [x] **Connected:** `gpt-5-mini` on the Foundry resource `wow-agents-marija2000`. Kaltunk (quest 4641) was cast as Tony, unfriendly ×1.2, 10% slower and lower: "gruff, approving and mildly mocking". His next part kept Tony. About 3–7 seconds per line
- The test showed the complete line is Gornek's, but got Kaltunk's voice. Fixed in Phase 9.2

**Done when:** with Azure OpenAI connected, generating a quest part shows "AI" with a fitting voice, style and reason, and the same NPC keeps their voice on their next quest.
**Skills:** Azure OpenAI in Microsoft Foundry, structured outputs (JSON schema), SSML styles and prosody, prompt design.
**Commit:** `Add AI voice director`

### Phase 9.1: Dragon HD Omni voices (testing)

**Goal:** hear whether HD voices sound good enough to switch to.

- `Speech:VoiceModel` in `appsettings.json`: `Neural` (standard voices) or `DragonHDOmni`
- Azure lists only 13 English Omni voices, but most standard voices have one: `en-GB-RyanNeural` → `en-GB-Ryan:DragonHDOmniLatestNeural`. Checked one by one: 86 of the 126 English voices have one. The Multilingual and Indic voices and 4 others don't, so they're left out
- Every Omni voice has the same ~60 styles (angry, annoyed, amused, whispering, shouting, terrified…). Speed, pitch and style strength can't be changed, and paragraphs replace pauses
- An NPC keeps their person across both: Kaltunk's `en-US-TonyNeural` became `en-US-Tony:DragonHDOmniLatestNeural`
- The AI director is told when voices are HD, and gets the shared style list once instead of with every voice
- **Checked:** the free F0 resource accepts Omni voices. Kaltunk (Tony HD, amused) and Sten Stoutarm (Thomas, British HD, amused) generate in about 10 seconds. The standard versions are kept in `data/compare/` to compare by ear
- **Cost:** $22 per million characters in East US, about 2 cents a quest. The pricing page says F0's free characters don't include HD voices, so keep an eye on usage

**Done when:** you've compared both and picked one.
**Commit:** `Add Dragon HD Omni voices`

### Phase 9.2: NPC races and who hands in ✅

**Goal:** the voice director knows each NPC's race, and every line is spoken by the NPC who really says it.

- [x] **Races:** cMaNGOS stores only an NPC's model, not its race, because the server never needs it. The game client's tables (`CreatureDisplayInfo` → `CreatureDisplayInfoExtra.DisplayRaceID` → `ChrRaces`, from wago.tools) give the race of 92% of quest givers. The rest get their creature type from cMaNGOS (Humanoid, Undead, Dragonkin, Demon, Elemental, Mechanical…). `NpcRaceReader`, used by the `cmangos` import
- [x] **Who hands in:** `creature_involvedrelation` gives the NPC you hand each quest in to. 1,730 quests are handed in to someone else, who now speaks the progress and complete text. E.g. Kaltunk gives 4641, Gornek takes it back
- [x] **Domain:** `QuestNpc` (name, race, gender), `Quest.Ender` and `Quest.SpeakerOf(part)`. Casting and the voice director use the speaker
- [x] **Database:** three new columns (`EnderName`, `EnderRace`, `EnderGender`), added to an existing database on startup, so cast voices and performances stay. Re-importing deleted the 3 MP3s spoken by the wrong NPC
- [x] **API and viewer:** `ender` on a quest and `speaker` on each part. Both NPCs show with their voice and a Recast button (`DELETE /api/quests/{id}/voice?npc=ender`)
- **Checked:** Kaltunk and Gornek are Orcs; Gornek was cast as Roger (HD), "confident", Kaltunk keeps Tony

**Commit:** `Add NPC races and quest enders`

### Phase 9.3: Import AI VoiceOver audio ✅

**Goal:** vanilla quests get real race voices (a dwarf sounds like a WoW dwarf) without generating them.

- The AI VoiceOver addon's English vanilla pack (`AI_VoiceOverData_Vanilla`, about 1 GB, from CurseForge) has quest audio made with ElevenLabs clones of the game's own NPC voices, one per race and gender. Files are `generatedsoundsquests<quest id>-<accept|complete>.mp3`, with `m-`/`f-` copies for lines that change with the player's gender. Progress text isn't voiced
- Importer command `voiceover [path] [--replace]` (default `data/voiceover`, git-ignored): copies each file to `addon/WowTts/audio/<id>-<part>.mp3` and records it as **Imported** with source "AI VoiceOver". Parts that already have audio are kept unless `--replace`. The `m-` copy is used, like our `$G` handling
- `AudioImportService` (Application), `VoiceOverPackReader` (Infrastructure), `VoiceDirector.Imported`, a batch `SaveManyAsync` for performances
- Viewer: imported parts show a green **Imported** badge with the source. **Regenerate** still replaces one with our own voice
- **Checked** with a small test pack: the `m-` file wins, existing audio is kept, unknown quests and parts without text are skipped
- [x] **Imported** `AI_VoiceOverData_Vanilla-v1.0.0.zip` (CurseForge, MrThinger, 1.1 GB, extracted to `data/voiceover`): 6,427 quest parts found, **6,289 imported**, 15 kept (generated by us), 59 for quests not in the database, 64 for parts without text. 6,307 of 10,962 parts now have audio, 870 MB. What's left is mostly progress text
- [x] **Only AI VoiceOver audio now:** our own 18 generated parts were moved to `data/generated-backup/` (git-ignored), and 15 of them were filled from the pack. The 3 progress parts it doesn't voice (16, 7674, 7675) have no audio. NPC voice castings are kept for later. 6,305 parts have audio
- Personal use only: the audio is cloned from Blizzard's voice actors and has no licence, so it never goes into git

**Done when:** the pack is imported and vanilla quests play with race voices in the viewer.
**Commit:** `Import AI VoiceOver audio`

### Phase 9.4: Word timings and karaoke (zone by zone: Teldrassil done)

**Goal:** every quest line knows when each word is spoken, so the words can light up karaoke-style while it plays: first in the viewer, then in game (Phase 10).

**Where the timings come from:**

| Audio | Source | Cost | Precision |
|---|---|---|---|
| Lines we generate (Azure Speech) | Azure's `WordBoundary` events while it speaks. Works for standard and Dragon HD Omni voices | Free | Exact |
| The 6,289 imported AI VoiceOver lines (32 hours) | **Whisper large-v3-turbo on your RTX 2080 Super**, once | Free | ~0.1–0.3 s |

**Before we start (you):**
- Install NVIDIA's **CUDA Toolkit 13** (about 3 GB). Whisper.net's GPU runtime needs it; your driver (617) is new enough
- The Whisper model `ggml-large-v3-turbo` (about 1.6 GB, from Hugging Face) is downloaded into `data/models/` (git-ignored). I'll ask before downloading

**Steps:**
1. **Domain:** `WordTiming` (word, start and end in milliseconds) and `AudioTimings` (the words of one part, their source `Azure`, `Whisper` or `Estimated`, and how well they matched the text)
2. **Database:** an `AudioTimings` table, one row per quest part (words as JSON). Removed together with the audio: regenerate, delete, and imports that change the text or speaker
3. **Our own lines:** `AzureSpeechSynthesizer` listens to `WordBoundary` and returns the word timings with the MP3. `QuestService` saves them with the performance
4. **Imported lines:** importer command `timings [--force]`:
   - Whisper.net 1.9 with its CUDA runtime, `large-v3-turbo`, English, token timestamps; the model loads once
   - MP3 → 16 kHz mono WAV in memory (NAudio)
   - Only parts with audio and no timings yet, so it can be stopped and resumed; prints progress
   - About 45–90 minutes for everything. The graphics card is busy meanwhile
5. **Matching to our text:** Whisper may mishear names ("Kaltunk" → "Coltunk"), so only its timings are used. Its words are aligned with our quest text (dynamic programming, like a diff). Matched words take Whisper's times; unmatched ones are spread between their neighbours by length. A match score is saved, and lines below ~70% are listed, since their audio probably says something else
6. **API:** each part in `GET /api/quests/{id}` gets its `words` (text, start, end) and the timing source
7. **Viewer:** while a part plays, its text highlights word by word, following the audio's position. Clicking a word jumps there. A small badge shows where the timings came from

**Built and checked so far:**
- [x] CUDA Toolkit 13.4 installed
- [x] Domain `WordAligner`, the `AudioTimings` table (created in the existing database on startup), Azure `WordBoundary` timings, the `timings` importer command (Whisper.net 1.9.1 + CUDA runtime, NAudio, DTW timestamps with the large-v3-turbo heads), `timings` on each part in the API, and the viewer's karaoke text
- [x] **Azure:** a test line in Thomas (HD) got all 23 words timed (100% match). In the viewer the highlight follows the voice ("Farming" 0.5 s, "thirsty" 1.1 s, "work" 1.7 s), and clicking a word plays from there. The test line was removed again
- [x] Fixed on the way: the audio endpoint now answers range requests, so the player's slider and word clicks can jump
- [x] Downloaded `ggml-large-v3-turbo.bin` (1.6 GB) into `data/models`. Whisper runs on CUDA: about 30× faster than real time, 65% GPU load on average (81% peak), 3.2 GB video memory, up to 84 °C
- [x] **Fixed:** Whisper stopped after its first 30-second window on longer lines (quest 929 lost its last 20 words). It now listens again from just before the last word it heard, until the end
- [x] Test (20 lines) and **Teldrassil** (83 quests, 147 lines, 2 minutes): all timed, 98.3% average match, lowest 80%, none below 70%. In the viewer the highlight follows the imported voices
- [x] Whisper falls back to the processor (`Whisper.net.Runtime`) on PCs without an NVIDIA card. The README has step-by-step setup for a new PC, including CUDA and the model
- [ ] The other zones, one at a time: `timings --zone "<zone>"`, e.g. Darnassus, Darkshore, then the rest

**Done when:** all imported lines have timings, a newly generated line gets Azure's, and the viewer's highlighting stays in step with the voice.
**Skills:** Whisper.net (whisper.cpp) on CUDA, word alignment, Azure `WordBoundary` events, audio decoding with NAudio, syncing React to audio.
**Commit:** `Add word timings and karaoke preview`

### Phase 10: The addon

**Goal:** open a quest in game and hear it. Quests without audio are saved, so the API can add them.

- Create `addon/WowTts/` with `WowTts.toc` and `WowTts.lua`.
- Link that folder into the game's `Interface\AddOns\WowTts` with a directory junction, so the repo and the game share one copy.
- The API already saves the audio in `addon/WowTts/audio/`.
- In `WowTts.lua`:
  - listen for the quest window events: `QUEST_DETAIL` (accept), `QUEST_PROGRESS` (progress) and `QUEST_COMPLETE` (complete)
  - get the quest ID and play `Interface\AddOns\WowTts\audio\<id>-<part>.mp3` with `PlaySoundFile`
  - stop the sound when the quest window closes (`QUEST_FINISHED`) or a new one starts
  - save each quest's ID, title, text, NPC name and gender, zone and map position in the addon's saved data, with your character's name turned back into `$N`
  - optional: read quests without audio aloud right away with WoW's built-in text-to-speech
- **Karaoke subtitles** (needs Phase 9.4): the API writes all word timings into `addon/WowTts/WowTts_Timings.lua` (start times only, a few MB, loaded on demand). While a line plays, a subtitle bar shows the spoken text and highlights each word on time, counting from when `PlaySoundFile` started (WoW can't report a sound's position). New timings appear after a `/reload`

**Done when:** you open a quest in game and hear it, with the words lighting up as they're spoken. Closing the window stops it. Quests you open are saved after a `/reload` or logout.
**Skills:** WoW addon structure (`.toc`), Lua events, `PlaySoundFile` and `StopSound`, SavedVariables.
**Commit:** `Add WoW addon that plays and captures quests`

### Phase 11: Import captured Forever quests

**Goal:** quests saved by the addon end up in the database with source Captured.

- Importer command `captured [path]` that reads the addon's SavedVariables file (`WTF\Account\<ACCOUNT>\SavedVariables\WowTts.lua`)
- Captured text replaces cMaNGOS text for the same quest, and its old audio is deleted

**Commit:** `Import quests captured in game`

## Decisions

### Decided

| Decision | Choice | Date |
|---|---|---|
| App type | Web API instead of a console app | 7 Oct 2026 |
| API style | Controllers | 7 Oct 2026 |
| Projects | 4: Domain, Application, Infrastructure, Api | 7 Oct 2026 |
| Console app | Removed. Its code moves to Infrastructure | 7 Oct 2026 |
| Scope for starters | Speech, quests and a small addon. No tests, batch jobs or voices endpoint | 7 Oct 2026 |
| API docs page | Scalar at `/scalar`, on top of the built-in OpenAPI document | 7 Oct 2026 |
| Where the key lives | `SPEECH_KEY` and `SPEECH_REGION` environment variables | 8 Oct 2026 |
| Error handling | Exceptions + one global handler that returns ProblemDetails | 8 Oct 2026 |
| Default voice | `en-US-GuyNeural`, set in `appsettings.json` | 8 Oct 2026 |
| Player placeholders | Generic words: adventurer, traveler, hero. `$G` takes the first form | 8 Oct 2026 |
| Quest title | Not read aloud | 8 Oct 2026 |
| Quest viewer | React + TypeScript with Vite in `web/`, using Vite's proxy to reach the API | 8 Oct 2026 |
| How the addon gets audio | The API writes MP3s into the addon folder. The addon only plays local files, because WoW Lua has no network access | 7 Oct 2026 |
| Game | World of Warcraft: Forever. Addon listings show it runs a modern client (1.60.x), so the addon can use `GetQuestID`. Check in game with `/dump GetBuildInfo()` | 8 Oct 2026 |
| Quest data source | cMaNGOS classic-db for vanilla quests, plus quests captured in game for Forever's new and changed ones | 8 Oct 2026 |
| Quest storage | SQLite file `data/wowtts.db` through EF Core, git-ignored. Audio stays as MP3 files | 8 Oct 2026 |
| Importer | Console tool `tools/WowTts.Importer` | 8 Oct 2026 |
| Word timings | Azure `WordBoundary` for lines we generate; Whisper large-v3-turbo on your GPU, once, for the imported ones | 10 Oct 2026 |
| Text priority | Captured > CMangos > Sample. Changed text deletes the old MP3s | 8 Oct 2026 |
| Zone and map data | Classic Era client tables from wago.tools: `AreaTable`, `QuestSort`, `UiMap`, `UiMapAssignment` | 8 Oct 2026 |
| Map style | Real WoW map images as the background, built by the importer from the game's own map tiles (via wago.tools). Zones and quest givers drawn on top. No downloading from Wowhead | 9 Oct 2026 |
| NPC race | From the game client's model tables (92% of quest givers), else the creature type from cMaNGOS | 9 Oct 2026 |
| Who speaks progress and complete | The NPC you hand the quest in to (`creature_involvedrelation`), when that's not the quest giver | 9 Oct 2026 |
| New database columns | Added in place on startup (`DatabaseSetup.AddedColumns`), so cast voices and performances survive. Migrations later, if needed | 9 Oct 2026 |
| Voice model | A switch between standard and Dragon HD Omni voices (`Speech:VoiceModel`). Omni is on while you compare | 9 Oct 2026 |
| Voice director model | `gpt-5-mini` on Foundry, through Azure OpenAI's v1 API with the OpenAI SDK, reasoning effort low | 9 Oct 2026 |
| Azure resources | Speech stays on the free F0 resource `wow-speech-marija2000`. The Foundry resource `wow-agents-marija2000` (East US) only hosts the GPT model for the voice director. Moving Speech to Foundry later is just a change of `SPEECH_KEY` and `SPEECH_REGION` | 9 Oct 2026 |
| Ready-made audio packs | AI VoiceOver's English vanilla pack is imported for personal use (real race voices). Its audio is cloned from Blizzard's voice actors and has no licence, so it stays out of git. Everything it doesn't cover (progress text, Forever's new and changed quests) is generated | 10 Oct 2026 (was: not used, 8 Oct) |

### Open

| # | Decision | Options | Suggestion | Needed by |
|---|---|---|---|---|
| 8 | How the addon folder reaches the game | Directory junction from the repo, copy by hand | Junction. One copy, and new audio appears in the game folder at once | Phase 10 |
| 12 | Where the karaoke shows in game | A subtitle bar, or colouring the words in the quest window itself | Subtitle bar. It shows the text that's actually spoken, even where Forever changed the quest window's text | Phase 10 |
| 11 | Where Forever is installed | No WoW install was found on this PC | Install it here, or tell me where it is. Needed for the addon | Phase 10 |

## Risks

| Risk | How we handle it |
|---|---|
| The CUDA Toolkit won't install or Whisper won't use the GPU | Whisper.net falls back to the processor: `small.en` takes about 4–6 hours instead. Or the Vulkan runtime |
| Some imported audio doesn't say exactly our text | Alignment uses only Whisper's timings and spreads unmatched words; low match scores are listed for a look |
| Timings drift in game, because WoW can't tell how far a sound has played | Count from the moment `PlaySoundFile` starts; a line is ~18 seconds, so drift stays small |
| Costs add up | Maximum text length on `/api/speech`, and quest audio is only generated once |
| Anyone who can reach the API spends your Azure credit | Runs on localhost only. No hosting until authentication is added |
| Placeholders get read aloud ("dollar N") | `TextCleaner` handles them in the Domain layer |
| The addon expects to call the API | It can't: WoW Lua has no network access. The API writes files, the addon only plays them |
| New audio isn't found in game | WoW only sees files that existed at startup. Generate first, then start the game |
| Forever changed 616 quests, so cMaNGOS text is outdated for them | The addon captures Forever's own text, and captured text wins over cMaNGOS |
| The free F0 Speech tier allows only a few requests per minute and a monthly character allowance | Fine while generating quest by quest. Before generating thousands at once, upgrade Speech to S0 or move it to the Foundry resource |
| A continent's map rectangle doesn't match its art (happened with Eastern Kingdoms) | Measure by fitting quest givers onto the land in the art, and correct it in `GameDataReader` |
| wago.tools has no Forever data yet, so zones and maps are Classic Era's | Forever's 3 new zones come from the addon's captures. Re-import the tables once Forever's appear |
| Old audio no longer matches changed text | Imports delete the MP3s of every part whose text changed |
| cMaNGOS has later fixes in its `Updates` folder that the full dump doesn't include | Quest text rarely changes there. Re-download the newest dump now and then |
| Quest text belongs to Blizzard | Personal, private project. The database and dump are git-ignored, so the repo holds no Blizzard text |
| The Speech key leaks | Key only in environment variables, never in a file |

## Later, not in this plan

Once speech and quests work, these are the natural next steps:

- More in the quest viewer: regenerate audio, edit quest text, add quests
- A voice for every NPC: race and gender pick the voice, built with SSML
- `GET /api/voices` and comparing Dragon HD voices
- Generating audio for all quests in one request, then batch jobs with a cost estimate
- Tests: unit tests for the Domain, and API tests with a fake synthesizer
- Deploying to Azure: App Service, Key Vault, managed identity, plus authentication

## How we work

- One phase at a time. I write the code and explain it, you run it.
- Every phase ends with a working API, a commit and a push.
- When a decision changes, this plan gets updated.

## Next step

1. Install the CUDA Toolkit 13, then build word timings and the karaoke preview (Phase 9.4).
2. Compare the standard and HD voices, and the imported AI VoiceOver ones, and pick (Phase 9.1).
3. Decision 11: where Forever is installed, for the addon (Phase 10).

## Notes

- Terminal tabs in the Claude app only see environment variables that existed when the app started. After a `setx`, either restart the app or load the variables into the open terminal:
  `$env:SPEECH_KEY = [Environment]::GetEnvironmentVariable('SPEECH_KEY','User'); $env:SPEECH_REGION = [Environment]::GetEnvironmentVariable('SPEECH_REGION','User')`
