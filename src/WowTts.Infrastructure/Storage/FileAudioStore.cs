using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Domain.Quests;

namespace WowTts.Infrastructure.Storage;

// Keeps quest audio as MP3 files named like 783-accept.mp3. The addon builds the same name from the quest ID.
public sealed class FileAudioStore(IOptions<AudioStorageOptions> options, IHostEnvironment environment)
    : IAudioStore
{
    private readonly string _folder =
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.Folder));

    public bool Exists(int questId, QuestPart part) => File.Exists(GetPath(questId, part));

    public async Task<byte[]?> TryReadAsync(int questId, QuestPart part)
    {
        string path = GetPath(questId, part);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
    }

    public async Task SaveAsync(int questId, QuestPart part, byte[] audio)
    {
        Directory.CreateDirectory(_folder);
        await File.WriteAllBytesAsync(GetPath(questId, part), audio);
    }

    private string GetPath(int questId, QuestPart part) =>
        Path.Combine(_folder, $"{questId}-{part.ToString().ToLowerInvariant()}.mp3");
}
