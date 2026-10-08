using WowTts.Domain.Quests;

namespace WowTts.Application.Quests;

// A quest as the API returns it. Parts only lists the parts that have text.
public sealed record QuestDto(
    int Id,
    string Title,
    string NpcName,
    string NpcRace,
    string NpcGender,
    IReadOnlyList<QuestPartDto> Parts);

// One part of a quest. Text is what will be spoken, with placeholders already replaced.
// HasAudio tells whether the MP3 has been generated yet.
public sealed record QuestPartDto(QuestPart Part, string Text, bool HasAudio);
