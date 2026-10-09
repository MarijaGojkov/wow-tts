using WowTts.Domain.Quests;
using WowTts.Domain.Speech;

namespace WowTts.Application.Abstractions;

// Everything the voice director needs to decide how one quest line is spoken.
// When VoiceIsCast is true, Voices holds only the NPC's own voice, and only the style, speed and pitch are open.
public sealed record VoiceDirectionRequest(
    string QuestTitle,
    string NpcName,
    string NpcGender,
    string NpcRace,
    QuestPart Part,
    string Text,
    IReadOnlyList<VoiceInfo> Voices,
    bool VoiceIsCast);

// Decides the voice and the performance (style, strength, speed, pitch) for a line.
// Infrastructure provides an AI version (Azure OpenAI) and a plain fallback for when no AI is set up.
public interface IVoiceDirector
{
    Task<VoicePerformance> DirectAsync(VoiceDirectionRequest request);
}

// The voices the speech service offers.
public interface IVoiceCatalog
{
    Task<IReadOnlyList<VoiceInfo>> GetVoicesAsync();
}

// The voice each NPC was cast with.
public interface INpcVoiceRepository
{
    Task<NpcVoice?> GetAsync(string npcName, string npcGender);
    Task SaveAsync(NpcVoice voice);
    Task DeleteAsync(string npcName, string npcGender);
}

// How each generated quest part was performed, so the viewer can show it and it can be reproduced.
public interface IPerformanceRepository
{
    Task<IReadOnlyDictionary<QuestPart, VoicePerformance>> GetForQuestAsync(int questId);
    Task SaveAsync(int questId, QuestPart part, VoicePerformance performance);
    Task DeleteAsync(int questId, QuestPart part);
}
