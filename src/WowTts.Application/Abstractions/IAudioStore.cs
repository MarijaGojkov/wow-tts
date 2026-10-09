using WowTts.Domain.Quests;

namespace WowTts.Application.Abstractions;

// One saved audio file: a part of a quest.
public readonly record struct AudioKey(int QuestId, QuestPart Part);

// Where generated quest audio is kept. Infrastructure saves it as MP3 files in the addon's audio folder.
public interface IAudioStore
{
    bool Exists(int questId, QuestPart part);

    // Everything saved so far, in one go. Much faster than asking Exists for thousands of quest parts.
    IReadOnlySet<AudioKey> ListAll();

    // Returns null when there's no saved audio yet.
    Task<byte[]?> TryReadAsync(int questId, QuestPart part);

    Task SaveAsync(int questId, QuestPart part, byte[] audio);

    // Removes saved audio, e.g. when the quest text changed and the audio no longer matches.
    void Delete(int questId, QuestPart part);
}
