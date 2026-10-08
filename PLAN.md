# wow-tts implementation plan

**Updated:** 7 October 2026
**Status:** Phases 0–5 are done. Next up is Phase 6, the addon.
**Scope for starters:** speech, quests and a small addon. No tests, batch jobs or voices endpoint yet.

## Goal

A small ASP.NET Core Web API, built with onion (clean) architecture, that turns World of Warcraft quest text into speech with Azure AI Speech, plus a small WoW addon that plays it.

When it's finished:

- `POST /api/speech` turns any text into an MP3.
- `GET /api/quests` lists the quests, and `GET /api/quests/{id}` shows one.
- `GET /api/quests/783/audio/accept` returns a quest's audio. It's generated on the first request and saved, so Azure is only paid once per text.
- A small local website, Quest Voices, shows every quest with its text, the NPC and an audio player.
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
| **Infrastructure** | Azure Speech SDK, audio files in the addon folder, quest JSON file | Application, Domain | Yes |
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

This is where the solution ends up after Phase 6.

```
wow-tts/
├─ WowTts.sln
├─ src/
│  ├─ WowTts.Domain/
│  │  ├─ Quests/            Quest, QuestPart
│  │  └─ Speech/            TextCleaner
│  ├─ WowTts.Application/
│  │  ├─ Abstractions/      ISpeechSynthesizer, IQuestRepository, IAudioStore
│  │  ├─ Speech/            SpeechService, SpeechRequest
│  │  ├─ Quests/            QuestService, QuestDto
│  │  └─ DependencyInjection.cs
│  ├─ WowTts.Infrastructure/
│  │  ├─ Speech/            AzureSpeechSynthesizer, SpeechOptions
│  │  ├─ Quests/            JsonQuestRepository
│  │  ├─ Storage/           FileAudioStore
│  │  └─ DependencyInjection.cs
│  └─ WowTts.Api/
│     ├─ Controllers/       SpeechController, QuestsController
│     ├─ ErrorHandling/     GlobalExceptionHandler
│     ├─ Program.cs
│     ├─ appsettings.json
│     └─ WowTts.Api.http    sample requests you can click to run
├─ web/                     Quest Voices: React + Vite quest viewer
│  └─ src/                  App, QuestList, QuestDetail, api.ts
├─ addon/
│  └─ WowTts/               the WoW addon, linked into Interface\AddOns\WowTts
│     ├─ WowTts.toc         addon name, version, files to load
│     ├─ WowTts.lua         plays audio when a quest window opens
│     └─ audio/             generated MP3s (git-ignored)
├─ data/
│  └─ sample-quests.json    5 hand-written test quests
├─ docs/plan.html
├─ PLAN.md
└─ README.md
```

## Quest data

Each quest in `data/sample-quests.json` has these fields:

| Field | Example | Notes |
|---|---|---|
| `id` | `783` | Quest ID, used in routes and file names |
| `title` | A Threat Within | Not read aloud (see decision 6) |
| `npcName` | Deputy Willem | Quest giver |
| `npcRace` | Human | Stored now, used later for NPC voices |
| `npcGender` | Male | Stored now, used later for NPC voices |
| `acceptText` | What the NPC says when you take the quest | Required |
| `progressText` | What the NPC says if you return before finishing | Optional |
| `completeText` | What the NPC says when you hand the quest in | Optional |

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

### Phase 6: The addon

**Goal:** open a quest in game and hear it.

- Create `addon/WowTts/` with `WowTts.toc` and `WowTts.lua`.
- Link that folder into the game's `Interface\AddOns\WowTts` with a directory junction, so the repo and the game share one copy.
- The API already saves the audio in `addon/WowTts/audio/`.
- In `WowTts.lua`:
  - listen for the quest window events: `QUEST_DETAIL` (accept), `QUEST_PROGRESS` (progress) and `QUEST_COMPLETE` (complete)
  - get the quest ID and play `Interface\AddOns\WowTts\audio\<id>-<part>.mp3` with `PlaySoundFile`
  - stop the sound when the quest window closes (`QUEST_FINISHED`) or a new one starts
- Add sample quests to `data/sample-quests.json` whose IDs match real quests you can open in game, so the test works for real.

**Done when:** you generate a quest's audio, start the game, open that quest and hear it. Closing the window stops it.
**Skills:** WoW addon structure (`.toc`), Lua events, `PlaySoundFile` and `StopSound`.
**Commit:** `Add WoW addon that plays quest audio`

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

### Open

| # | Decision | Options | Suggestion | Needed by |
|---|---|---|---|---|
| 7 | Which WoW client "WoW Forever" runs | Modern Classic client (1.14+), original 1.12 client | Tell me which. A 1.12 client has no `GetQuestID`, so the addon would match quests by title instead | Phase 6 |
| 8 | How the addon folder reaches the game | Directory junction from the repo, copy by hand | Junction. One copy, and new audio appears in the game folder at once | Phase 6 |

## Risks

| Risk | How we handle it |
|---|---|
| Costs add up | Maximum text length on `/api/speech`, and quest audio is only generated once |
| Anyone who can reach the API spends your Azure credit | Runs on localhost only. No hosting until authentication is added |
| Placeholders get read aloud ("dollar N") | `TextCleaner` handles them in the Domain layer |
| The addon expects to call the API | It can't: WoW Lua has no network access. The API writes files, the addon only plays them |
| New audio isn't found in game | WoW only sees files that existed at startup. Generate first, then start the game |
| The game client is too old for `GetQuestID` | Decision 7. Fall back to matching by quest title |
| Quest text belongs to Blizzard | Sample quests are hand-written. The repo stays private |
| The Speech key leaks | Key only in environment variables, never in a file |

## Later, not in this plan

Once speech and quests work, these are the natural next steps:

- More in the quest viewer: regenerate audio, edit quest text, add quests
- WoW's built-in text-to-speech as a fallback for quests without audio, with the addon saving missing quests and their text so the API can generate them (needs a modern client)
- A voice for every NPC: race and gender pick the voice, built with SSML
- `GET /api/voices` and comparing Dragon HD voices
- Generating audio for all quests in one request, then batch jobs with a cost estimate
- Tests: unit tests for the Domain, and API tests with a fake synthesizer
- Real quest data for the WoW version you play
- Deploying to Azure: App Service, Key Vault, managed identity, plus authentication

## How we work

- One phase at a time. I write the code and explain it, you run it.
- Every phase ends with a working API, a commit and a push.
- When a decision changes, this plan gets updated.

## Next step

1. Phase 6: the addon.
2. Answer decision 7: which client does "WoW Forever" run? It's needed by Phase 6.

## Notes

- Terminal tabs in the Claude app only see environment variables that existed when the app started. After a `setx`, either restart the app or load the variables into the open terminal:
  `$env:SPEECH_KEY = [Environment]::GetEnvironmentVariable('SPEECH_KEY','User'); $env:SPEECH_REGION = [Environment]::GetEnvironmentVariable('SPEECH_REGION','User')`
