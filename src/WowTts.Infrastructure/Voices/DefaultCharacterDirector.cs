using WowTts.Application.Abstractions;
using WowTts.Domain.Speech;

namespace WowTts.Infrastructure.Voices;

// The character director when there's no AI (or it failed): the NPC's saved casting, or else a voice for their
// gender and a plain character from their race, read naturally.
public sealed class DefaultCharacterDirector : ICharacterDirector
{
    public Task<CharacterDirection> DirectAsync(CharacterDirectionRequest request)
    {
        const string delivery = "Natural and sincere, matching the words.";

        if (request.Cast is NpcCharacter cast)
            return Task.FromResult(new CharacterDirection(
                cast.Voice, cast.Character, delivery, "The NPC's character, read plainly (no AI director set up).", VoiceDirector.Default));

        string voice = request.Npc.Gender switch
        {
            "Female" => "coral",
            "Male" => "ash",
            _ => "alloy",
        };
        string race = request.Npc.Race == "Unknown" ? "" : $"{request.Npc.Race} ";
        string character = $"A {race}{request.Npc.Gender.ToLowerInvariant()} quest giver in World of Warcraft.".Replace("unknown ", "");

        return Task.FromResult(new CharacterDirection(
            voice, character, delivery, "Default voice for this gender (no AI director set up).", VoiceDirector.Default));
    }
}
