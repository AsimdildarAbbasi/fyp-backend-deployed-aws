using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfficeBoyController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public OfficeBoyController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/officeboy/{id}/tasks
        [HttpGet("{id}/tasks")]
        public async Task<IActionResult> GetTasksByOfficeBoy(int id)
        {
            var officeBoy = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.Role == 1);

            if (officeBoy == null)
                return NotFound(new { message = "OfficeBoy not found" });

            var tasks = await _context.Tasks
                .Where(t => t.OfficeBoyAccountId == id)
                .OrderByDescending(t => t.Id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location != null ? t.Location.Name : null,
                    latitude = t.Location != null ? t.Location.Latitude : null,
                    longitude = t.Location != null ? t.Location.Longitude : null,
                    assignedBy = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    status = t.Status,
                    taskTime = t.TaskTime,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null,
                    scheduledAt = t.ScheduledAt,
                    isScheduled = t.IsScheduled
                })
                .ToListAsync();

            return Ok(tasks);
        }
    }
}