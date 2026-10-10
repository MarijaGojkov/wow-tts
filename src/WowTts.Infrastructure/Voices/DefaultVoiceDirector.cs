using Microsoft.Extensions.Options;
using WowTts.Application.Abstractions;
using WowTts.Domain.Speech;
using WowTts.Infrastructure.Speech;

namespace WowTts.Infrastructure.Voices;

// The voice director when there's no AI: the NPC's cast voice, or else the default voice for their gender,
// read plainly with no style.
public sealed class DefaultVoiceDirector(IOptions<SpeechOptions> options) : IVoiceDirector
{
    private readonly SpeechOptions _options = options.Value;

    public Task<VoicePerformance> DirectAsync(VoiceDirectionRequest request)
    {
        if (request.VoiceIsCast)
            return Task.FromResult(VoicePerformance.Plain(request.Voices[0].Name, "The NPC's voice, read plainly (no AI voice director set up)."));

        string preferred = request.NpcGender.Equals("Female", StringComparison.OrdinalIgnoreCase)
            ? _options.DefaultFemaleVoice
            : _options.DefaultMaleVoice;

        // By persona, so the default voice also works when the voices are HD (en-US-Guy:DragonHDOmniLatestNeural).
        string voice = request.Voices.FirstOrDefault(v => VoiceInfo.SamePersona(v.Name, preferred))?.Name
            ?? request.Voices[0].Name;

        return Task.FromResult(VoicePerformance.Plain(voice, "Default voice for this gender (no AI voice director set up)."));
    }
}
