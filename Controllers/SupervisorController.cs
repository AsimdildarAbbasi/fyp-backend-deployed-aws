using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    // ---------------------------------------------------------------------------
    // SupervisorController
    // Handles all Supervisor related APIs.
    //
    // APIs:
    //   1. GET api/supervisor/dashboard  → counts + full list of everything
    //   2. GET api/supervisor/floors     → all floors with offices
    //   3. GET api/supervisor/officeboys → all officeboys
    //   4. GET api/supervisor/faculty    → all faculty with office info
    // ---------------------------------------------------------------------------

    [Route("api/[controller]")]
    [ApiController]
    public class SupervisorController : ControllerBase
    {
        // Database connection injected automatically by .NET
        private readonly ObmanagementContext _context;

        public SupervisorController(ObmanagementContext context)
        {
            _context = context;
        }

        // -----------------------------------------------------------------------
        // GET api/supervisor/dashboard
        //
        // PURPOSE:
        //   Returns total counts AND full data for floors, offices,
        //   officeboys and faculty all in one response.
        //   Supervisor sees counts on cards and full lists below.
        //
        // RESPONSE (200):
        //   {
        //     "totalFloors":     4,
        //     "totalOffices":    8,
        //     "totalOfficeBoys": 4,
        //     "totalFaculty":    4,
        //     "floors":     [ { "floorId": 1, "floorNumber": 1, "offices": [...] } ],
        //     "officeboys": [ { "id": 1, "name": "Ali Raza" } ],
        //     "faculty":    [ { "id": 5, "name": "Dr. Ayesha", "office": "CS Dept", "floor": 1 } ]
        //   }
        // -----------------------------------------------------------------------
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            // Count total floors
            var totalFloors = await _context.BuildingFloors.CountAsync();

            // Count total offices
            var totalOffices = await _context.Offices.CountAsync();

            // Count OfficeBoys (Role = 1)
            var totalOfficeBoys = await _context.Accounts
                .CountAsync(a => a.Role == 1);

            // Count Faculty (Role = 2)
            var totalFaculty = await _context.Accounts
                .CountAsync(a => a.Role == 2);

            // Get full list of all floors with their offices
            var floors = await _context.BuildingFloors
                .Select(f => new
                {
                    floorId = f.Id,
                    floorNumber = f.Number,
                    offices = f.Offices.Select(o => new
                    {
                        id = o.Id,
                        name = o.OfficeName
                    }).ToList()
                })
                .ToListAsync();

            // Get full list of all OfficeBoys
            var officeboys = await _context.Accounts
                .Where(a => a.Role == 1)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name
                })
                .ToListAsync();

            // Get full list of all Faculty with their office and floor
            var faculty = await _context.Accounts
                .Where(a => a.Role == 2)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    office = a.FacultyMemberOffices
                                .Select(f => f.Office.OfficeName)
                                .FirstOrDefault(),
                    floor = a.FacultyMemberOffices
                                .Select(f => f.Office.BuildingFloor.Number)
                                .FirstOrDefault()
                })
                .ToListAsync();

            // Return everything in one response
            return Ok(new
            {
                totalFloors = totalFloors,
                totalOffices = totalOffices,
                totalOfficeBoys = totalOfficeBoys,
                totalFaculty = totalFaculty,
                floors = floors,
                officeboys = officeboys,
                faculty = faculty
            });
        }

        // -----------------------------------------------------------------------
        // GET api/supervisor/floors
        //
        // PURPOSE:
        //   Returns all floors with offices inside each floor.
        //   Called when Supervisor clicks on the Floors card.
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "floorId": 1,
        //       "floorNumber": 1,
        //       "offices": [
        //         { "id": 1, "name": "CS Department Office" },
        //         { "id": 2, "name": "Admin Office" }
        //       ]
        //     }
        //   ]
        // -----------------------------------------------------------------------
        [HttpGet("floors")]
        public async Task<IActionResult> GetFloors()
        {
            var floors = await _context.BuildingFloors
                .Select(f => new
                {
                    floorId = f.Id,
                    floorNumber = f.Number,
                //    offices = f.Offices.Select(o => new
                //    {
                //        id = o.Id,
                //        name = o.OfficeName
                //    }).ToList()
                })
                .ToListAsync();

            return Ok(floors);
        }
        [HttpGet("FloorOffices")]

        public async Task<IActionResult> GetFloorOffices(int id)
        {
            var floorOffices = await _context.Offices
                .Where(f => f.BuildingFloorId == id)
                .Select(f => new
                {
                    OfficeName = f.OfficeName
                })
                .ToListAsync();

            if (floorOffices == null || !floorOffices.Any())
                return NotFound();

            return Ok(floorOffices);
        }

        // -----------------------------------------------------------------------
        // GET api/supervisor/officeboys
        //
        // PURPOSE:
        //   Returns all OfficeBoys with their assigned floors.
        //   Called when Supervisor clicks on the OfficeBoys card.
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "id":             1,
        //       "name":           "Ali Raza",
        //       "assignedFloors": [1, 2],
        //       "assignedOffices": ["CS Department Office", "Admin Office"]
        //     }
        //   ]
        // -----------------------------------------------------------------------
        [HttpGet("officeboys")]
        public async Task<IActionResult> GetOfficeBoys()
        {
            var officeboys = await _context.Accounts
                .Where(a => a.Role == 1)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    // List of floor numbers assigned to this officeboy
                    assignedFloors = a.OfficeBoyAssignedFloors
                        .Select(f => f.Floor.Number)
                        .Distinct()
                        .ToList(),
                    // List of office names assigned to this officeboy
                    //assignedOffices = a.OfficeBoyAssignedFloors
                    //    .Select(f => f.Office.OfficeName)
                    //    .ToList()
                })
                .ToListAsync();

            return Ok(officeboys);
        }

        // -----------------------------------------------------------------------
        // GET api/supervisor/faculty
        //
        // PURPOSE:
        //   Returns all Faculty members with their office and floor info.
        //   Called when Supervisor clicks on the Faculty card.
        //
        // RESPONSE (200):
        //   [
        //     {
        //       "id":     5,
        //       "name":   "Dr. Ayesha Noor",
        //       "office": "CS Department Office",
        //       "floor":  1
        //     }
        //   ]
        // -----------------------------------------------------------------------
        [HttpGet("faculty")]
        public async Task<IActionResult> GetFaculty()
        {
            var faculty = await _context.Accounts
                .Where(a => a.Role == 2)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    office = a.FacultyMemberOffices
                                .Select(f => f.Office.OfficeName)
                                .FirstOrDefault(),
                    floor = a.FacultyMemberOffices
                                .Select(f => f.Office.BuildingFloor.Number)
                                .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(faculty);
        }
    }
}