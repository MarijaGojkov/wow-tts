using System.Text.Json;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Persistence;

// One row of the AudioTimings table: when each word of a quest part's audio is spoken.
// The words are stored as compact JSON, [[offset, length, start ms, end ms], …], about 15 bytes a word.
public sealed class AudioTimingsEntity
{
    public int QuestId { get; set; }
    public QuestPart Part { get; set; }
    public TimingSource Source { get; set; }
    public double Match { get; set; }
    public string Words { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }

    public AudioTimings ToDomain() => new(
        Source,
        Match,
        JsonSerializer.Deserialize<int[][]>(Words)!.Select(w => new WordTiming(w[0], w[1], w[2], w[3])).ToList());

    public void CopyFrom(AudioTimings timings)
    {
        Source = timings.Source;
        Match = timings.Match;
        Words = JsonSerializer.Serialize(timings.Words.Select(w => new[] { w.Offset, w.Length, w.StartMs, w.EndMs }));
        CreatedAt = DateTime.UtcNow;
    }
}
