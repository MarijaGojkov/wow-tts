using Microsoft.CognitiveServices.Speech;

// The key and region live in environment variables, so they never end up in the code or on GitHub.
string? key = Environment.GetEnvironmentVariable("SPEECH_KEY");
string? region = Environment.GetEnvironmentVariable("SPEECH_REGION");

if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
{
    Console.Error.WriteLine("SPEECH_KEY and SPEECH_REGION must both be set.");
    Console.Error.WriteLine("Set them with setx, then open a new terminal so it picks them up.");
    return 1;
}

var config = SpeechConfig.FromSubscription(key, region);
config.SpeechSynthesisVoiceName = "en-US-GuyNeural";

const string questLine =
    "Greetings, adventurer. Kobolds have been stealing candles from the Echo Ridge Mine. " +
    "Head north and drive them out before they reach the abbey.";

Console.WriteLine($"Speaking with {config.SpeechSynthesisVoiceName} in {region}...");

// With no audio config, the synthesizer plays through the default speakers.
using var synthesizer = new SpeechSynthesizer(config);
using var result = await synthesizer.SpeakTextAsync(questLine);

if (result.Reason == ResultReason.SynthesizingAudioCompleted)
{
    Console.WriteLine($"Done. Spoke {questLine.Length} characters.");
    return 0;
}

if (result.Reason == ResultReason.Canceled)
{
    var details = SpeechSynthesisCancellationDetails.FromResult(result);
    Console.Error.WriteLine($"Speech was canceled: {details.Reason}");

    if (details.Reason == CancellationReason.Error)
    {
        Console.Error.WriteLine($"Error code: {details.ErrorCode}");
        Console.Error.WriteLine($"Details:    {details.ErrorDetails}");
        Console.Error.WriteLine("Check that SPEECH_KEY and SPEECH_REGION match your Speech resource in the Azure portal.");
    }
    return 1;
}

Console.Error.WriteLine($"Unexpected result: {result.Reason}");
return 1;
