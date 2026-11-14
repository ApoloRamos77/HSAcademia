using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Torneos: ficha, equipos con su responsable, plantel y partidos.</summary>
[ApiController]
[Route("api/tournaments")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class TournamentsController : ControllerBase
{
    private readonly TournamentService _service;

    public TournamentsController(TournamentService service) => _service = service;

    private Guid AcademyId => Guid.Parse(User.FindFirst("AcademyId")?.Value ?? User.FindFirst("academyId")?.Value
        ?? throw new UnauthorizedAccessException("AcademyId no encontrado en el token."));

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet]
    public Task<IActionResult> List() => Run(async () => Ok(await _service.ListAsync(AcademyId)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id) => Run(async () => Ok(await _service.GetAsync(AcademyId, id)));

    [HttpPost]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> Create([FromBody] CreateTournamentDto dto) => Run(async () =>
    {
        var created = await _service.CreateAsync(AcademyId, dto);
        return Created($"/api/tournaments/{created.Id}", created);
    });

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> Update(Guid id, [FromBody] UpdateTournamentDto dto) =>
        Run(async () => Ok(await _service.UpdateAsync(AcademyId, id, dto)));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> Delete(Guid id) => Run(async () => { await _service.DeleteAsync(AcademyId, id); return NoContent(); });

    // ── Equipos ──
    [HttpPost("{id:guid}/teams")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> CreateTeam(Guid id, [FromBody] SaveTournamentTeamDto dto) =>
        Run(async () => Ok(await _service.CreateTeamAsync(AcademyId, id, dto)));

    [HttpPut("teams/{teamId:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> UpdateTeam(Guid teamId, [FromBody] SaveTournamentTeamDto dto) =>
        Run(async () => Ok(await _service.UpdateTeamAsync(AcademyId, teamId, dto)));

    [HttpDelete("teams/{teamId:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> DeleteTeam(Guid teamId) =>
        Run(async () => { await _service.DeleteTeamAsync(AcademyId, teamId); return NoContent(); });

    // ── Plantel ──
    [HttpGet("teams/{teamId:guid}/candidates")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> Candidates(Guid teamId) => Run(async () => Ok(await _service.GetCandidatesAsync(AcademyId, teamId)));

    [HttpPut("teams/{teamId:guid}/players")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> SaveRoster(Guid teamId, [FromBody] SaveTournamentRosterDto dto) =>
        Run(async () => Ok(await _service.SaveRosterAsync(AcademyId, teamId, dto)));

    // ── Partidos ──
    [HttpPost("teams/{teamId:guid}/matches")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> CreateMatch(Guid teamId, [FromBody] SaveTournamentMatchDto dto) =>
        Run(async () => Ok(await _service.CreateMatchAsync(AcademyId, teamId, dto)));

    [HttpPost("teams/{teamId:guid}/matches/generate")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> GenerateMatches(Guid teamId, [FromBody] GenerateTournamentMatchesDto dto) =>
        Run(async () => Ok(await _service.GenerateMatchesAsync(AcademyId, teamId, dto)));

    [HttpPut("matches/{eventId:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> UpdateMatch(Guid eventId, [FromBody] SaveTournamentMatchDto dto) =>
        Run(async () => Ok(await _service.UpdateMatchAsync(AcademyId, eventId, dto)));

    [HttpPost("matches/{eventId:guid}/calls/{studentId:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> AddCall(Guid eventId, Guid studentId) =>
        Run(async () => { await _service.AddCallAsync(AcademyId, eventId, studentId); return NoContent(); });

    [HttpDelete("matches/{eventId:guid}/calls/{studentId:guid}")]
    [RequirePermission(Permissions.EventsManage)]
    public Task<IActionResult> RemoveCall(Guid eventId, Guid studentId) =>
        Run(async () => { await _service.RemoveCallAsync(AcademyId, eventId, studentId); return NoContent(); });
}
