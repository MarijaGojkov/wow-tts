namespace WowTts.Infrastructure.Storage;

// The "Audio" section of appsettings.json. A relative path is relative to the Api project folder.
// It points at the addon's audio folder, so the game can play the files.
public sealed class AudioStorageOptions
{
    public const string SectionName = "Audio";

    public string Folder { get; set; } = "../../addon/WowTts/audio";
}
