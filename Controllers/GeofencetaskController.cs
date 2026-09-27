using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GeofenceTasksController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public GeofenceTasksController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/geofencetasks/geofences
        // GET api/geofencetasks/GetGeofences
        // PURPOSE: Dropdown source for the "My Location" screen.
        [HttpGet("geofences")]
        [HttpGet("GetGeofences")]
        public async Task<IActionResult> GetGeofences()
        {
            var geofences = await _context.Geofences
                .Where(g => g.IsActive)
                .Select(g => new
                {
                    id = g.Id,
                    name = g.Name,
                    centerLatitude = g.CenterLatitude,
                    centerLongitude = g.CenterLongitude,
                    radiusMeters = g.RadiusMeters
                })
                .ToListAsync();

            return Ok(geofences);
        }

        // GET api/geofencetasks/categories
        // GET api/geofencetasks/GetTaskCategories
        // GET api/geofencetasks/taskcategories
        // PURPOSE: Quick-action buttons (Turn on AC, Turn off AC, Clean room...).
        [HttpGet("categories")]
        [HttpGet("GetTaskCategories")]
        [HttpGet("taskcategories")]
        public async Task<IActionResult> GetTaskCategories()
        {
            var categories = await _context.TaskCategories
                .Where(tc => tc.IsActive)
                .Select(tc => new
                {
                    id = tc.Id,
                    name = tc.Name
                })
                .ToListAsync();

            return Ok(categories);
        }

        // POST api/geofencetasks/createGeofenceTask
        // PURPOSE: Faculty creates a "My Location" task. Creates the base Task row,
        // then its GeofenceTaskDetail row - stays hidden from the OfficeBoy until
        // FacultyTrackingController detects the matching Enter/Exit and flips it visible.
        [HttpPost("createGeofenceTask")]
        public async Task<IActionResult> CreateGeofenceTask([FromBody] CreateGeofenceTaskRequest request)
        {
            var facultyExists = await _context.Accounts.AnyAsync(a => a.Id == request.FacultyAccountId && a.Role == 2);
            if (!facultyExists) return BadRequest(new { message = "Faculty not found" });

            var officeBoyExists = await _context.Accounts.AnyAsync(a => a.Id == request.OfficeBoyAccountId && a.Role == 1);
            if (!officeBoyExists) return BadRequest(new { message = "OfficeBoy not found" });

            int locationId = request.LocationId;
            if (locationId <= 0)
            {
                var inBiitLocation = await _context.Locations.FirstOrDefaultAsync(l => l.Name == "InBIIT");
                if (inBiitLocation != null)
                {
                    locationId = inBiitLocation.Id;
                }
                else
                {
                    var firstLocation = await _context.Locations.FirstOrDefaultAsync();
                    if (firstLocation != null)
                    {
                        locationId = firstLocation.Id;
                    }
                }
            }
            else
            {
                var locationExists = await _context.Locations.AnyAsync(l => l.Id == locationId);
                if (!locationExists) return BadRequest(new { message = "Location not found" });
            }

            var geofenceExists = await _context.Geofences.AnyAsync(g => g.Id == request.GeofenceId && g.IsActive);
            if (!geofenceExists) return BadRequest(new { message = "Geofence not found" });

            int? taskCategoryId = null;
            if (request.TaskCategoryId.HasValue && request.TaskCategoryId.Value > 0)
            {
                var categoryExists = await _context.TaskCategories.AnyAsync(c => c.Id == request.TaskCategoryId.Value && c.IsActive);
                if (!categoryExists) return BadRequest(new { message = "Task Category not found" });
                taskCategoryId = request.TaskCategoryId;
            }

            if (string.IsNullOrWhiteSpace(request.TriggerType))
                return BadRequest(new { message = "TriggerType is required ('Enter' or 'Exit')" });

            string triggerTypeFormatted = request.TriggerType.Trim();
            if (string.Equals(triggerTypeFormatted, "enter", StringComparison.OrdinalIgnoreCase))
                triggerTypeFormatted = "Enter";
            else if (string.Equals(triggerTypeFormatted, "exit", StringComparison.OrdinalIgnoreCase))
                triggerTypeFormatted = "Exit";
            else
                return BadRequest(new { message = "TriggerType must be 'Enter' or 'Exit'" });

            // Step 1: create the base Task row (same common fields as Now/Later tasks)
            var task = new OBManagementAPI.Models.Task
            {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = locationId,
                Description = request.Description,
                TaskTime = DateTime.Now,
                IsScheduled = false,
                Status = "Pending"
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();   // save now so task.Id is generated for the detail row below

            // Step 2: create the geofence-specific detail row, linked by the same Id
            var detail = new GeofenceTaskDetail
            {
                TaskId = task.Id,
                FacultyAccountId = request.FacultyAccountId,
                GeofenceId = request.GeofenceId,
                TriggerType = triggerTypeFormatted,
                TaskCategoryId = taskCategoryId,
                IsVisibleToOfficeBoy = false
            };

            _context.GeofenceTaskDetails.Add(detail);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Geofence task created successfully",
                taskId = task.Id
            });
        }
    }

    public class CreateGeofenceTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public int OfficeBoyAccountId { get; set; }
        public int LocationId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int GeofenceId { get; set; }
        public int? TaskCategoryId { get; set; }
        public string TriggerType { get; set; } = string.Empty;
    }
}