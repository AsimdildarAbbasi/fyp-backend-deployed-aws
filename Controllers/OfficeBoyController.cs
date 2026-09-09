using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfficeBoyController : ControllerBase
    {
        private readonly IDbConnection _db;

        public OfficeBoyController(IDbConnection db)
        {
            _db = db;
        }

        // GET api/officeboy/{id}/tasks
        [HttpGet("{id}/tasks")]
        public async Task<IActionResult> GetTasksByOfficeBoy(int id)
        {
            var officeBoy = await _db.QueryFirstOrDefaultAsync<Account>(
                "SELECT Id FROM Account WHERE Id = @Id AND Role = 1", new { Id = id });

            if (officeBoy == null)
                return NotFound(new { message = "OfficeBoy not found" });

            string sql = @"
                SELECT 
                    t.Id AS taskId,
                    t.Description,
                    l.Name AS location,
                    l.Latitude AS latitude,
                    l.Longitude AS longitude,
                    f.Name AS assignedBy,
                    t.Status,
                    t.TaskTime,
                    t.Rating,
                    t.Remarks,
                    t.CurrentLocationId,
                    cl.Name AS currentLocationName,
                    cl.Latitude AS currentLatitude,
                    cl.Longitude AS currentLongitude
                FROM Task t
                JOIN Location l ON t.LocationId = l.Id
                JOIN Account f ON t.FacultyAccountId = f.Id
                LEFT JOIN Location cl ON t.CurrentLocationId = cl.Id
                WHERE t.OfficeBoyAccountId = @OfficeBoyId";

            var tasks = await _db.QueryAsync<dynamic>(sql, new { OfficeBoyId = id });

            return Ok(tasks);
        }
    }
}