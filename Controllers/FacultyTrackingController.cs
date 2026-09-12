using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FacultyTrackingController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public FacultyTrackingController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/facultytracking/has-pending/{facultyId}
        // Returns whether faculty has any pending geofence tasks waiting for enter/exit
        [HttpGet("has-pending/{facultyId}")]
        public async Task<IActionResult> HasPendingGeofenceTasks(int facultyId)
        {
            var count = await _context.Tasks.CountAsync(t =>
                t.FacultyAccountId == facultyId &&
                t.GeofenceId != null &&
                !t.IsVisibleToOfficeBoy &&
                t.Status == "Pending");

            return Ok(new { hasPending = count > 0, count });
        }

        // POST api/facultytracking/ping
        // PURPOSE: Faculty app calls this every ~15-30 seconds while a geofence task is pending.
        // Detects Enter/Exit transitions and flips matching geofence Tasks to visible.
        [HttpPost("ping")]
        public async Task<IActionResult> Ping([FromBody] LocationPingRequest request)
        {
            var facultyExists = await _context.Accounts.AnyAsync(a => a.Id == request.FacultyAccountId && a.Role == 2);
            if (!facultyExists) return BadRequest(new { message = "Faculty not found" });

            var geofences = await _context.Geofences
                .Where(g => g.IsActive)
                .ToListAsync();

            foreach (var geofence in geofences)
            {
                double distanceMeters = HaversineDistanceMeters(
                    request.Latitude, request.Longitude, geofence.CenterLatitude, geofence.CenterLongitude);

                bool isInsideNow = distanceMeters <= geofence.RadiusMeters;

                var existingState = await _context.FacultyGeofenceStates
                    .FirstOrDefaultAsync(s => s.FacultyAccountId == request.FacultyAccountId && s.GeofenceId == geofence.Id);

                bool wasInsideBefore = existingState != null && existingState.IsCurrentlyInside;

                // Log the raw ping regardless of whether a transition happened
                _context.FacultyTrackings.Add(new FacultyTracking
                {
                    FacultyAccountId = request.FacultyAccountId,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    RecordedAt = DateTime.Now,
                    IsInsideGeofence = isInsideNow,
                    GeofenceId = geofence.Id
                });

                // Only act when the state actually flipped (this is the ENTER/EXIT event)
                if (isInsideNow != wasInsideBefore)
                {
                    string triggerType = isInsideNow ? "Enter" : "Exit";

                    var tasksToTrigger = await _context.Tasks
                        .Where(t => t.FacultyAccountId == request.FacultyAccountId &&
                                    t.GeofenceId == geofence.Id &&
                                    t.TriggerType == triggerType &&
                                    !t.IsVisibleToOfficeBoy &&
                                    t.Status == "Pending")
                        .ToListAsync();

                    foreach (var task in tasksToTrigger)
                    {
                        task.IsVisibleToOfficeBoy = true;
                        task.TriggeredAt = DateTime.Now;
                    }
                }

                // Upsert the state row
                if (existingState == null)
                {
                    _context.FacultyGeofenceStates.Add(new FacultyGeofenceState
                    {
                        FacultyAccountId = request.FacultyAccountId,
                        GeofenceId = geofence.Id,
                        IsCurrentlyInside = isInsideNow,
                        LastUpdatedAt = DateTime.Now
                    });
                }
                else
                {
                    existingState.IsCurrentlyInside = isInsideNow;
                    existingState.LastUpdatedAt = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Location processed" });
        }

        // Standard haversine formula - distance between two lat/long points, in meters.
        // Used instead of exact coordinate matching, since GPS pings never land exactly on a boundary point.
        private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusMeters = 6371000;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusMeters * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }

    public class LocationPingRequest
    {
        public int FacultyAccountId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class GeofenceRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public double RadiusMeters { get; set; }
    }
}