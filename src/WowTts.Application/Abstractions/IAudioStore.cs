using WowTts.Domain.Quests;

namespace WowTts.Application.Abstractions;

// Where generated quest audio is kept. Infrastructure saves it as MP3 files in the addon's audio folder.
public interface IAudioStore
{
    bool Exists(int questId, QuestPart part);

    // Returns null when there's no saved audio yet.
    Task<byte[]?> TryReadAsync(int questId, QuestPart part);

    Task SaveAsync(int questId, QuestPart part, byte[] audio);
}
