using Microsoft.Extensions.Logging;
using WowTts.Application.Abstractions;
using WowTts.Application.Exceptions;
using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Quests;

public sealed class QuestService(
    IQuestRepository quests,
    IAudioStore audioStore,
    ISpeechSynthesizer synthesizer,
    ILogger<QuestService> logger)
{
    public async Task<IReadOnlyList<QuestDto>> GetAllAsync()
    {
        var all = await quests.GetAllAsync();
        return all.Select(ToDto).ToList();
    }

    public async Task<QuestDto> GetAsync(int id)
    {
        return ToDto(await GetQuestAsync(id));
    }

    // Returns the quest part's audio. It's generated with Azure the first time, then read from the saved file,
    // so each text is only paid for once.
    public async Task<byte[]> GetOrCreateAudioAsync(int id, QuestPart part)
    {
        if (!Enum.IsDefined(part))
            throw new InvalidRequestException($"Unknown quest part '{part}'. Use accept, progress or complete.");

        byte[]? saved = await audioStore.TryReadAsync(id, part);
        if (saved is not null)
        {
            logger.LogInformation("Quest {QuestId} {Part}: served saved audio", id, part);
            return saved;
        }

        Quest quest = await GetQuestAsync(id);

        string? text = quest.GetText(part);
        if (string.IsNullOrWhiteSpace(text))
            throw new NotFoundException($"Quest {id} has no {part.ToString().ToLowerInvariant()} text.");

        string spoken = TextCleaner.Clean(text);

        logger.LogInformation("Quest {QuestId} {Part}: generating audio for {Characters} characters",
            id, part, spoken.Length);

        byte[] audio = await synthesizer.SynthesizeAsync(spoken, voice: null);
        await audioStore.SaveAsync(id, part, audio);

        return audio;
    }

    private async Task<Quest> GetQuestAsync(int id)
    {
        return await quests.GetByIdAsync(id)
            ?? throw new NotFoundException($"There's no quest with ID {id}.");
    }

    private QuestDto ToDto(Quest quest)
    {
        var parts = Enum.GetValues<QuestPart>()
            .Where(quest.HasText)
            .Select(part => new QuestPartDto(
                part,
                TextCleaner.Clean(quest.GetText(part)!),
                audioStore.Exists(quest.Id, part)))
            .ToList();

        return new QuestDto(quest.Id, quest.Title, quest.NpcName, quest.NpcRace, quest.NpcGender, parts);
    }
}
