# wow-tts

A C# console app that turns World of Warcraft quest text into speech using Azure AI Speech.

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
dotnet run --project WowQuestTts
```
