using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Quests;
using WowTts.Domain.Quests;

namespace WowTts.Api.Controllers;

[ApiController]
[Route("api/quests")]
public sealed class QuestsController(QuestService questService) : ControllerBase
{
    // One page of quests, without their text. Every filter is optional, e.g.
    // /api/quests?search=kobold&categoryId=12&side=alliance&audio=missing&page=1&pageSize=50
    [HttpGet]
    [ProducesResponseType(typeof(QuestSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<QuestSearchResult> Search([FromQuery] QuestQuery query)
    {
        return await questService.SearchAsync(query);
    }

    // How many quests and quest parts there are, and how many parts have audio.
    [HttpGet("stats")]
    public async Task<QuestStatsDto> GetStats()
    {
        return await questService.GetStatsAsync();
    }

    // The quest log headers (zones, dungeons and categories such as Seasonal) with their quest counts.
    [HttpGet("categories")]
    public async Task<IReadOnlyList<QuestCategoryDto>> GetCategories()
    {
        return await questService.GetCategoriesAsync();
    }

    // One quest with all its text.
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

    // Deletes a quest part's audio, so the next request generates it again with a fresh performance.
    [HttpDelete("{id:int}/audio/{part}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteAudio(int id, QuestPart part)
    {
        await questService.DeleteAudioAsync(id, part);
        return NoContent();
    }

    // Forgets the voice cast for this quest's NPC, so the voice director casts a new one next time.
    [HttpDelete("{id:int}/voice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Recast(int id)
    {
        await questService.RecastAsync(id);
        return NoContent();
    }
}
