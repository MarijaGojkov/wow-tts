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

    public void Delete(int questId, QuestPart part) => File.Delete(GetPath(questId, part));

    // One directory listing instead of a File.Exists call per quest part.
    public IReadOnlySet<AudioKey> ListAll()
    {
        var keys = new HashSet<AudioKey>();
        if (!Directory.Exists(_folder))
            return keys;

        foreach (string file in Directory.EnumerateFiles(_folder, "*.mp3"))
        {
            // File names look like 783-accept.mp3.
            string[] name = Path.GetFileNameWithoutExtension(file).Split('-');
            if (name.Length == 2 && int.TryParse(name[0], out int questId)
                && Enum.TryParse(name[1], ignoreCase: true, out QuestPart part))
            {
                keys.Add(new AudioKey(questId, part));
            }
        }

        return keys;
    }

    private string GetPath(int questId, QuestPart part) =>
        Path.Combine(_folder, $"{questId}-{part.ToString().ToLowerInvariant()}.mp3");
}
