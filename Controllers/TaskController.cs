using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;
using OBManagementAPI.Services;
using System.ComponentModel.DataAnnotations;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ObmanagementContext _context;
        private readonly NotificationService _notificationService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public TasksController(
            ObmanagementContext context,
            NotificationService notificationService,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _notificationService = notificationService;
            _environment = environment;
            _configuration = configuration;
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
                    facultyImageUrl = t.FacultyAccount != null ? t.FacultyAccount.ProfileImageUrl : null,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
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
                    isOutsideCampus = t.IsOutsideCampus,
                    updatedAt = t.UpdatedAt,
                    attachments = t.Attachments.Select(a => new
                    {
                        id = a.Id,
                        fileName = a.FileName,
                        path = a.StoredPath,
                        contentType = a.ContentType,
                        sizeBytes = a.SizeBytes
                    }).ToList()
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
                                  where obaf.FloorId == facultyFloorId.Value
                                      && obaf.Status == "Active"
                                      && a.Role == 1
                                  select new
                                  {
                                      id = a.Id,
                                      name = a.Name,
                                      profileImageUrl = a.ProfileImageUrl,
                                      floor = bf.Number,
                                      officeName = o.OfficeName
                                  }).ToListAsync();

            var officeBoys = dbResult.GroupBy(x => new { x.id, x.name, x.profileImageUrl, x.floor })
                .Select(g => new
                {
                    id = g.Key.id,
                    name = g.Key.name,
                    profileImageUrl = g.Key.profileImageUrl,
                    floor = g.Key.floor,
                    assignedOffices = g.Select(x => x.officeName).ToList()
                }).ToList();

            return Ok(officeBoys);
        }

        // POST api/tasks/createTask
        // PURPOSE: Faculty creates a new task (Now or Later) and assigns it to an OfficeBoy.
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

            var isOutsideCampus = await _notificationService
                .IsOutsideCampusAsync(request.LocationId);

            var task = new OBManagementAPI.Models.Task
            {
                FacultyAccountId = request.FacultyAccountId,
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                LocationId = request.LocationId,
                Description = request.Description,
                TaskTime = DateTime.Now,
                ScheduledAt = scheduledTime,
                IsScheduled = isScheduled,
                IsOutsideCampus = isOutsideCampus,
                Status = "Pending"
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyFloorFacultyOfOutsideTaskAsync(
                task, request.Description ?? string.Empty);

            return Ok(new
            {
                message = isScheduled ? "Task scheduled successfully" : "Task created successfully",
                taskId = task.Id,
                scheduledAt = scheduledTime,
                isOutsideCampus
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
                    facultyImageUrl = t.FacultyAccount != null ? t.FacultyAccount.ProfileImageUrl : null,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
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
                    isOutsideCampus = t.IsOutsideCampus,
                    updatedAt = t.UpdatedAt,
                    attachments = t.Attachments.Select(a => new
                    {
                        id = a.Id,
                        fileName = a.FileName,
                        path = a.StoredPath,
                        contentType = a.ContentType,
                        sizeBytes = a.SizeBytes
                    }).ToList()
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // GET api/tasks/byfaculty-tasks/{facultyId}
        // PURPOSE: Returns only the tasks created by the given faculty member.
        [HttpGet("byfaculty-tasks/{facultyId}")]
        public async Task<IActionResult> GetTasksByFaculty(int facultyId)
        {
            var facultyExists = await _context.Accounts
                .AnyAsync(a => a.Id == facultyId && a.Role == 2);

            if (!facultyExists)
                return NotFound(new { message = "Faculty not found" });

            var tasks = await _context.Tasks
                .Where(t => t.FacultyAccountId == facultyId)
                .OrderByDescending(t => t.Id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location != null ? t.Location.Name : null,
                    latitude = t.Location != null ? t.Location.Latitude : null,
                    longitude = t.Location != null ? t.Location.Longitude : null,
                    faculty = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    facultyImageUrl = t.FacultyAccount != null ? t.FacultyAccount.ProfileImageUrl : null,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
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
                    isOutsideCampus = t.IsOutsideCampus,
                    updatedAt = t.UpdatedAt,
                    attachments = t.Attachments.Select(a => new
                    {
                        id = a.Id,
                        fileName = a.FileName,
                        path = a.StoredPath,
                        contentType = a.ContentType,
                        sizeBytes = a.SizeBytes
                    }).ToList()
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // PUT api/tasks/{id}/description
        // PURPOSE: Faculty edits the description while the task is still Pending.
        [HttpPut("{id}/description")]
        public async Task<IActionResult> UpdateDescription(
            int id,
            [FromBody] UpdateTaskDescriptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest(new { message = "Description is required" });

            if (request.Description.Length > 255)
                return BadRequest(new { message = "Description cannot exceed 255 characters" });

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.FacultyAccountId != request.FacultyAccountId)
                return BadRequest(new { message = "Only the faculty member who created this task can edit it" });

            if (task.Status != "Pending")
                return BadRequest(new { message = "Description can only be edited while the task is pending" });

            task.Description = request.Description.Trim();
            task.UpdatedAt = DateTime.Now;
            task.UpdatedByAccountId = request.FacultyAccountId;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Description updated",
                taskId = task.Id,
                description = task.Description,
                updatedAt = task.UpdatedAt
            });
        }

        // POST api/tasks/{id}/attachments
        // PURPOSE: Uploads one or more files for a pending task.
        [HttpPost("{id}/attachments")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAttachments(
            int id,
            [FromForm] List<IFormFile> files)
        {
            var task = await _context.Tasks
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound(new { message = "Task not found" });

            if (task.Status != "Pending")
                return BadRequest(new { message = "Attachments can only be added while the task is pending" });

            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files were uploaded" });

            if (files.Count > 10)
                return BadRequest(new { message = "A task can have at most 10 attachments" });

            var maxFileSizeMb = _configuration.GetValue<int?>("Uploads:MaxFileSizeMb") ?? 10;
            var maxFileSizeBytes = (long)maxFileSizeMb * 1024 * 1024;

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
                ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip"
            };

            var webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            var relativeFolder = _configuration["Uploads:TaskFilesPath"] ?? "uploads/tasks";
            relativeFolder = relativeFolder.Replace('\\', '/').Trim('/');

            var physicalFolder = Path.Combine(
                webRoot,
                relativeFolder.Replace('/', Path.DirectorySeparatorChar),
                id.ToString());

            Directory.CreateDirectory(physicalFolder);

            var created = new List<object>();

            foreach (var file in files)
            {
                if (file.Length <= 0)
                    continue;

                if (file.Length > maxFileSizeBytes)
                    return BadRequest(new { message = $"Each file must be {maxFileSizeMb} MB or smaller" });

                var extension = Path.GetExtension(file.FileName);

                if (!allowedExtensions.Contains(extension))
                    return BadRequest(new { message = $"File type '{extension}' is not allowed" });

                var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var physicalPath = Path.Combine(physicalFolder, storedName);

                await using (var stream = System.IO.File.Create(physicalPath))
                {
                    await file.CopyToAsync(stream);
                }

                var storedPath = $"/{relativeFolder}/{id}/{storedName}";

                var attachment = new TaskAttachment
                {
                    TaskId = task.Id,
                    FileName = Path.GetFileName(file.FileName),
                    StoredPath = storedPath,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType,
                    SizeBytes = file.Length,
                    UploadedAt = DateTime.Now
                };

                _context.TaskAttachments.Add(attachment);

                created.Add(new
                {
                    fileName = attachment.FileName,
                    path = attachment.StoredPath,
                    contentType = attachment.ContentType,
                    sizeBytes = attachment.SizeBytes
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Attachments uploaded",
                taskId = task.Id,
                attachments = created
            });
        }

        // DELETE api/tasks/attachments/{attachmentId}
        // PURPOSE: Removes an attachment while its task is still Pending.
        [HttpDelete("attachments/{attachmentId}")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            var attachment = await _context.TaskAttachments
                .Include(a => a.Task)
                .FirstOrDefaultAsync(a => a.Id == attachmentId);

            if (attachment == null)
                return NotFound(new { message = "Attachment not found" });

            if (attachment.Task.Status != "Pending")
                return BadRequest(new { message = "Attachments can only be removed while the task is pending" });

            var webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            var physicalPath = Path.Combine(
                webRoot,
                attachment.StoredPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (System.IO.File.Exists(physicalPath))
                System.IO.File.Delete(physicalPath);

            _context.TaskAttachments.Remove(attachment);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Attachment removed" });
        }

//location work 
public class CreateLocationDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = null!;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

}
[HttpPost("Locations")]
public async Task<IActionResult> CreateLocation([FromBody] CreateLocationDto dto)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);

    var location = new Location
    {
        Name = dto.Name.Trim(),
        Latitude = dto.Latitude,
        Longitude = dto.Longitude
    };

    _context.Locations.Add(location);
    await _context.SaveChangesAsync();

    // return in the same shape as GetLocations
    return Ok(new
    {
        id = location.Id,
        name = location.Name,
        latitude = location.Latitude,
        longitude = location.Longitude
    });
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
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
                    description = t.Description,
                    targetLocation = t.Location != null ? t.Location.Name : null,
                    currentLocationName = t.CurrentLocation != null ? t.CurrentLocation.Name : "Unknown",
                    currentLatitude = t.CurrentLocation != null ? t.CurrentLocation.Latitude : null,
                    currentLongitude = t.CurrentLocation != null ? t.CurrentLocation.Longitude : null
                })
                .ToListAsync();

            return Ok(tasks);
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

    public class RateTaskRequest
    {
        public int Rating { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public class UpdateTaskDescriptionRequest
    {
        public int FacultyAccountId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateCurrentLocationRequest
    {
        public int LocationId { get; set; }
    }
}