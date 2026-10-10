namespace WowTts.Infrastructure.Speech;

// Settings for Azure Speech. Key and Region come from the SPEECH_KEY and SPEECH_REGION environment
// variables, so the key never ends up in a file. The voices come from the "Speech" section of appsettings.json.
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    public string Key { get; set; } = "";
    public string Region { get; set; } = "";

    // For POST /api/speech when no voice is given.
    public string DefaultVoice { get; set; } = "en-US-GuyNeural";

    // For quest lines when no AI voice director is set up (or it fails).
    public string DefaultMaleVoice { get; set; } = "en-US-GuyNeural";
    public string DefaultFemaleVoice { get; set; } = "en-US-JennyNeural";

    // Which voices quest lines use:
    //   "Neural"        standard voices: a few have styles, and speed and pitch can be changed
    //   "DragonHDOmni"  HD versions of the same voices: more natural, every voice has the same ~60 styles,
    //                   but speed, pitch and style strength can't be changed. Billed at the HD price
    public string VoiceModel { get; set; } = Neural;

    public const string Neural = "Neural";
    public const string DragonHDOmni = "DragonHDOmni";

    public bool UsesHdVoices => VoiceModel.Equals(DragonHDOmni, StringComparison.OrdinalIgnoreCase);
}
