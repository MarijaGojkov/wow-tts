using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;
using Whisper.net.LibraryLoader;
using WowTts.Application.Abstractions;
using WowTts.Domain.Speech;

// Whisper (whisper.cpp, through Whisper.net) listening to quest audio and reporting each word with its time.
// It runs on the graphics card through CUDA when it can (Whisper.net falls back to the processor otherwise), and
// uses DTW timestamps, which follow the audio more closely than Whisper's ordinary token times.
// Lives in the importer rather than Infrastructure, so the API doesn't carry Whisper's large native libraries.
public sealed class WhisperWordRecognizer : IWordRecognizer, IDisposable
{
    private const int SampleRate = 16000;   // what Whisper listens at

    private readonly WhisperFactory _factory;
    private readonly WhisperProcessor _processor;

    public WhisperWordRecognizer(string modelPath)
    {
        _factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions
        {
            UseGpu = true,
            UseDtwTimeStamps = true,
            HeadsPreset = WhisperAlignmentHeadsPreset.LargeV3Turbo,
        });
        _processor = _factory.CreateBuilder()
            .WithLanguage("en")
            .WithTokenTimestamps()
            .Build();
    }

    public TimingSource Source => TimingSource.Whisper;

    // Which native library Whisper.net loaded: Cuda when the graphics card is used.
    public string Runtime => RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown";

    // Whisper sometimes stops after its first 30-second window and drops the rest of a longer line. So when it stops
    // well before the end, it listens again from just before the last word it heard (that word may have been cut off,
    // so it's dropped and heard again), until it reaches the end. Words heard twice in that overlap are dropped.
    public async Task<IReadOnlyList<HeardWord>> RecognizeAsync(byte[] mp3, CancellationToken cancel)
    {
        const int SamplesPerMs = SampleRate / 1000;
        const int MaxPasses = 6;

        float[] samples = Decode(mp3);
        int totalMs = samples.Length / SamplesPerMs;
        const int OverlapMs = 400;

        var words = new List<HeardWord>();
        int offsetMs = 0, keepFromMs = 0;

        for (int pass = 0; pass < MaxPasses; pass++)
        {
            var heard = await RecognizeSamplesAsync(samples[(offsetMs * SamplesPerMs)..], cancel);
            words.AddRange(heard
                .Select(word => word with { StartMs = word.StartMs + offsetMs, EndMs = word.EndMs + offsetMs })
                .Where(word => word.StartMs >= keepFromMs - 150));

            if (heard.Count < 2 || words.Count == 0 || totalMs - words[^1].StartMs < 2000)
                break;

            int lastStart = words[^1].StartMs;
            if (lastStart - OverlapMs <= offsetMs)
                break;
            words.RemoveAt(words.Count - 1);
            keepFromMs = lastStart;
            offsetMs = lastStart - OverlapMs;
        }

        // Whisper's own token ends often trail its (more precise) DTW starts, so a word ends where the next begins.
        for (int i = 0; i < words.Count; i++)
        {
            int nextStart = i + 1 < words.Count ? words[i + 1].StartMs : words[i].StartMs + 400;
            if (words[i].EndMs <= words[i].StartMs)
                words[i] = words[i] with { EndMs = Math.Max(nextStart, words[i].StartMs) };
        }

        return words;
    }

    // One pass of Whisper over the samples; times are from the start of the samples.
    private async Task<List<HeardWord>> RecognizeSamplesAsync(float[] samples, CancellationToken cancel)
    {
        var words = new List<HeardWord>();
        string? text = null;
        int start = 0, end = 0;

        await foreach (var segment in _processor.ProcessAsync(samples, cancel))
        {
            foreach (var token in segment.Tokens ?? [])
            {
                // Special tokens ([_BEG_], [_TT_123], <|endoftext|>…) carry no words.
                string piece = token.Text ?? "";
                if (piece.Length == 0 || piece.StartsWith("[_") || piece.StartsWith("<|"))
                    continue;

                // Times are in hundredths of a second. DTW is -1 when it couldn't be worked out.
                int tokenStart = (int)(token.DtwTimestamp >= 0 ? token.DtwTimestamp : token.Start) * 10;
                int tokenEnd = (int)token.End * 10;

                // A token starting with a space begins a new word; others continue it ("Kal" + "tunk").
                if (text is null || piece.StartsWith(' '))
                {
                    if (text is not null)
                        words.Add(new HeardWord(text.Trim(), start, Math.Max(end, start)));
                    text = piece;
                    start = tokenStart;
                }
                else
                {
                    text += piece;
                }
                end = tokenEnd;
            }
        }

        if (text is not null)
            words.Add(new HeardWord(text.Trim(), start, Math.Max(end, start)));

        return words;
    }

    // MP3 → 16 kHz mono samples, the way Whisper wants them.
    private static float[] Decode(byte[] mp3)
    {
        using var reader = new Mp3FileReader(new MemoryStream(mp3));
        ISampleProvider source = reader.ToSampleProvider();
        if (source.WaveFormat.Channels == 2)
            source = source.ToMono();
        if (source.WaveFormat.SampleRate != SampleRate)
            source = new WdlResamplingSampleProvider(source, SampleRate);

        var samples = new List<float>(SampleRate * 30);
        var buffer = new float[SampleRate];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            samples.AddRange(buffer.AsSpan(0, read));
        return samples.ToArray();
    }

    public void Dispose()
    {
        _processor.Dispose();
        _factory.Dispose();
    }
}
