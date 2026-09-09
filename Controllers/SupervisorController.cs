using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupervisorController : ControllerBase
    {
        private readonly IDbConnection _db;

        public SupervisorController(IDbConnection db)
        {
            _db = db;
        }

        // GET api/supervisor/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var totalFloors = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM BuildingFloor");
            var totalOffices = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Office");
            var totalOfficeBoys = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Account WHERE Role = 1");
            var totalFaculty = await _db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Account WHERE Role = 2");

            var dbFloors = await _db.QueryAsync<dynamic>("SELECT Id AS floorId, Number AS floorNumber FROM BuildingFloor");
            var dbOffices = await _db.QueryAsync<dynamic>("SELECT Id, OfficeName AS name, BuildingFloorId FROM Office");

            var floors = dbFloors.Select(f => new
            {
                floorId = f.floorId,
                floorNumber = f.floorNumber,
                offices = dbOffices.Where(o => o.BuildingFloorId == f.floorId)
                                   .Select(o => new { id = o.Id, name = o.name })
                                   .ToList()
            }).ToList();

            var officeboys = await _db.QueryAsync<dynamic>("SELECT Id AS id, Name AS name FROM Account WHERE Role = 1");

            var faculty = await _db.QueryAsync<dynamic>(@"
                SELECT 
                    a.Id AS id, 
                    a.Name AS name, 
                    o.OfficeName AS office, 
                    bf.Number AS floor 
                FROM Account a 
                LEFT JOIN FacultyMemberOffice fmo ON a.Id = fmo.FacultyAccountId 
                LEFT JOIN Office o ON fmo.OfficeId = o.Id 
                LEFT JOIN BuildingFloor bf ON o.BuildingFloorId = bf.Id 
                WHERE a.Role = 2");

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
            var floors = await _db.QueryAsync<dynamic>("SELECT Id AS floorId, Number AS floorNumber FROM BuildingFloor");
            return Ok(floors);
        }

        // GET api/supervisor/FloorOffices
        [HttpGet("FloorOffices")]
        public async Task<IActionResult> GetFloorOffices(int id)
        {
            var floorOffices = await _db.QueryAsync<dynamic>(
                "SELECT OfficeName FROM Office WHERE BuildingFloorId = @FloorId", new { FloorId = id });

            if (floorOffices == null || !floorOffices.Any())
                return NotFound();

            return Ok(floorOffices);
        }

        // GET api/supervisor/officeboys
        [HttpGet("officeboys")]
        public async Task<IActionResult> GetOfficeBoys()
        {
            var officeboys = await _db.QueryAsync<dynamic>("SELECT Id AS id, Name AS name FROM Account WHERE Role = 1");
            var assignedFloors = await _db.QueryAsync<dynamic>(@"
                SELECT obaf.OfficeBoyAccountId, bf.Number AS FloorNumber 
                FROM OfficeBoyAssignedFloors obaf
                JOIN BuildingFloor bf ON obaf.FloorId = bf.Id");

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
            var faculty = await _db.QueryAsync<dynamic>(@"
                SELECT 
                    a.Id AS id, 
                    a.Name AS name, 
                    o.OfficeName AS office, 
                    bf.Number AS floor 
                FROM Account a 
                LEFT JOIN FacultyMemberOffice fmo ON a.Id = fmo.FacultyAccountId 
                LEFT JOIN Office o ON fmo.OfficeId = o.Id 
                LEFT JOIN BuildingFloor bf ON o.BuildingFloorId = bf.Id 
                WHERE a.Role = 2");

            return Ok(faculty);
        }
    }
}