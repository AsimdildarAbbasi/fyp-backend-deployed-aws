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

        [HttpGet("{id}/tasks")]
        public async Task<IActionResult> GetTasksByOfficeBoy(int id)
        {
            var now = DateTime.Now;

            var tasks = await _context.Tasks
                .Where(t => t.OfficeBoyAccountId == id &&
                    (
                        // Simple immediate tasks: not scheduled, and not a geofence task at all
                        (t.GeofenceTaskDetail == null && !t.IsScheduled)
                        ||
                        // Scheduled tasks: only show starting 1 hour before ScheduledAt
                        (t.IsScheduled && t.ScheduledAt != null && t.ScheduledAt <= now.AddHours(1))
                        ||
                        // Geofence tasks: only show once triggered (Enter/Exit matched)
                        (t.GeofenceTaskDetail != null && t.GeofenceTaskDetail.IsVisibleToOfficeBoy)
                    ))
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
                    isScheduled = t.IsScheduled,
                    isGeofenceTask = t.GeofenceTaskDetail != null,
                    triggerType = t.GeofenceTaskDetail != null ? t.GeofenceTaskDetail.TriggerType : null
                })
                .ToListAsync();

            return Ok(tasks);
        }
    }
}