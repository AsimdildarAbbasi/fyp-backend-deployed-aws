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
                .Select(a => new { id = a.Id, name = a.Name })
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
                .Select(a => new { id = a.Id, name = a.Name })
                .ToListAsync();

            var assignedFloors = await (from obaf in _context.OfficeBoyAssignedFloors
                                        join bf in _context.BuildingFloors on obaf.FloorId equals bf.Id
                                        select new
                                        {
                                            obaf.OfficeBoyAccountId,
                                            FloorNumber = bf.Number
                                        }).ToListAsync();

            var result = officeboys.Select(ob => new
            {
                id = ob.id,
                name = ob.name,
                assignedFloors = assignedFloors.Where(af => af.OfficeBoyAccountId == ob.id)
                                               .Select(af => af.FloorNumber)
                                               .Distinct()
                                               .ToList()
            }).ToList();

            return Ok(result);
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
                                     floor = bf != null ? bf.Number : null
                                 }).ToListAsync();

            return Ok(faculty);
        }
    }
}