# wow-tts implementation plan

**Updated:** 7 October 2026
**Status:** Phases 0 and 1 are done. Next up is Phase 2.

## Goal

A C# console app that reads World of Warcraft quest text from a file and uses Azure AI Speech to create one audio file per quest text, in a voice that fits the quest giver.

When it's finished, one command like this does the whole job:

```
dotnet run --project WowQuestTts -- --input data/quests.json --limit 50
```

It will:

- estimate the cost before spending anything
- create `output/<questId>-accept.mp3`, `-progress.mp3` and `-complete.mp3` for each quest
- skip files that already exist, so an interrupted run continues where it stopped
- list any quests that failed, so you can retry just those

## How it works

```
quests.json → Load → Clean text → Pick voice → Build SSML → Azure Speech → MP3 files + report
```

| Step | What it does | Class |
|---|---|---|
| Load | Reads the quest file into quest objects | `QuestLoader` |
| Clean text | Replaces WoW placeholders such as `$N`, `$B` and `$G` with plain words or pauses | `TextCleaner` |
| Pick voice | Chooses a voice from the quest giver's race and gender | `VoicePicker` |
| Build SSML | Wraps the text in SSML with the chosen voice, pauses and speaking style | `SsmlBuilder` |
| Azure Speech | Sends the SSML to your Speech resource and gets audio back, retrying when throttled | `SpeechGenerator` |
| Save and report | Writes the files, skips finished ones, counts characters, logs failures | `BatchRunner` |

## Quest data

Each quest in our JSON file has these fields:

| Field | Example | Notes |
|---|---|---|
| `id` | `783` | Quest ID, used in file names |
| `title` | A Threat Within | Not read aloud (see decision 5) |
| `npcName` | Deputy Willem | Quest giver |
| `npcRace` | Human | Used to pick the voice |
| `npcGender` | Male | Used to pick the voice |
| `acceptText` | What the NPC says when you take the quest | Required |
| `progressText` | What the NPC says if you return before finishing | Optional |
| `completeText` | What the NPC says when you hand the quest in | Optional |

## Project layout

This is where the project ends up after Phase 6. Files are only added in the phase that needs them.

```
wow-tts/
├─ WowQuestTts/
│  ├─ Program.cs            entry point: reads options, starts the run
│  ├─ Options.cs            command-line options
│  ├─ Models/Quest.cs       the quest data model
│  ├─ QuestLoader.cs        reads the quest JSON file
│  ├─ TextCleaner.cs        replaces $N, $B, $G and other placeholders
│  ├─ VoicePicker.cs        race + gender → voice name
│  ├─ SsmlBuilder.cs        builds the SSML for one text
│  ├─ SpeechGenerator.cs    calls Azure Speech, retries on throttling
│  └─ BatchRunner.cs        loops over quests, skips done files, reports
├─ WowQuestTts.Tests/       unit tests for cleaner, voice picker and SSML
├─ data/
│  ├─ sample-quests.json    5 test quests
│  └─ settings.json         voices, player profile, audio format, price
├─ output/                  generated audio (git-ignored)
├─ PLAN.md
└─ README.md
```

## Phases

Every phase ends with an app that runs, a commit and a push.

### Phase 0: Setup ✅

- [x] .NET 9 console app `WowQuestTts` with `Microsoft.CognitiveServices.Speech` 1.52.0
- [x] `SPEECH_KEY` and `SPEECH_REGION=eastus` set as user environment variables
- [x] Private GitHub repo: `MarijaGojkov/wow-tts`
- [x] Confirm the Speech resource exists and `SPEECH_KEY` is its key (proved by Phase 1)

### Phase 1: First words ✅

**Goal:** hear one quest line through your speakers.

- [x] Read `SPEECH_KEY` and `SPEECH_REGION`. Stop with a clear message if either is missing.
- [x] Create the speech config and a synthesizer with a standard neural voice (`en-US-GuyNeural`).
- [x] Speak one hard-coded quest line.
- [x] Check the result. On failure, print the reason and the error details Azure sends back.

