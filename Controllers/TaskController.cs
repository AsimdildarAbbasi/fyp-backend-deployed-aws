using Microsoft.AspNetCore.Mvc;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly IDbConnection _db;

        public TasksController(IDbConnection db)
        {
            _db = db;
        }

        // GET api/tasks
        // PURPOSE: Returns all tasks with full details.
        [HttpGet]
        public async Task<IActionResult> GetAllTasks()
        {
            string sql = @"
                SELECT 
                    t.Id AS taskId,
                    t.Description AS description,
                    l.Name AS location,
                    l.Latitude AS latitude,
                    l.Longitude AS longitude,
                    f.Name AS faculty,
                    ob.Name AS officeBoy,
                    t.Status AS status,
                    t.TaskTime AS taskTime,
                    t.Rating AS rating,
                    t.Remarks AS remarks,
                    t.CurrentLocationId AS currentLocationId,
                    cl.Name AS currentLocationName,
                    cl.Latitude AS currentLatitude,
                    cl.Longitude AS currentLongitude,
                    t.ScheduledAt AS scheduledAt,
                    t.IsScheduled AS isScheduled
                FROM Task t
                LEFT JOIN Location l ON t.LocationId = l.Id
                LEFT JOIN Account f ON t.FacultyAccountId = f.Id
                LEFT JOIN Account ob ON t.OfficeBoyAccountId = ob.Id
                LEFT JOIN Location cl ON t.CurrentLocationId = cl.Id
                ORDER BY t.Id DESC";

            var tasks = await _db.QueryAsync<dynamic>(sql);
            return Ok(tasks);
        }

        // GET api/tasks/byfaculty/{facultyId}
        // PURPOSE: Returns only OfficeBoys assigned to the same floor as the Faculty.
        [HttpGet("byfaculty/{facultyId}")]
        public async Task<IActionResult> GetOfficeBoysByFaculty(int facultyId)
        {
            var facultyExists = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Account WHERE Id = @Id AND Role = 2", new { Id = facultyId }) > 0;

            if (!facultyExists)
                return NotFound(new { message = "Faculty not found" });

            var facultyFloorId = await _db.ExecuteScalarAsync<int>(@"
                SELECT TOP 1 o.BuildingFloorId 
                FROM FacultyMemberOffice fmo
                JOIN Office o ON fmo.OfficeId = o.Id
                WHERE fmo.FacultyAccountId = @FacultyAccountId", new { FacultyAccountId = facultyId });

            if (facultyFloorId == 0)
                return NotFound(new { message = "No floor assigned to this faculty" });

            var query = @"
                SELECT 
                    a.Id AS id,
                    a.Name AS name,
                    bf.Number AS floor,
                    o.OfficeName AS officeName
                FROM OfficeBoyAssignedFloors obaf
                JOIN Account a ON obaf.OfficeBoyAccountId = a.Id
                JOIN BuildingFloor bf ON obaf.FloorId = bf.Id
                JOIN Office o ON obaf.OfficeId = o.Id
                WHERE obaf.FloorId = @FloorId AND obaf.Status = 'Active'";

            var dbResult = await _db.QueryAsync<dynamic>(query, new { FloorId = facultyFloorId });

            var officeBoys = dbResult.GroupBy(x => new { x.id, x.name, x.floor })
                .Select(g => new
                {
                    id = g.Key.id,
                    name = g.Key.name,
                    floor = g.Key.floor,
                    assignedOffices = g.Select(x => (string)x.officeName).ToList()
                }).ToList();

            return Ok(officeBoys);
        }

        // POST api/tasks/createTask
        // PURPOSE: Faculty creates a new task and assigns it to an OfficeBoy.
        [HttpPost("createTask")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            var facultyExists = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Account WHERE Id = @Id AND Role = 2", new { Id = request.FacultyAccountId }) > 0;
            if (!facultyExists) return BadRequest(new { message = "Faculty not found" });

            var officeBoyExists = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Account WHERE Id = @Id AND Role = 1", new { Id = request.OfficeBoyAccountId }) > 0;
            if (!officeBoyExists) return BadRequest(new { message = "OfficeBoy not found" });

            var locationExists = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Location WHERE Id = @Id", new { Id = request.LocationId }) > 0;
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

            string sql = @"
                INSERT INTO Task (FacultyAccountId, OfficeBoyAccountId, LocationId, Description, TaskTime, ScheduledAt, IsScheduled, Status)
                OUTPUT INSERTED.Id
                VALUES (@FacultyAccountId, @OfficeBoyAccountId, @LocationId, @Description, GETDATE(), @ScheduledAt, @IsScheduled, 'Pending')";

            int newTaskId = await _db.ExecuteScalarAsync<int>(sql, new {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = request.LocationId,
                Description = request.Description,
                ScheduledAt = scheduledTime,
                IsScheduled = isScheduled
            });

            return Ok(new
            {
                message = isScheduled ? "Task scheduled successfully" : "Task created successfully",
                taskId = newTaskId,
                scheduledAt = scheduledTime
            });
        }

        // PUT api/tasks/{id}/complete
        // PURPOSE: OfficeBoy marks a task as Completed after finishing the work.
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var task = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, Status FROM Task WHERE Id = @Id", new { Id = id });

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.Status == "Completed")
                return BadRequest(new { message = "Task is already completed" });

            await _db.ExecuteAsync(
                "UPDATE Task SET Status = 'Completed' WHERE Id = @Id", new { Id = id });

            return Ok(new { message = "Task marked as completed" });
        }

        // PUT api/tasks/{id}/rate
        // PURPOSE: Faculty adds a rating and remarks to a completed task.
        [HttpPut("{id}/rate")]
        public async Task<IActionResult> RateTask(int id, [FromBody] RateTaskRequest request)
        {
            var task = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, Status FROM Task WHERE Id = @Id", new { Id = id });

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.Status != "Completed")
                return BadRequest(new { message = "Task must be completed before rating" });

            if (request.Rating < 1 || request.Rating > 5)
                return BadRequest(new { message = "Rating must be between 1 and 5" });

            await _db.ExecuteAsync(
                "UPDATE Task SET Rating = @Rating, Remarks = @Remarks WHERE Id = @Id", 
                new { Rating = request.Rating, Remarks = request.Remarks, Id = id });

            return Ok(new { message = "Task rated successfully" });
        }

        // GET api/tasks/completed
        // PURPOSE: Returns all completed tasks.
        [HttpGet("completed")]
        public async Task<IActionResult> GetCompletedTasks()
        {
            string sql = @"
                SELECT 
                    t.Id AS taskId,
                    t.Description AS description,
                    l.Name AS location,
                    f.Name AS faculty,
                    ob.Name AS officeBoy,
                    t.Status AS status,
                    t.TaskTime AS taskTime,
                    t.Rating AS rating,
                    t.Remarks AS remarks,
                    t.CurrentLocationId AS currentLocationId,
                    cl.Name AS currentLocationName,
                    cl.Latitude AS currentLatitude,
                    cl.Longitude AS currentLongitude,
                    t.ScheduledAt AS scheduledAt,
                    t.IsScheduled AS isScheduled
                FROM Task t
                LEFT JOIN Location l ON t.LocationId = l.Id
                LEFT JOIN Account f ON t.FacultyAccountId = f.Id
                LEFT JOIN Account ob ON t.OfficeBoyAccountId = ob.Id
                LEFT JOIN Location cl ON t.CurrentLocationId = cl.Id
                WHERE t.Status = 'Completed'
                ORDER BY t.Id DESC";

            var tasks = await _db.QueryAsync<dynamic>(sql);
            return Ok(tasks);
        }

        // GET api/tasks/Locations
        [HttpGet("Locations")]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _db.QueryAsync<dynamic>(
                "SELECT Id AS id, Name AS name, Latitude AS latitude, Longitude AS longitude FROM Location");
            return Ok(locations);
        }

        // PUT api/tasks/{id}/start
        // PURPOSE: OfficeBoy starts a pending task.
        [HttpPut("{id}/start")]
        public async Task<IActionResult> StartTask(int id)
        {
            var task = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, Status FROM Task WHERE Id = @Id", new { Id = id });

            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "Pending")
                return BadRequest(new { message = "Only Pending tasks can be started." });

            await _db.ExecuteAsync(
                "UPDATE Task SET Status = 'In Progress' WHERE Id = @Id", new { Id = id });

            return Ok(new { message = "Task started successfully. Status is now In Progress." });
        }

        // PUT api/tasks/{id}/update-current-location
        // PURPOSE: Office Boy updates live tracking location.
        [HttpPut("{id}/update-current-location")]
        public async Task<IActionResult> UpdateCurrentLocation(int id, [FromBody] UpdateCurrentLocationRequest request)
        {
            var task = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id, Status FROM Task WHERE Id = @Id", new { Id = id });

            if (task == null) return NotFound(new { message = "Task not found" });

            if (task.Status != "In Progress")
                return BadRequest(new { message = "Task must be 'In Progress' to update location." });

            var locationExists = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Location WHERE Id = @Id", new { Id = request.LocationId }) > 0;

            if (!locationExists)
                return BadRequest(new { message = "Invalid location ID." });

            await _db.ExecuteAsync(
                "UPDATE Task SET CurrentLocationId = @LocationId, LocationUpdatedAt = GETDATE() WHERE Id = @Id",
                new { LocationId = request.LocationId, Id = id });

            return Ok(new { message = "Location updated successfully." });
        }

        // GET api/tasks/active-for-faculty/{facultyId}
        [HttpGet("active-for-faculty/{facultyId}")]
        public async Task<IActionResult> GetActiveTasksForFaculty(int facultyId)
        {
            var facultyFloorId = await _db.ExecuteScalarAsync<int>(@"
                SELECT TOP 1 o.BuildingFloorId 
                FROM FacultyMemberOffice fmo
                JOIN Office o ON fmo.OfficeId = o.Id
                WHERE fmo.FacultyAccountId = @FacultyAccountId", new { FacultyAccountId = facultyId });

            if (facultyFloorId == 0) return Ok(new object[] { });

            string sql = @"
                SELECT 
                    t.Id AS taskId,
                    t.OfficeBoyAccountId AS officeBoyId,
                    ob.Name AS officeBoyName,
                    t.Description AS description,
                    l.Name AS targetLocation,
                    ISNULL(cl.Name, 'Unknown') AS currentLocationName,
                    cl.Latitude AS currentLatitude,
                    cl.Longitude AS currentLongitude
                FROM Task t
                LEFT JOIN Account ob ON t.OfficeBoyAccountId = ob.Id
                LEFT JOIN Location l ON t.LocationId = l.Id
                LEFT JOIN Location cl ON t.CurrentLocationId = cl.Id
                WHERE t.Status = 'In Progress'
                  AND EXISTS (
                      SELECT 1 
                      FROM OfficeBoyAssignedFloors obaf 
                      WHERE obaf.OfficeBoyAccountId = t.OfficeBoyAccountId 
                        AND obaf.FloorId = @FloorId 
                        AND obaf.Status = 'Active')";

            var tasks = await _db.QueryAsync<dynamic>(sql, new { FloorId = facultyFloorId });
            return Ok(tasks);
        }
    }

    public class CreateTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public int OfficeBoyAccountId { get; set; }
        public int LocationId { get; set; }
        public string Description { get; set; }
        public string TaskMode { get; set; }
        public DateTime? ScheduledAt { get; set; }
    }

    public class RateTaskRequest
    {
        public int Rating { get; set; }
        public string Remarks { get; set; }
    }

    public class UpdateCurrentLocationRequest
    {
        public int LocationId { get; set; }
    }
}
