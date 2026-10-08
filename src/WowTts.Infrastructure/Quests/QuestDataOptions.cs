namespace WowTts.Infrastructure.Quests;

// The "Quests" section of appsettings.json. A relative path is relative to the Api project folder.
public sealed class QuestDataOptions
{
    public const string SectionName = "Quests";

    public string DataFile { get; set; } = "../../data/sample-quests.json";
}
