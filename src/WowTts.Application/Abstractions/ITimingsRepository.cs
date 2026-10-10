using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Abstractions;

// When each word of a quest part's audio is spoken. Kept as long as the audio it belongs to.
public interface ITimingsRepository
{
    Task<IReadOnlyDictionary<QuestPart, AudioTimings>> GetForQuestAsync(int questId);

    // Every quest part that has timings, in one go.
    Task<IReadOnlySet<AudioKey>> ListAllAsync();

    Task SaveAsync(int questId, QuestPart part, AudioTimings timings);
    Task DeleteAsync(int questId, QuestPart part);
    Task DeleteManyAsync(IReadOnlyCollection<AudioKey> parts);
}

// Listens to audio and reports the words it hears and when, e.g. Whisper.
public interface IWordRecognizer
{
    TimingSource Source { get; }

    Task<IReadOnlyList<HeardWord>> RecognizeAsync(byte[] mp3, CancellationToken cancel);
}
