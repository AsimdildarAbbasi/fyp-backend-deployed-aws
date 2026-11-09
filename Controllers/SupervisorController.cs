using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupervisorController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public SupervisorController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/supervisor/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var totalFloors = await _context.BuildingFloors.CountAsync();
            var totalOffices = await _context.Offices.CountAsync();
            var totalOfficeBoys = await _context.Accounts.CountAsync(a => a.Role == 1);
            var totalFaculty = await _context.Accounts.CountAsync(a => a.Role == 2);

            var dbFloors = await _context.BuildingFloors
                .Select(f => new { floorId = f.Id, floorNumber = f.Number })
                .ToListAsync();

            var dbOffices = await _context.Offices
                .Select(o => new { o.Id, name = o.OfficeName, o.BuildingFloorId })
                .ToListAsync();

            var floors = dbFloors.Select(f => new
            {
                floorId = f.floorId,
                floorNumber = f.floorNumber,
                offices = dbOffices.Where(o => o.BuildingFloorId == f.floorId)
                                   .Select(o => new { id = o.Id, name = o.name })
                                   .ToList()
            }).ToList();

            var officeboys = await _context.Accounts
                .Where(a => a.Role == 1)
                .Select(a => new { id = a.Id, name = a.Name, profileImageUrl = a.ProfileImageUrl })
                .ToListAsync();

            var faculty = await (from a in _context.Accounts
                                 where a.Role == 2
                                 from fmo in _context.FacultyMemberOffices.Where(f => f.FacultyAccountId == a.Id).DefaultIfEmpty()
                                 from o in _context.Offices.Where(o => o.Id == fmo.OfficeId).DefaultIfEmpty()
                                 from bf in _context.BuildingFloors.Where(b => b.Id == o.BuildingFloorId).DefaultIfEmpty()
                                 select new
                                 {
                                     id = a.Id,
                                     name = a.Name,
                                     office = o != null ? o.OfficeName : null,
                                      profileImageUrl = a.ProfileImageUrl,
                                     floor = bf != null ? bf.Number : null
                                 }).ToListAsync();

            return Ok(new
            {
                totalFloors,
                totalOffices,
                totalOfficeBoys,
                totalFaculty,
                floors,
                officeboys,
                faculty
            });
        }

        // GET api/supervisor/floors
        [HttpGet("floors")]
        public async Task<IActionResult> GetFloors()
        {
            var floors = await _context.BuildingFloors
                .Select(f => new { floorId = f.Id, floorNumber = f.Number })
                .ToListAsync();
            return Ok(floors);
        }

        // GET api/supervisor/FloorOffices
        [HttpGet("FloorOffices")]
        public async Task<IActionResult> GetFloorOffices(int id)
        {
            var floorOffices = await _context.Offices
                .Where(o => o.BuildingFloorId == id)
                .Select(o => new { officeName = o.OfficeName })
                .ToListAsync();

            if (floorOffices == null || !floorOffices.Any())
                return NotFound();

            return Ok(floorOffices);
        }

        // GET api/supervisor/officeboys
        [HttpGet("officeboys")]
        public async Task<IActionResult> GetOfficeBoys()
        {
            var officeboys = await _context.Accounts
                .Where(a => a.Role == 1)
                .Select(a => new { id = a.Id, name = a.Name, profileImageUrl = a.ProfileImageUrl })
                .ToListAsync();

            var assignedFloors = await (from obaf in _context.OfficeBoyAssignedFloors
                                        join bf in _context.BuildingFloors on obaf.FloorId equals bf.Id
                                        select new
                                        {
                                            obaf.OfficeBoyAccountId,
                                            obaf.FloorId,
                                            FloorNumber = bf.Number,
                                            obaf.Status
                                        }).ToListAsync();

            var taskLoads = await _context.Tasks
                .Where(t => t.Status == "Pending" || t.Status == "In Progress")
                .GroupBy(t => t.OfficeBoyAccountId)
                .Select(g => new { OfficeBoyAccountId = g.Key, Count = g.Count() })
                .ToListAsync();

            var ratingStats = await _context.Tasks
                .Where(t => t.OfficeBoyAccountId != 0 && t.Rating != null)
                .GroupBy(t => t.OfficeBoyAccountId)
                .Select(g => new
                {
                    OfficeBoyAccountId = g.Key,
                    AverageRating = g.Average(t => (double)t.Rating!.Value),
                    RatedTaskCount = g.Count()
                })
                .ToListAsync();

            var result = officeboys.Select(ob =>
            {
                var activeAssignments = assignedFloors
                    .Where(af => af.OfficeBoyAccountId == ob.id && af.Status == "Active")
                    .ToList();

                var rating = ratingStats.FirstOrDefault(r => r.OfficeBoyAccountId == ob.id);

                return new
                {
                    id = ob.id,
                    name = ob.name,
                    profileImageUrl = ob.profileImageUrl,
                    assignedFloors = activeAssignments
                        .Select(af => af.FloorNumber)
                        .Distinct()
                        .ToList(),
                    assignedFloorIds = activeAssignments
                        .Select(af => af.FloorId)
                        .Distinct()
                        .ToList(),
                    activeTaskCount = taskLoads
                        .FirstOrDefault(l => l.OfficeBoyAccountId == ob.id)?.Count ?? 0,
                    averageRating = rating != null ? Math.Round(rating.AverageRating, 2) : (double?)null,
                    ratedTaskCount = rating != null ? rating.RatedTaskCount : 0
                };
            }).ToList();

            return Ok(result);
        }

        // GET api/supervisor/reviews
        // PURPOSE: Average rating per office boy. Only rated tasks are counted and
        // the list is sorted highest-rated first.
        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews()
        {
            var ratedTasks = await _context.Tasks
                .Where(t => t.Rating != null)
                .Select(t => new
                {
                    taskId = t.Id,
                    officeBoyId = t.OfficeBoyAccountId,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
                    faculty = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    description = t.Description,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    taskTime = t.TaskTime,
                    status = t.Status
                })
                .ToListAsync();

            var reviews = ratedTasks
                .GroupBy(t => new { t.officeBoyId, t.officeBoy, t.officeBoyImageUrl })
                .Select(g => new
                {
                    officeBoyId = g.Key.officeBoyId,
                    officeBoyName = g.Key.officeBoy,
                    officeBoyImageUrl = g.Key.officeBoyImageUrl,
                    averageRating = Math.Round(g.Average(t => t.rating!.Value), 2),
                    ratedTaskCount = g.Count(),
                    reviews = g
                        .OrderByDescending(t => t.rating)
                        .ThenByDescending(t => t.taskId)
                        .Select(t => new
                        {
                            taskId = t.taskId,
                            faculty = t.faculty,
                            description = t.description,
                            rating = t.rating,
                            remarks = t.remarks,
                            taskTime = t.taskTime,
                            status = t.status
                        })
                        .ToList()
                })
                .OrderByDescending(r => r.averageRating)
                .ThenByDescending(r => r.ratedTaskCount)
                .ThenBy(r => r.officeBoyName)
                .ToList();

            return Ok(reviews);
        }

        // GET api/supervisor/reviews/tasks
        // PURPOSE: Flat list of rated tasks sorted highest rating first.
        [HttpGet("reviews/tasks")]
        public async Task<IActionResult> GetRatedTasks()
        {
            var tasks = await _context.Tasks
                .Where(t => t.Rating != null)
                .OrderByDescending(t => t.Rating)
                .ThenByDescending(t => t.Id)
                .Select(t => new
                {
                    taskId = t.Id,
                    officeBoyId = t.OfficeBoyAccountId,
                    officeBoy = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = t.OfficeBoyAccount != null ? t.OfficeBoyAccount.ProfileImageUrl : null,
                    faculty = t.FacultyAccount != null ? t.FacultyAccount.Name : null,
                    description = t.Description,
                    rating = t.Rating,
                    remarks = t.Remarks,
                    taskTime = t.TaskTime,
                    status = t.Status
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // POST api/supervisor/rotate
        // PURPOSE: Two-way rotation. The two office boys swap their active floor
        // assignments and take over each other's open (Pending / In Progress) tasks.
        [HttpPost("rotate")]
        public async Task<IActionResult> Rotate([FromBody] RotateOfficeBoysRequest request)
        {
            if (request.OfficeBoyAId == request.OfficeBoyBId)
                return BadRequest(new { message = "Please choose two different office boys" });

            var supervisorExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.SupervisorAccountId && a.Role == 3);

            if (!supervisorExists)
                return BadRequest(new { message = "Supervisor not found" });

            var validOfficeBoys = await _context.Accounts
                .CountAsync(a => a.Role == 1
                    && (a.Id == request.OfficeBoyAId || a.Id == request.OfficeBoyBId));

            if (validOfficeBoys != 2)
                return BadRequest(new { message = "Both accounts must be office boys" });

            var assignments = await _context.OfficeBoyAssignedFloors
                .Where(a => (a.OfficeBoyAccountId == request.OfficeBoyAId
                        || a.OfficeBoyAccountId == request.OfficeBoyBId)
                    && a.Status == "Active")
                .ToListAsync();

            var assignmentsA = assignments.Where(a => a.OfficeBoyAccountId == request.OfficeBoyAId).ToList();
            var assignmentsB = assignments.Where(a => a.OfficeBoyAccountId == request.OfficeBoyBId).ToList();

            if (assignmentsA.Count == 0 || assignmentsB.Count == 0)
                return BadRequest(new { message = "Both office boys need an active floor assignment before rotating" });

            var onLeave = await _context.LeaveRequests
                .AnyAsync(l => (l.OfficeBoyAccountId == request.OfficeBoyAId
                        || l.OfficeBoyAccountId == request.OfficeBoyBId)
                    && l.Status == "Approved"
                    && l.StartDate.Date <= DateTime.Today
                    && l.EndDate.Date >= DateTime.Today);

            if (onLeave)
                return BadRequest(new { message = "An office boy on leave cannot be rotated" });

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Swap floor assignments between the two office boys.
                foreach (var assignment in assignmentsA)
                    assignment.OfficeBoyAccountId = request.OfficeBoyBId;

                foreach (var assignment in assignmentsB)
                    assignment.OfficeBoyAccountId = request.OfficeBoyAId;

                // Swap open tasks so each office boy picks up the work on the new floor.
                var openTasks = await _context.Tasks
                    .Where(t => (t.OfficeBoyAccountId == request.OfficeBoyAId
                            || t.OfficeBoyAccountId == request.OfficeBoyBId)
                        && (t.Status == "Pending" || t.Status == "In Progress"))
                    .ToListAsync();

                var floorAIds = assignmentsA.Select(a => a.FloorId).Distinct().ToHashSet();
                var floorBIds = assignmentsB.Select(a => a.FloorId).Distinct().ToHashSet();

                // The office boys' new floors are each other's old floors.
                var tasksForA = openTasks
                    .Where(t => t.OfficeBoyAccountId == request.OfficeBoyBId)
                    .ToList();

                var tasksForB = openTasks
                    .Where(t => t.OfficeBoyAccountId == request.OfficeBoyAId)
                    .ToList();

                foreach (var task in tasksForA)
                {
                    task.OfficeBoyAccountId = request.OfficeBoyAId;
                    task.UpdatedAt = DateTime.Now;
                }

                foreach (var task in tasksForB)
                {
                    task.OfficeBoyAccountId = request.OfficeBoyBId;
                    task.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Office boys rotated",
                    officeBoyAId = request.OfficeBoyAId,
                    officeBoyBId = request.OfficeBoyBId,
                    officeBoyANewFloors = floorBIds.OrderBy(id => id).ToList(),
                    officeBoyBNewFloors = floorAIds.OrderBy(id => id).ToList(),
                    tasksMovedToA = tasksForA.Count,
                    tasksMovedToB = tasksForB.Count
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // GET api/supervisor/faculty
        [HttpGet("faculty")]
        public async Task<IActionResult> GetFaculty()
        {
            var faculty = await (from a in _context.Accounts
                                 where a.Role == 2
                                 from fmo in _context.FacultyMemberOffices.Where(f => f.FacultyAccountId == a.Id).DefaultIfEmpty()
                                 from o in _context.Offices.Where(o => o.Id == fmo.OfficeId).DefaultIfEmpty()
                                 from bf in _context.BuildingFloors.Where(b => b.Id == o.BuildingFloorId).DefaultIfEmpty()
                                 select new
                                 {
                                     id = a.Id,
                                     name = a.Name,
                                     office = o != null ? o.OfficeName : null,
                                      profileImageUrl = a.ProfileImageUrl,
                                     floor = bf != null ? bf.Number : null
                                 }).ToListAsync();

            return Ok(faculty);
        }
    }

    public class RotateOfficeBoysRequest
    {
        public int SupervisorAccountId { get; set; }
        public int OfficeBoyAId { get; set; }
        public int OfficeBoyBId { get; set; }
    }
}