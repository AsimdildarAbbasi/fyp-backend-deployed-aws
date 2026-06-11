using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    // ---------------------------------------------------------------------------
    // OfficeBoyController
    // Handles all OfficeBoy related APIs.
    //
    // APIs:
    //   1. GET api/officeboys         → Get all officeboys
    //   2. GET api/officeboys/{id}/tasks → Get tasks assigned to an officeboy
    // ---------------------------------------------------------------------------

    [Route("api/[controller]")]
    [ApiController]
    public class OfficeBoyController : ControllerBase
    {
        // Database connection injected automatically by .NET
        private readonly ObmanagementContext _context;

        public OfficeBoyController(ObmanagementContext context)
        {
            _context = context;
        }

        // -----------------------------------------------------------------------
        // GET api/officeboys
        //
        // PURPOSE:
        //   Returns a list of all OfficeBoys.
        //   Used by Faculty when creating a task to pick an OfficeBoy.
        //   Used by Supervisor to see all OfficeBoys.
        //
        // RESPONSE (200):
        //   [
        //     { "id": 1, "name": "Ali Raza" },
        //     { "id": 2, "name": "Usman Tariq" }
        //   ]
        // -----------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllOfficeBoys()
        {
            // Filter Account table where Role = 1 (OfficeBoy only)
            var officeBoys = await _context.Accounts
                .Where(a => a.Role == 1)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name
                })
                .ToListAsync();

            return Ok(officeBoys);
        }

        // -----------------------------------------------------------------------
        // GET api/officeboys/{id}/tasks
        //
        // PURPOSE:
        //   Returns all tasks assigned to a specific OfficeBoy.
        //   OfficeBoy uses this to see his task list on his home screen.
        //
        // URL PARAM:
        //   id → the OfficeBoy's Account ID
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "taskId":      1,
        //       "description": "Print exam papers",
        //       "location":    "Photocopy Room",
        //       "assignedBy":  "Dr. Ayesha Noor",
        //       "status":      "Pending",
        //       "taskTime":    "2025-01-10T09:00:00",
        //       "rating":      null,
        //       "remarks":     null
        //     }
        //   ]
        //
        // RESPONSE (404):
        //   { "message": "OfficeBoy not found" }
        // -----------------------------------------------------------------------
        [HttpGet("{id}/tasks")]
        public async Task<IActionResult> GetTasksByOfficeBoy(int id)
        {
            // First check if this OfficeBoy exists in the Account table
            var officeBoy = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.Role == 1);

            if (officeBoy == null)
                return NotFound(new { message = "OfficeBoy not found" });

            // Get all tasks where OfficeBoyAccountId matches the given id
            // Also join Location and Faculty tables to get their names
            var tasks = await _context.Tasks
                .Include(t => t.Location)
                .Include(t => t.CurrentLocation)
                .Where(t => t.OfficeBoyAccountId == id)
                .Select(t => new
                {
                    taskId = t.Id,
                    description = t.Description,
                    location = t.Location.Name,         // from Location table
                    latitude = t.Location.Latitude,
                    longitude = t.Location.Longitude,
                    assignedBy = t.FacultyAccount.Name,   // from Account table (Faculty)
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
    }
}