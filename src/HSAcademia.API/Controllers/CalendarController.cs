using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Services;
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
        
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdStr, out var uId) ? uId : (Guid?)null;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        var events = await _calendarService.GetEventsForMonthAsync(
            academyId, year, month, headquarterId, categoryId, eventType, userId, userRole);
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
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
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
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
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

    /// <summary>
    /// PUT /api/calendar/events/{id}
    /// </summary>
    [HttpPut("events/{id:guid}")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var academyId = GetAcademyId();
            var updated = await _calendarService.UpdateEventAsync(academyId, id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// POST /api/calendar/events/bulk-shift?days=-1
    /// Shifts StartTime/EndTime of ALL academy events by N days (can be negative).
    /// Used to correct timezone offsets in bulk.
    /// </summary>
    [HttpPost("events/bulk-shift")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> BulkShiftDays([FromQuery] int days)
    {
        if (days == 0) return BadRequest(new { message = "days must be non-zero." });
        var academyId = GetAcademyId();
        var count = await _calendarService.BulkShiftDaysAsync(academyId, days);
        return Ok(new { message = $"{count} eventos desplazados {days} día(s).", count });
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
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
    public async Task<IActionResult> CreateTournament([FromBody] CreateTournamentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var academyId = GetAcademyId();
        var created = await _calendarService.CreateTournamentAsync(academyId, dto);
        return Created($"/api/calendar/tournaments/{created.Id}", created);
    }

    [HttpPut("tournaments/{id:guid}")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
    public async Task<IActionResult> UpdateTournament(Guid id, [FromBody] UpdateTournamentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var academyId = GetAcademyId();
            var updated = await _calendarService.UpdateTournamentAsync(academyId, id, dto);
            return Ok(updated);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("tournaments/{id:guid}")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
    public async Task<IActionResult> DeleteTournament(Guid id)
    {
        try
        {
            var academyId = GetAcademyId();
            await _calendarService.DeleteTournamentAsync(academyId, id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ─────────────────────────────────────────────────────────────
    // EVENT CALLS (Convocatorias)
    // ─────────────────────────────────────────────────────────────
    [HttpGet("events/{eventId:guid}/calls")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetEventCalls(Guid eventId)
    {
        var academyId = GetAcademyId();
        var calls = await _calendarService.GetEventCallsAsync(academyId, eventId);
        return Ok(calls);
    }

    [HttpPost("events/{eventId:guid}/calls/auto-generate")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.EventsManage)]
    public async Task<IActionResult> AutoGenerateEventCalls(Guid eventId)
    {
        try
        {
            var academyId = GetAcademyId();
            var count = await _calendarService.AutoGenerateEventCallsAsync(academyId, eventId);
            return Ok(new { message = $"Se generaron {count} nuevas convocatorias." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // ─────────────────────────────────────────────────────────────
    // Phase 3 — Mobile App endpoints
    // ─────────────────────────────────────────────────────────────
    [HttpGet("mobile/events/{eventId:guid}/my-call")]
    public async Task<IActionResult> GetMyEventCall(Guid eventId, [FromQuery] Guid studentId, [FromServices] AccessControlService access)
    {
        try
        {
            var academyId = GetAcademyId();
            if (User.IsEndUser() && !await access.UserOwnsStudentAsync(academyId, User.GetUserId(), studentId))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes acceso a la información de este alumno." });

            var call = await _calendarService.GetMyEventCallAsync(academyId, eventId, studentId);
            if (call == null) return NoContent();
            return Ok(call);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

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
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userId = Guid.TryParse(userIdStr, out var uId) ? uId : (Guid?)null;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            var events = await _calendarService.GetMobileEventsAsync(
                academyId, categoryId, headquarterId, userId, userRole);
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
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userId = Guid.TryParse(userIdStr, out var uId) ? uId : (Guid?)null;
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            var next = await _calendarService.GetNextEventAsync(academyId, categoryId, userId, userRole);
            return next is null ? NoContent() : Ok(next);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/calendar/mobile/my-calls — convocatorias de los PRÓXIMOS partidos de mis alumnos
    /// (apoderado o alumno), para confirmar o rechazar la asistencia desde la app.
    /// </summary>
    [HttpGet("mobile/my-calls")]
    [Authorize(Roles = "Student,Guardian")]
    public async Task<IActionResult> GetMyCalls([FromServices] HSAcademia.Infrastructure.Persistence.AppDbContext db)
    {
        var academyId = GetAcademyId();
        var userId = User.GetUserId();
        var now = HSAcademia.Infrastructure.Services.AcademyClock.WallNow;

        var calls = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.EventCalls
              .Where(c => c.AcademyId == academyId && c.Event.EndTime >= now && !c.Event.IsDeleted &&
                          (c.Student.GuardianId == userId || c.Student.UserId == userId))
              .OrderBy(c => c.Event.StartTime)
              .Select(c => new
              {
                  callId = c.Id,
                  eventId = c.EventId,
                  title = c.Event.Title,
                  opponent = c.Event.OpponentTeam,
                  startTime = c.Event.StartTime,
                  endTime = c.Event.EndTime,
                  location = c.Event.Headquarter != null ? c.Event.Headquarter.Name : null,
                  studentId = c.StudentId,
                  studentName = c.Student.FirstName + " " + c.Student.LastName,
                  isConfirmed = c.IsConfirmed
              }));
        return Ok(calls);
    }

    [HttpPut("mobile/calls/{callId:guid}/confirm")]
    public async Task<IActionResult> ConfirmEventCall(Guid callId, [FromBody] UpdateEventCallDto dto, [FromServices] AccessControlService access)
    {
        try
        {
            var academyId = GetAcademyId();
            // Un apoderado/alumno solo puede confirmar convocatorias de sus propios alumnos.
            if (User.IsEndUser() && !await access.UserOwnsEventCallAsync(academyId, User.GetUserId(), callId))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes responder esta convocatoria." });

            var updated = await _calendarService.UpdateEventCallStatusAsync(academyId, callId, dto.IsConfirmed);
            return Ok(updated);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

