using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System;
using System.Threading.Tasks;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarController(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int year,
        [FromQuery] int month)
    {
        try {
            var events = await _calendarService.GetEventsForMonthAsync(
                Guid.Empty, year, month, null, null, null, null, "");
            return Ok(events);
        } catch (Exception ex) {
            Console.WriteLine("======= EXCEPTION =======");
            Console.WriteLine(ex.ToString());
            return StatusCode(500, ex.ToString());
        }
    }
}
