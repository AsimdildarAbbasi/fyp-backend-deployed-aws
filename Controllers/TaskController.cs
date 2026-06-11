using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    // ---------------------------------------------------------------------------
    // TasksController
    // Handles all Task related APIs.
    //
    // APIs:
    //   1. GET    api/tasks                  → Get all tasks
    //   2. GET    api/tasks/{id}             → Get single task by ID
    //   3. POST   api/tasks                  → Faculty creates a new task
    //   4. PUT    api/tasks/{id}/complete    → OfficeBoy marks task as completed
    //   5. PUT    api/tasks/{id}/rate        → Faculty rates a completed task
    //   6. GET    api/tasks/pending          → Get all pending tasks
    //   7. GET    api/tasks/completed        → Get all completed tasks
    // ---------------------------------------------------------------------------

    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        // Database connection injected automatically by .NET
        private readonly ObmanagementContext _context;

        public TasksController(ObmanagementContext context)
        {
            _context = context;
        }

        // -----------------------------------------------------------------------
        // GET api/tasks
        //
        // PURPOSE:
        //   Returns all tasks with full details.
        //   Used by Supervisor to see every task in the system.
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "taskId":      1,
        //       "description": "Print exam papers",
        //       "location":    "Photocopy Room",
        //       "faculty":     "Dr. Ayesha Noor",
        //       "officeBoy":   "Ali Raza",
        //       "status":      "Pending",
        //       "taskTime":    "2025-01-10T09:00:00",
        //       "rating":      null,
        //       "remarks":     null
        //     }
        //   ]
        // -----------------------------------------------------------------------
        [HttpGet("{id}/tasks")]
        public async Task<IActionResult> GetTasksByOfficeBoy(int id)
        {
            var officeBoy = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.Role == 1);

            if (officeBoy == null)
                return NotFound(new { message = "OfficeBoy not found" });

            var tasks = await _context.Tasks
                .Include(t => t.Location)           // ← ADD THIS
                .Include(t => t.FacultyAccount)     // ← ADD THIS
                .Include(t => t.CurrentLocation)
                .Where(t => t.OfficeBoyAccountId == id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,
                    latitude = t.Location.Latitude,
                    longitude = t.Location.Longitude,
                    assignedBy = t.FacultyAccount.Name,
                    status = t.Status,
                    taskTime = t.TaskTime,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .OrderByDescending(t => t.taskTime)
                .ToListAsync();

            return Ok(tasks);
        }
   

        // -----------------------------------------------------------------------
        // GET api/tasks/{id}
        //
        // PURPOSE:
        //   Returns a single task by its ID with full details.
        //   Used when clicking on a task to see its details.
        //
        // URL PARAM:
        //   id → Task ID
        //
        // RESPONSE (200): single task object (same fields as GetAllTasks)
        // RESPONSE (404): { "message": "Task not found" }
        // -----------------------------------------------------------------------
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Location)            // 🔥 REQUIRED
                .Include(t => t.FacultyAccount)
                .Include(t => t.OfficeBoyAccount)
                .Include(t => t.CurrentLocation)
                .Where(t => t.Id == id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,

                    latitude = t.Location.Latitude,    // ← ADD THIS
                    longitude = t.Location.Longitude,// ✅ now works

                    faculty = t.FacultyAccount.Name,
                    officeBoy = t.OfficeBoyAccount.Name,

                    status = t.Status,
                    taskTime = t.TaskTime,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .FirstOrDefaultAsync();

            if (task == null)
                return NotFound(new { message = "Task not found" });

            return Ok(task);
        }
        // -----------------------------------------------------------------------
        // GET api/officeboys/byfaculty/{facultyId}
        //
        // PURPOSE:
        //   Returns only OfficeBoys assigned to the same floor as the Faculty.
        //   Faculty sees these OfficeBoys on their dashboard to assign tasks.
        //
        // URL PARAM:
        //   facultyId → the logged in Faculty's Account ID
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "id":    1,
        //       "name":  "Ali Raza",
        //       "floor": 1,
        //       "assignedOffices": ["CS Department Office", "Admin Office"]
        //     }
        //   ]
        //
        // RESPONSE (404):
        //   { "message": "Faculty not found" }
        //   { "message": "No floor assigned to this faculty" }
        // -----------------------------------------------------------------------
        [HttpGet("byfaculty/{facultyId}")]
        public async Task<IActionResult> GetOfficeBoysByFaculty(int facultyId)
        {
            // Step 1 — Check faculty exists
            var faculty = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == facultyId && a.Role == 2);

            if (faculty == null)
                return NotFound(new { message = "Faculty not found" });

            // Step 2 — Get the floor number of this faculty's office
            var facultyFloorId = await _context.FacultyMemberOffices
                .Where(f => f.FacultyAccountId == facultyId)
                .Select(f => f.Office.BuildingFloorId)
                .FirstOrDefaultAsync();

            if (facultyFloorId == 0)
                return NotFound(new { message = "No floor assigned to this faculty" });

            // Step 3 — Get OfficeBoys assigned to that same floor
            var officeBoys = await _context.OfficeBoyAssignedFloors
    .Where(o => o.FloorId == facultyFloorId && o.Status == "Active")
    .GroupBy(o => new { o.OfficeBoyAccount.Id, o.OfficeBoyAccount.Name, o.Floor.Number })
    .Select(g => new
    {
        id = g.Key.Id,
        name = g.Key.Name,
        floor = g.Key.Number,
        assignedOffices = g.Select(x => x.Office.OfficeName).ToList()
    })
    .ToListAsync();

            return Ok(officeBoys);
        }


        // -----------------------------------------------------------------------
        // POST api/tasks
        //
        // PURPOSE:
        //   Faculty creates a new task and assigns it to an OfficeBoy.
        //   Status is automatically set to "Pending".
        //   TaskTime is automatically set to current date and time.
        //
        // REQUEST BODY:
        //   {
        //     "facultyAccountId":   5,
        //     "officeBoyAccountId": 1,
        //     "locationId":         3,
        //     "description":        "Clean the main corridor before the event"
        //   }
        //
        // RESPONSE (200):
        //   { "message": "Task created successfully", "taskId": 1 }
        //
        // RESPONSE (400):
        //   { "message": "Faculty not found" }
        //   { "message": "OfficeBoy not found" }
        //   { "message": "Location not found" }
        // -----------------------------------------------------------------------

        [HttpPost("createTask")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            // Validate Faculty
            var faculty = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.FacultyAccountId && a.Role == 2);

            if (faculty == null)
                return BadRequest(new { message = "Faculty not found" });

            // Validate OfficeBoy
            var officeBoy = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == request.OfficeBoyAccountId && a.Role == 1);

            if (officeBoy == null)
                return BadRequest(new { message = "OfficeBoy not found" });

            // Validate Location
            var location = await _context.Locations
                .FirstOrDefaultAsync(l => l.Id == request.LocationId);

            if (location == null)
                return BadRequest(new { message = "Location not found" });

            // Validate Task Mode
            if (string.IsNullOrEmpty(request.TaskMode))
                return BadRequest(new { message = "TaskMode is required (now/later)" });

            bool isScheduled = request.TaskMode.ToLower() == "later";

            DateTime scheduledTime;

            if (isScheduled)
            {
                if (!request.ScheduledAt.HasValue)
                    return BadRequest(new { message = "ScheduledAt is required for later tasks" });

                if (request.ScheduledAt <= DateTime.Now)
                    return BadRequest(new { message = "Scheduled time must be in future" });

                scheduledTime = request.ScheduledAt.Value;
            }
            else
            {
                scheduledTime = DateTime.Now;
            }

            // Create Task
            var task = new OBManagementAPI.Models.Task
            {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = request.LocationId,
                Description = request.Description,

                TaskTime = DateTime.Now,

                ScheduledAt = scheduledTime,
                IsScheduled = isScheduled,

                Status =  "Pending",

                Rating = null,
                Remarks = null
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = isScheduled ? "Task scheduled successfully" : "Task created successfully",
                taskId = task.Id,
                scheduledAt = task.ScheduledAt
            });
        }
        // -----------------------------------------------------------------------
        // PUT api/tasks/{id}/complete: 'An error occurred while saving the entity changes. See the inner exception for details
        //
        // PURPOSE:
        //   OfficeBoy marks a task as Completed after finishing the work.
        //   Only tasks that are currently "Pending" can be completed.
        //
        // URL PARAM:
        //   id → Task ID
        //
        // RESPONSE (200): { "message": "Task marked as completed" }
        // RESPONSE (404): { "message": "Task not found" }
        // RESPONSE (400): { "message": "Task is already completed" }
        // -----------------------------------------------------------------------
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteTask(int id)
        {
            // Find the task by ID
            var task = await _context.Tasks.FindAsync(id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            // Prevent marking an already completed task
            if (task.Status == "Completed")
                return BadRequest(new { message = "Task is already completed" });

            // Update status to Completed
            task.Status = "Completed";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task marked as completed" });
        }

        // -----------------------------------------------------------------------
        // PUT api/tasks/{id}/rate
        //
        // PURPOSE:
        //   Faculty adds a rating and remarks to a completed task.
        //   Only completed tasks can be rated.
        //
        // URL PARAM:
        //   id → Task ID
        //
        // REQUEST BODY:
        //   {
        //     "rating":  5,
        //     "remarks": "Excellent work, done very quickly"
        //   }
        //
        // RESPONSE (200): { "message": "Task rated successfully" }
        // RESPONSE (404): { "message": "Task not found" }
        // RESPONSE (400): { "message": "Task must be completed before rating" }
        // RESPONSE (400): { "message": "Rating must be between 1 and 5" }
        // -----------------------------------------------------------------------
        [HttpPut("{id}/rate")]
        public async Task<IActionResult> RateTask(int id, [FromBody] RateTaskRequest request)
        {
            // Find the task by ID
            var task = await _context.Tasks.FindAsync(id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            // Only completed tasks can be rated
            if (task.Status != "Completed")
                return BadRequest(new { message = "Task must be completed before rating" });

            // Rating must be between 1 and 5
            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5" });

            // Update rating and remarks
            task.Rating = request.Rating;
            task.Remarks = request.Remarks;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task rated successfully" });
        }

        // -----------------------------------------------------------------------
        // GET api/tasks/pending
        //
        // PURPOSE:
        //   Returns all tasks that are currently Pending.
        //   Used by Supervisor or OfficeBoy to see unfinished tasks.
        //
        // RESPONSE (200): list of pending tasks (same fields as GetAllTasks)
        // -----------------------------------------------------------------------
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingTasks()
        {
            // Filter tasks where Status = "Pending"
            var tasks = await _context.Tasks
                .Include(t => t.CurrentLocation)
                .Where(t => t.Status == "Pending")
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,
                    faculty = t.FacultyAccount.Name,
                    officeBoy = t.OfficeBoyAccount.Name,
                    status = t.Status,
                    taskTime = t.TaskTime,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // -----------------------------------------------------------------------
        // GET api/tasks/completed
        //
        // PURPOSE:
        //   Returns all tasks that have been completed.
        //   Used by Supervisor or Faculty to see finished tasks with ratings.
        //
        // RESPONSE (200): list of completed tasks (same fields as GetAllTasks)
        // -----------------------------------------------------------------------
        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedTasks()
        {
            // Filter tasks where Status = "Completed"
            var tasks = await _context.Tasks
                .Include(t => t.CurrentLocation)
                .Where(t => t.Status == "Completed")
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,
                    faculty = t.FacultyAccount.Name,
                    officeBoy = t.OfficeBoyAccount.Name,
                    status = t.Status,
                    taskTime = t.TaskTime,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .ToListAsync();

            return Ok(tasks);
        }


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

        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            var tasks = await _context.Tasks
                .Include(t => t.Location)
                .Include(t => t.FacultyAccount)
                .Include(t => t.OfficeBoyAccount)
                .Include(t => t.CurrentLocation)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,
                    latitude = t.Location.Latitude,
                    longitude = t.Location.Longitude,
                    faculty = t.FacultyAccount.Name,
                    officeBoy = t.OfficeBoyAccount.Name,
                    status = t.Status,
                    taskTime = t.TaskTime,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    currentLocationId = t.CurrentLocationId,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : null,
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .OrderByDescending(t => t.taskTime)
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpPut("{id}/start")]
        public async Task<IActionResult> StartTask(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "Pending")
                return BadRequest(new { message = "Only Pending tasks can be started." });

            task.Status = "In Progress";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Task started successfully. Status is now In Progress." });
        }

        [HttpPut("{id}/update-current-location")]
        public async Task<IActionResult> UpdateCurrentLocation(int id, [FromBody] UpdateCurrentLocationRequest request)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "In Progress")
                return BadRequest(new { message = "Task must be 'In Progress' to update location." });

            var locationExists = await _context.Locations.AnyAsync(l => l.Id == request.LocationId);
            if (!locationExists)
                return BadRequest(new { message = "Invalid location ID." });

            task.CurrentLocationId = request.LocationId;
            task.LocationUpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Location updated successfully.", updatedAt = task.LocationUpdatedAt });
        }

        [HttpGet("active-on-floor/{floorId}")]
        public async Task<IActionResult> GetActiveTasksOnFloor(int floorId)
        {
            var tasks = await _context.Tasks
                .Include(t => t.OfficeBoyAccount)
                .Include(t => t.CurrentLocation)
                .Include(t => t.Location)
                .Where(t => t.Status == "In Progress" && 
                            _context.OfficeBoyAssignedFloors.Any(o => o.OfficeBoyAccountId == t.OfficeBoyAccountId && o.FloorId == floorId && o.Status == "Active"))
                .Select(t => new
                {
                    taskId = t.Id,
                    officeBoyId = t.OfficeBoyAccountId,
                    officeBoyName = t.OfficeBoyAccount.Name,
                    description = t.Description,
                    targetLocation = t.Location.Name,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : "Unknown",
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : (double?)null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : (double?)null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpGet("active-for-faculty/{facultyId}")]
        public async Task<IActionResult> GetActiveTasksForFaculty(int facultyId)
        {
            var facultyFloorId = await _context.FacultyMemberOffices
                .Where(f => f.FacultyAccountId == facultyId)
                .Select(f => f.Office.BuildingFloorId)
                .FirstOrDefaultAsync();

            if (facultyFloorId == 0) return Ok(new object[] { });

            var tasks = await _context.Tasks
                .Include(t => t.OfficeBoyAccount)
                .Include(t => t.CurrentLocation)
                .Include(t => t.Location)
                .Where(t => t.Status == "In Progress" && 
                            _context.OfficeBoyAssignedFloors.Any(o => o.OfficeBoyAccountId == t.OfficeBoyAccountId && o.FloorId == facultyFloorId && o.Status == "Active"))
                .Select(t => new
                {
                    taskId = t.Id,
                    officeBoyId = t.OfficeBoyAccountId,
                    officeBoyName = t.OfficeBoyAccount.Name,
                    description = t.Description,
                    targetLocation = t.Location.Name,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : "Unknown",
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : (double?)null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : (double?)null
                })
                .ToListAsync();

            return Ok(tasks);
        }
    }
        // ---------------------------------------------------------------------------
        // CreateTaskRequest
        // Request body model for POST api/tasks
        // Faculty sends these fields when creating a task
        // ---------------------------------------------------------------------------
        public class CreateTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public int OfficeBoyAccountId { get; set; }
        public int LocationId { get; set; }
        public string Description { get; set; }

        public string TaskMode { get; set; } // "now" or "later"

        public DateTime? ScheduledAt { get; set; } // required if later
    }

    // ---------------------------------------------------------------------------
    // RateTaskRequest
    // Request body model for PUT api/tasks/{id}/rate
    // Faculty sends these fields when rating a completed task
    // ---------------------------------------------------------------------------
    public class RateTaskRequest
        {
            public int Rating { get; set; }  // Rating from 1 to 5
            public string Remarks { get; set; }  // Optional feedback/comments
        }
        
    public class UpdateCurrentLocationRequest
    {
        public int LocationId { get; set; }
    }

    }
