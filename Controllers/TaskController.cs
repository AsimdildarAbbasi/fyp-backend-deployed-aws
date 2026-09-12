using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public TasksController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/tasks
        // PURPOSE: Returns all tasks with full details.
        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await _context.Tasks
                .OrderByDescending(t => t.Id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location != null ? t.Location.Name : null,
                    latitude = t.Location != null ? t.Location.Latitude : null,
                    longitude = t.Location != null ? t.Location.Longitude : null,
                    faculty = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
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

        // GET api/tasks/byfaculty/{facultyId}
        // PURPOSE: Returns only OfficeBoys assigned to the same floor as the Faculty.
        [HttpGet("byfaculty/{facultyId}")]
        public async Task<IActionResult> GetOfficeBoysByFaculty(int facultyId)
        {
            var facultyExists = await _context.Accounts.AnyAsync(a => a.Id == facultyId && a.Role == 2);

            if (!facultyExists)
                return NotFound(new { message = "Faculty not found" });

            var facultyFloorId = await _context.FacultyMemberOffices
                .Where(fmo => fmo.FacultyAccountId == facultyId)
                .Select(fmo => (int?)fmo.Office.BuildingFloorId)
                .FirstOrDefaultAsync();

            if (facultyFloorId == null || facultyFloorId == 0)
                return NotFound(new { message = "No floor assigned to this faculty" });

            var dbResult = await (from obaf in _context.OfficeBoyAssignedFloors
                                  join a in _context.Accounts on obaf.OfficeBoyAccountId equals a.Id
                                  join bf in _context.BuildingFloors on obaf.FloorId equals bf.Id
                                  join o in _context.Offices on obaf.OfficeId equals o.Id
                                  where obaf.FloorId == facultyFloorId.Value && obaf.Status == "Active"
                                  select new
                                  {
                                      id = a.Id,
                                      name = a.Name,
                                      floor = bf.Number,
                                      officeName = o.OfficeName
                                  }).ToListAsync();

            var officeBoys = dbResult.GroupBy(x => new { x.id, x.name, x.floor })
                .Select(g => new
                {
                    id = g.Key.id,
                    name = g.Key.name,
                    floor = g.Key.floor,
                    assignedOffices = g.Select(x => x.officeName).ToList()
                }).ToList();

            return Ok(officeBoys);
        }

        // POST api/tasks/createTask
        // PURPOSE: Faculty creates a new task and assigns it to an OfficeBoy.
        [HttpPost("createTask")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            var facultyExists = await _context.Accounts.AnyAsync(a => a.Id == request.FacultyAccountId && a.Role == 2);
            if (!facultyExists) return BadRequest(new { message = "Faculty not found" });

            var officeBoyExists = await _context.Accounts.AnyAsync(a => a.Id == request.OfficeBoyAccountId && a.Role == 1);
            if (!officeBoyExists) return BadRequest(new { message = "OfficeBoy not found" });

            var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
            if (!locationExists) return BadRequest(new { message = "Location not found" });

            if (string.IsNullOrEmpty(request.TaskMode))
                return BadRequest(new { message = "TaskMode is required (now/later)" });

            bool isScheduled = request.TaskMode.ToLower() == "later";
            DateTime scheduledTime = isScheduled ? (request.ScheduledAt ?? DateTime.Now) : DateTime.Now;

            if (isScheduled)
            {
                if (!request.ScheduledAt.HasValue)
                    return BadRequest(new { message = "ScheduledAt is required for later tasks" });
                if (request.ScheduledAt <= DateTime.Now)
                    return BadRequest(new { message = "Scheduled time must be in future" });
            }

            var task = new OBManagementAPI.Models.Task
            {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = request.LocationId,
                Description = request.Description,
                TaskTime = DateTime.Now,
                ScheduledAt = scheduledTime,
                IsScheduled = isScheduled,
                Status = "Pending"
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isScheduled ? "Task scheduled successfully" : "Task created successfully",
                taskId = task.Id,
                scheduledAt = scheduledTime
            });
        }

        // PUT api/tasks/{id}/complete
        // PURPOSE: OfficeBoy marks a task as Completed after finishing the work.
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.Status == "Completed")
                return BadRequest(new { message = "Task is already completed" });

            task.Status = "Completed";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task marked as completed" });
        }

        // PUT api/tasks/{id}/rate
        // PURPOSE: Faculty adds a rating and remarks to a completed task.
        [HttpPut("{id}/rate")]
        public async Task<IActionResult> RateTask(int id, [FromBody] RateTaskRequest request)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.Status != "Completed")
                return BadRequest(new { message = "Task must be completed before rating" });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5" });

            task.Rating = request.Rating;
            task.Remarks = request.Remarks;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task rated successfully" });
        }

        // GET api/tasks/completed
        // PURPOSE: Returns all completed tasks.
        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedTasks()
        {
            var tasks = await _context.Tasks
                .Where(t => t.Status == "Completed")
                .OrderByDescending(t => t.Id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location != null ? t.Location.Name : null,
                    faculty = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
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

        // GET api/tasks/Locations
        [HttpGet("Locations")]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations
                .Select(l => new
                {
                    id = l.Id,
                    name = l.Name,
                    latitude = l.Latitude,
                    longitude = l.Longitude
                })
                .ToListAsync();

            return Ok(locations);
        }

        // PUT api/tasks/{id}/start
        // PURPOSE: OfficeBoy starts a pending task.
        [HttpPut("{id}/start")]
        public async Task<IActionResult> StartTask(int id)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "Pending")
                return BadRequest(new { message = "Only Pending tasks can be started." });

            task.Status = "In Progress";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task started successfully. Status is now In Progress." });
        }

        // PUT api/tasks/{id}/update-current-location
        // PURPOSE: Office Boy updates live tracking location.
        [HttpPut("{id}/update-current-location")]
        public async Task<IActionResult> UpdateCurrentLocation(int id, [FromBody] UpdateCurrentLocationRequest request)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "In Progress")
                return BadRequest(new { message = "Task must be 'In Progress' to update location." });

            var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);

            if (!locationExists)
                return BadRequest(new { message = "Invalid location ID." });

            task.CurrentLocationId = request.LocationId;
            task.LocationUpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Location updated successfully." });
        }

        // GET api/tasks/active-for-faculty/{facultyId}
        [HttpGet("active-for-faculty/{facultyId}")]
        public async Task<IActionResult> GetActiveTasksForFaculty(int facultyId)
        {
            var facultyFloorId = await _context.FacultyMemberOffices
                .Where(fmo => fmo.FacultyAccountId == facultyId)
                .Select(fmo => (int?)fmo.Office.BuildingFloorId)
                .FirstOrDefaultAsync();

            if (facultyFloorId == null || facultyFloorId == 0) return Ok(new object[] { });

            int floorId = facultyFloorId.Value;

            var tasks = await _context.Tasks
                .Where(t => t.Status == "In Progress" &&
                            _context.OfficeBoyAssignedFloors.Any(obaf =>
                                obaf.OfficeBoyAccountId == t.OfficeBoyAccountId &&
                                obaf.FloorId == floorId &&
                                obaf.Status == "Active"))
                .Select(t => new
                {
                    taskId = t.Id,
                    officeBoyId = t.OfficeBoyAccountId,
                    officeBoyName = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    description = t.Description,
                    targetLocation = t.Location != null ? t.Location.Name : null,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : "Unknown",
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // GET api/tasks/geofences
        // GET api/tasks/GetGeofences
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

        // GET api/tasks/categories
        // GET api/tasks/GetTaskCategories
        // GET api/tasks/taskcategories
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

        // POST api/tasks/createGeofenceTask
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

            if (request.TaskCategoryId.HasValue && request.TaskCategoryId.Value > 0)
            {
                var categoryExists = await _context.TaskCategories.AnyAsync(c => c.Id == request.TaskCategoryId.Value && c.IsActive);
                if (!categoryExists) return BadRequest(new { message = "Task Category not found" });
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

            var task = new OBManagementAPI.Models.Task
            {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = locationId,
                Description = request.Description,
                TaskTime = DateTime.Now,
                IsScheduled = false,
                Status = "Pending",
                GeofenceId = request.GeofenceId,
                TaskCategoryId = (request.TaskCategoryId.HasValue && request.TaskCategoryId.Value > 0) ? request.TaskCategoryId : null,
                TriggerType = triggerTypeFormatted,
                IsVisibleToOfficeBoy = false
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Geofence task created successfully",
                taskId = task.Id
            });
        }
    }

    public class CreateTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public int OfficeBoyAccountId { get; set; }
        public int LocationId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TaskMode { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
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

    public class RateTaskRequest
    {
        public int Rating { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class UpdateCurrentLocationRequest
    {
        public int LocationId { get; set; }
    }
}
