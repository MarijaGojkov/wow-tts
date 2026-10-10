namespace WowTts.Domain.Speech;

// A word as a speech service spoke or heard it, and when: milliseconds from the start of the audio.
public sealed record HeardWord(string Text, int StartMs, int EndMs);

// One word of a line's text: where it is in the text (so it can be highlighted) and when it's spoken.
public sealed record WordTiming(int Offset, int Length, int StartMs, int EndMs);

// Where word timings came from: Azure Speech while it spoke the line, or Whisper listening to it afterwards.
public enum TimingSource
{
    Azure,
    Whisper
}

// When each word of one quest part's audio is spoken. Match is the share of the text's words the source
// confirmed, from 0 to 1; the other words got times spread between their neighbours.
public sealed record AudioTimings(TimingSource Source, double Match, IReadOnlyList<WordTiming> Words);
