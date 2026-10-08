using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Quests;
using WowTts.Domain.Quests;

namespace WowTts.Api.Controllers;

[ApiController]
[Route("api/quests")]
public sealed class QuestsController(QuestService questService) : ControllerBase
{
    // Lists all quests, with the text of each part and whether its audio exists yet.
    [HttpGet]
    public async Task<IReadOnlyList<QuestDto>> GetAll()
    {
        return await questService.GetAllAsync();
    }

    // One quest's details.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<QuestDto> Get(int id)
    {
        return await questService.GetAsync(id);
    }

    // The MP3 for one part of a quest: accept, progress or complete.
    // Generated with Azure on the first request, then served from the saved file.
    [HttpGet("{id:int}/audio/{part}")]
    [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, "audio/mpeg")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<IActionResult> GetAudio(int id, QuestPart part)
    {
        byte[] audio = await questService.GetOrCreateAudioAsync(id, part);

        return File(audio, "audio/mpeg");
    }
}
