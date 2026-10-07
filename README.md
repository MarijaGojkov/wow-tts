# wow-tts

An ASP.NET Core Web API that turns World of Warcraft quest text into speech using Azure AI Speech, plus a small WoW addon that plays the audio in game. See [PLAN.md](PLAN.md) for the full plan.

## Solution

Onion architecture: dependencies point inward.

| Project | Layer | References |
|---|---|---|
| `src/WowTts.Domain` | WoW rules: quests, text cleaning | nothing |
| `src/WowTts.Application` | Services (use cases) and the interfaces they need | Domain |
| `src/WowTts.Infrastructure` | Azure Speech, audio files, quest data | Application |
| `src/WowTts.Api` | Controllers, OpenAPI, wiring | Application, Infrastructure |

## Requirements

- .NET 9 SDK
- An Azure Speech resource
- These environment variables:

```powershell
setx SPEECH_KEY "<your key>"
setx SPEECH_REGION "eastus"
```

Open a new terminal after running `setx`.

## Run

```powershell
dotnet run --project src/WowTts.Api
```

Then open http://localhost:5219/scalar to see and try the endpoints. The sample requests in `src/WowTts.Api/WowTts.Api.http` can be run from Visual Studio or VS Code.
