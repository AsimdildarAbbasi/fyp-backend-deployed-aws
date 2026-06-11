using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeacherTrackingController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public TeacherTrackingController(ObmanagementContext context)
        {
            _context = context;
        }

        [HttpPost("set-task")]
        public async Task<IActionResult> SetTask([FromBody] SetTaskRequest request)
        {
            // Deactivate existing task of the same type (Arrival/Departure) for this faculty
            var existingTasks = await _context.ArrivalDepartureTasks
                .Where(t => t.FacultyAccountId == request.FacultyAccountId && t.TaskType == request.TaskType && t.IsActive)
                .ToListAsync();

            foreach (var t in existingTasks)
            {
                t.IsActive = false;
            }

            var task = new ArrivalDepartureTask
            {
                FacultyAccountId = request.FacultyAccountId,
                TaskType = request.TaskType,
                Description = request.Description,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                IsActive = true
            };

            _context.ArrivalDepartureTasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task configuration saved successfully" });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateTeacherLocationRequest request)
        {
            var activeTasks = await _context.ArrivalDepartureTasks
                .Where(t => t.FacultyAccountId == request.FacultyAccountId && t.IsActive)
                .ToListAsync();

            if (!activeTasks.Any())
                return Ok(new { message = "No active geofence tasks." });

            bool taskTriggered = false;

            foreach (var geofence in activeTasks)
            {
                double distance = CalculateDistance((double)geofence.Latitude, (double)geofence.Longitude, (double)request.Latitude, (double)request.Longitude);
                
                // If within 500 meters (0.5 km)
                if (distance <= 0.5)
                {
                    var today = DateTime.Today;
                    var alreadyTriggered = await _context.Tasks.AnyAsync(t => 
                        t.FacultyAccountId == request.FacultyAccountId &&
                        t.Description == geofence.Description &&
                        t.TaskTime != null && t.TaskTime.Value.Date == today);

                    if (!alreadyTriggered)
                    {
                        var facultyFloorId = await _context.FacultyMemberOffices
                            .Where(f => f.FacultyAccountId == request.FacultyAccountId)
                            .Select(f => f.Office.BuildingFloorId)
                            .FirstOrDefaultAsync();

                        var availableOfficeBoyId = await _context.OfficeBoyAssignedFloors
                            .Where(o => o.FloorId == facultyFloorId && o.Status == "Active")
                            .Select(o => o.OfficeBoyAccountId)
                            .FirstOrDefaultAsync();

                        if (availableOfficeBoyId != 0)
                        {
                            var newTask = new OBManagementAPI.Models.Task
                            {
                                FacultyAccountId = request.FacultyAccountId,
                                OfficeBoyAccountId = availableOfficeBoyId,
                                LocationId = 1, // Fallback location
                                Description = geofence.Description,
                                TaskTime = DateTime.Now,
                                IsScheduled = false,
                                Status = "Pending",
                            };

                            _context.Tasks.Add(newTask);
                            taskTriggered = true;
                        }
                    }
                }
            }

            if (taskTriggered)
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Location updated, task triggered!" });
            }

            return Ok(new { message = "Location updated." });
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; 
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; 
            return d;
        }

        private double Deg2Rad(double deg)
        {
            return deg * (Math.PI / 180);
        }
    }

    public class SetTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public string TaskType { get; set; }
        public string Description { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    public class UpdateTeacherLocationRequest
    {
        public int FacultyAccountId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}