**Done when:** you hear the line, and a wrong key prints a readable error instead of a crash.
**Azure skills:** `SpeechConfig`, `SpeechSynthesizer`, result reasons, cancellation details.
**Commit:** `Speak one quest line`

### Phase 2: Save to a file

**Goal:** write the audio to a file instead of the speakers.

- Set the output format to MP3.
- Create `output/` if it's missing and write `output/test.mp3`.
- Print the file path and size.

**Done when:** `output/test.mp3` plays in a media player and git doesn't pick it up.
**Azure skills:** audio config, output formats.
**Commit:** `Save speech to an MP3 file`

### Phase 3: Choose the voice

**Goal:** hear the difference between voice models and pick yours.

- Add a `--list-voices` option that asks the service which voices East US offers and prints the English ones, marking the HD voices.
- Generate the same quest line with a standard neural voice, a Dragon HD Flash voice and a Dragon HD voice.
- Listen, then pick a narrator voice plus voices for male and female NPCs.
- Save your picks in `data/settings.json`.

**Done when:** the three comparison files exist and `settings.json` holds your picks.
**Azure skills:** listing voices, HD voice models, the cost versus quality trade-off.
**Commit:** `Compare voices and save voice settings`

### Phase 4: Quests from a file

**Goal:** generate audio for several quests from a JSON file.

- Add the `Quest` model and `QuestLoader`.
- Create `data/sample-quests.json` with 5 quests from different races.
- Add `TextCleaner`:
  - `$N` becomes the player name from settings.
  - `$R` becomes the race and `$C` the class.
  - `$B` becomes a pause.
  - `$Gmale:female;` picks the form that matches the player gender in settings.
- Write one file per quest text, e.g. `783-accept.mp3`. Skip texts that are empty.
- Add the `WowQuestTts.Tests` xUnit project with tests for `TextCleaner`.

**Done when:** one run creates the files for all 5 sample quests, and the cleaner tests pass.
**Skills:** C# records, `System.Text.Json`, file handling, unit tests.
**Commit:** `Generate audio for quests from a JSON file`

### Phase 5: A voice for every NPC

**Goal:** quest givers of different races and genders sound different.

- Add `VoicePicker`: race + gender → voice from `settings.json`, with the narrator as fallback.
- Add `SsmlBuilder`:
  - wraps the cleaned text in SSML with the chosen voice
  - escapes `&`, `<` and `>`
  - turns `$B` pauses into `<break>` elements
  - optionally sets speaking rate and pitch per race, e.g. slower and lower for orcs
- Switch from plain-text synthesis to SSML synthesis.
- Add tests for the voice picker fallback and SSML escaping.

**Done when:** sample quests from different races sound different, and text containing `&` or `<` works.
**Azure skills:** SSML `<voice>`, `<break>` and `<prosody>`.
**Commit:** `Pick NPC voices and build SSML`

### Phase 6: Ready for thousands

**Goal:** the app can run for hours, survive interruptions and tell you the cost up front.

- Command-line options: `--input`, `--output`, `--limit N`, `--only <questId>`, `--dry-run`, `--force`, `--list-voices`.
- Skip files that already exist, unless `--force` is given.
- Retry throttled requests (HTTP 429) and temporary errors with growing waits. After a few failed tries, log the failure and move on.
- Send one request at a time first. Add a few parallel requests only if it's too slow.
- Show a progress line for each file, e.g. `[120/4000] 783-accept.mp3`.
- `--dry-run` counts characters and estimates the cost from the price in settings, without calling Azure.
- At the end, print how many files were created, skipped and failed. Write failures to `output/failed.json`.

