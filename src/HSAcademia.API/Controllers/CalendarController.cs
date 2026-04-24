using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarController(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    private Guid GetAcademyId()
    {
        var claim = User.FindFirstValue("AcademyId")
                    ?? User.FindFirstValue("academyId")
                    ?? throw new UnauthorizedAccessException("AcademyId no encontrado en el token.");
        return Guid.Parse(claim);
    }

    // ─────────────────────────────────────────────────────────────
    // EVENTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/calendar/events?year=2026&amp;month=4&amp;headquarterId=...&amp;categoryId=...&amp;eventType=1
    /// Returns all real events + virtual birthday events for the given month.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? headquarterId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] int? eventType = null)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Año o mes inválidos.");

        var academyId = GetAcademyId();
        var events = await _calendarService.GetEventsForMonthAsync(
            academyId, year, month, headquarterId, categoryId, eventType);
        return Ok(events);
    }

    /// <summary>
    /// GET /api/calendar/events/{id}
    /// </summary>
    [HttpGet("events/{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id)
    {
        var academyId = GetAcademyId();
        var ev = await _calendarService.GetEventByIdAsync(academyId, id);
        return ev is null ? NotFound() : Ok(ev);
    }

    /// <summary>
    /// POST /api/calendar/events
    /// Creates a new event. Returns 409 Conflict if a scheduling conflict is detected.
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var academyId = GetAcademyId();
            var created = await _calendarService.CreateEventAsync(academyId, dto);
            return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/calendar/events/{id}
    /// </summary>
    [HttpDelete("events/{id:guid}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        try
        {
            var academyId = GetAcademyId();
            await _calendarService.DeleteEventAsync(academyId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // TOURNAMENTS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/calendar/tournaments
    /// </summary>
    [HttpGet("tournaments")]
    public async Task<IActionResult> GetTournaments()
    {
        var academyId = GetAcademyId();
        var list = await _calendarService.GetTournamentsAsync(academyId);
        return Ok(list);
    }

    /// <summary>
    /// POST /api/calendar/tournaments
    /// </summary>
    [HttpPost("tournaments")]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var academyId = GetAcademyId();
        var created = await _calendarService.CreateTournamentAsync(academyId, dto);
        return Created($"/api/calendar/tournaments/{created.Id}", created);
    }

    // ─────────────────────────────────────────────────────────────
    // Phase 3 — Mobile App endpoints
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/calendar/mobile/events?categoryId=...&amp;headquarterId=...
    /// Returns upcoming events (current + next month) in mobile-friendly format.
    /// Includes virtual birthday events. Used by the react-native-calendars screen.
    /// </summary>
    [HttpGet("mobile/events")]
    public async Task<IActionResult> GetMobileEvents(
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? headquarterId = null)
    {
        try
        {
            var academyId = GetAcademyId();
            var events = await _calendarService.GetMobileEventsAsync(
                academyId, categoryId, headquarterId);
            return Ok(events);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/calendar/mobile/next?categoryId=...
    /// Returns the next upcoming event for the dashboard hero card.
    /// Returns 204 No Content when there are no upcoming events.
    /// </summary>
    [HttpGet("mobile/next")]
    public async Task<IActionResult> GetNextEvent([FromQuery] Guid? categoryId = null)
    {
        try
        {
            var academyId = GetAcademyId();
            var next = await _calendarService.GetNextEventAsync(academyId, categoryId);
            return next is null ? NoContent() : Ok(next);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}

