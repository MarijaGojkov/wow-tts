using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Maps;
using WowTts.Application.Quests;

namespace WowTts.Api.Controllers;

[ApiController]
[Route("api/map")]
public sealed class MapController(MapService mapService, QuestService questService) : ControllerBase
{
    // The world map's continents and zones, where each zone sits on its continent, and how many quests it has.
    [HttpGet("zones")]
    public async Task<IReadOnlyList<MapZoneDto>> GetZones()
    {
        return await mapService.GetZonesAsync();
    }

    // Every matching quest giver on one map, as points in percent of that map. Same filters as /api/quests, e.g.
    // /api/map/markers?mapZoneId=1429&side=alliance
    [HttpGet("markers")]
    [ProducesResponseType(typeof(IReadOnlyList<MapMarkerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IReadOnlyList<MapMarkerDto>> GetMarkers([FromQuery] QuestQuery query)
    {
        return await questService.GetMarkersAsync(query);
    }
}