**Done when:** a run that you stop halfway continues where it stopped when you start it again, and the dry run prints a character count and cost estimate.
**Azure skills:** service limits and throttling, retry with backoff, pricing tiers, cost estimation.
**Commit:** `Make batch runs resumable and cost-aware`

### Phase 7: Real quest data

**Goal:** audio for every quest in the game version you play.

- Choose the data source (decisions 1 and 8).
- Write a converter from that source into our quest format.
- Work out each quest giver's race and gender. Use the narrator voice when unknown.
- Check the data for missing texts, leftover placeholders and very long texts.
- Set a budget alert in Azure Cost Management.
- Run one zone first, e.g. Elwynn Forest. Compare the real cost in the portal with the estimate, then run the rest.

**Done when:** every quest has audio, and the real cost matches the estimate.
**Azure skills:** Cost Management, budgets and alerts.
**Commit:** `Import real quest data`

### Phase 8: Play it in game (optional)

**Goal:** hear the audio when a quest window opens.

- Look at how the VoiceOver addon names and loads its audio files.
- Match its folder layout, file names and audio format. That may mean OGG instead of MP3.
- For your own use only. Don't publish the audio.

**Done when:** opening a quest in game plays its audio.

## Decisions

| # | Decision | Options | Suggestion | Needed by |
|---|---|---|---|---|
| 1 | Which WoW version | Classic Era, a 1.12 private server, something else | Tell me which one "WoW Forever" is | Phase 7 |
| 2 | Voice model | Standard neural, Dragon HD Flash, Dragon HD | Flash, unless HD sounds clearly better | Phase 3 |
| 3 | Pricing tier | Free F0, Standard S0 | F0 for Phases 1–5, S0 for the full run | Phase 6 |
| 4 | Player placeholders (`$N`, `$R`, `$C`, `$G`) | Generic words like "adventurer", or your character | Generic, so the audio fits any character | Phase 4 |
| 5 | Read the quest title aloud | Yes, No | No, NPCs don't say it in game | Phase 5 |
| 6 | Audio format | MP3, OGG Opus | MP3 for now, switch if the addon needs OGG | Phase 8 |
| 7 | Command-line parsing | Hand-written, `System.CommandLine` | Hand-written, there are only a few options | Phase 6 |
| 8 | Quest data source | Private-server database export, addon data files, hand-made | Depends on decision 1 | Phase 7 |

## Risks

| Risk | How we handle it |
|---|---|
| The full run costs more than expected | Dry-run estimate, `--limit`, one zone first, budget alert |
| Azure throttles requests (HTTP 429) | Retry with growing waits, few parallel requests |
| HD voices aren't offered in East US | Check in Phase 3. If needed, add a Speech resource in another region |
| Placeholders get read aloud ("dollar N") | `TextCleaner` with tests, and the Phase 7 data check flags leftovers |
| `&` or `<` in quest text breaks the SSML | Escape all text, with a test for it |
| Quest text and audio belong to Blizzard | Private repo, real data exports git-ignored, audio never published |
| The Speech key leaks | Key only in environment variables, never in a file |

## Out of scope

- A graphical interface
- Generating speech live while you play
- Custom cloned voices (Custom Neural Voice needs approval from Microsoft)
- Other languages (possible later by adding voices)

## How we work

- One phase at a time. I write the code and explain it, you run it.
- Every phase ends with a working app, a commit and a push.
- When a decision changes, this plan gets updated.

## Next step

1. Start Phase 2: save the audio to an MP3 file.
2. Answer decision 1 when you can: which WoW version is "WoW Forever"? It's needed by Phase 7.

## Notes

- Terminal tabs in the Claude app only see environment variables that existed when the app started. After a `setx`, either restart the app or load the variables into the open terminal:
  `$env:SPEECH_KEY = [Environment]::GetEnvironmentVariable('SPEECH_KEY','User'); $env:SPEECH_REGION = [Environment]::GetEnvironmentVariable('SPEECH_REGION','User')`
