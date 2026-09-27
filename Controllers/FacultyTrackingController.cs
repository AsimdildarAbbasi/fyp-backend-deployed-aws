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
            var count = await _context.GeofenceTaskDetails
                .CountAsync(g => g.FacultyAccountId == facultyId && !g.IsVisibleToOfficeBoy);

            return Ok(new { hasPending = count > 0, count });
        }

        // POST api/facultytracking/ping
        // PURPOSE: Faculty app calls this every ~15-30 seconds while a geofence task is pending.
        // Checks the faculty's CURRENT position against each active geofence and fires any
        // matching pending task immediately. No location history is stored anywhere.
        [HttpPost("ping")]
        public async Task<IActionResult> Ping([FromBody] LocationPingRequest request)
        {
            var facultyExists = await _context.Accounts.AnyAsync(a => a.Id == request.FacultyAccountId && a.Role == 2);
            if (!facultyExists) return BadRequest(new { message = "Faculty not found" });

            var geofences = await _context.Geofences
                .Where(g => g.IsActive)
                .ToListAsync();

            bool anyTaskTriggered = false;

            foreach (var geofence in geofences)
            {
                double distanceMeters = HaversineDistanceMeters(
                    request.Latitude, request.Longitude, geofence.CenterLatitude, geofence.CenterLongitude);

                bool isInsideNow = distanceMeters <= geofence.RadiusMeters;
                string currentCondition = isInsideNow ? "Enter" : "Exit";

                var detailsToTrigger = await _context.GeofenceTaskDetails
                    .Where(g => g.FacultyAccountId == request.FacultyAccountId &&
                                g.GeofenceId == geofence.Id &&
                                g.TriggerType == currentCondition &&
                                !g.IsVisibleToOfficeBoy)
                    .ToListAsync();

                foreach (var detail in detailsToTrigger)
                {
                    detail.IsVisibleToOfficeBoy = true;
                    detail.TriggeredAt = DateTime.Now;
                    anyTaskTriggered = true;
                }
            }

            if (anyTaskTriggered)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Location processed", taskTriggered = anyTaskTriggered });
        }

        // Standard haversine formula - distance between two lat/long points, in meters.
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
}