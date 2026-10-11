using Microsoft.AspNetCore.Mvc;
using WowTts.Application.Abstractions;
using WowTts.Application.Quests;

namespace WowTts.Api.Controllers;

[ApiController]
[Route("api/voices")]
public sealed class VoicesController(QuestService questService) : ControllerBase
{
    // The speech engines quest audio can be generated with, e.g. Dragon HD Omni and GPT-4o mini TTS,
    // and whether each is set up.
    [HttpGet("engines")]
    public IReadOnlyList<VoiceEngineInfo> GetEngines() => questService.GetEngines();
}
