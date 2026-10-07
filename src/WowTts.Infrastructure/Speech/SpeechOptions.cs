namespace WowTts.Infrastructure.Speech;

// Settings for Azure Speech. Key and Region come from the SPEECH_KEY and SPEECH_REGION environment
// variables, so the key never ends up in a file. DefaultVoice comes from the "Speech" section of appsettings.json.
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public string Key { get; set; } = "";
    public string Region { get; set; } = "";
    public string DefaultVoice { get; set; } = "en-US-GuyNeural";
}
