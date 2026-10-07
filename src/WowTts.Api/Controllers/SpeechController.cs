using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Speech;

namespace WowTts.Api.Controllers;

[ApiController]
[Route("api/speech")]
public sealed class SpeechController(SpeechService speechService) : ControllerBase
{
    // Turns text into speech and returns it as MP3 audio.
    [HttpPost]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "audio/mpeg")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<IActionResult> Speak(SpeechRequest request)
    {
        byte[] audio = await speechService.SpeakAsync(request);

        return File(audio, "audio/mpeg");
    }
}
