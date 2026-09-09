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
    public class TeacherTrackingController : ControllerBase
    {
        private readonly IDbConnection _db;

        public TeacherTrackingController(IDbConnection db)
        {
            _db = db;
        }

        [HttpPost("set-task")]
        public async Task<IActionResult> SetTask([FromBody] SetTaskRequest request)
        {
            // Deactivate existing task of the same type (Arrival/Departure) for this faculty
            string updateSql = "UPDATE ArrivalDepartureTasks SET IsActive = 0 WHERE FacultyAccountId = @FacultyAccountId AND TaskType = @TaskType AND IsActive = 1";
            await _db.ExecuteAsync(updateSql, new { FacultyAccountId = request.FacultyAccountId, TaskType = request.TaskType });

            // Insert new task
            string insertSql = @"
                INSERT INTO ArrivalDepartureTasks (FacultyAccountId, TaskType, Description, Latitude, Longitude, IsActive)
                VALUES (@FacultyAccountId, @TaskType, @Description, @Latitude, @Longitude, 1)";
            await _db.ExecuteAsync(insertSql, request);

            return Ok(new { message = "Task configuration saved successfully" });
        }

        [HttpPost("update-location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateTeacherLocationRequest request)
        {
            string fetchTasksSql = "SELECT Id, FacultyAccountId, TaskType, Description, Latitude, Longitude, IsActive FROM ArrivalDepartureTasks WHERE FacultyAccountId = @FacultyAccountId AND IsActive = 1";
            var activeTasks = (await _db.QueryAsync<dynamic>(fetchTasksSql, new { FacultyAccountId = request.FacultyAccountId })).ToList();

            if (!activeTasks.Any())
                return Ok(new { message = "No active geofence tasks." });

            bool taskTriggered = false;

            foreach (var geofence in activeTasks)
            {
                double distance = CalculateDistance((double)geofence.Latitude, (double)geofence.Longitude, (double)request.Latitude, (double)request.Longitude);
                
                // If within 500 meters (0.5 km)
                if (distance <= 0.5)
                {
                    string checkSql = @"
                        SELECT COUNT(*) 
                        FROM Task 
                        WHERE FacultyAccountId = @FacultyAccountId 
                          AND Description = @Description 
                          AND CAST(TaskTime AS DATE) = CAST(GETDATE() AS DATE)";

                    int count = await _db.ExecuteScalarAsync<int>(checkSql, new { FacultyAccountId = request.FacultyAccountId, Description = geofence.Description });

                    if (count == 0)
                    {
                        string floorSql = @"
                            SELECT TOP 1 o.BuildingFloorId 
                            FROM FacultyMemberOffice fmo
                            JOIN Office o ON fmo.OfficeId = o.Id
                            WHERE fmo.FacultyAccountId = @FacultyAccountId";

                        int facultyFloorId = await _db.ExecuteScalarAsync<int>(floorSql, new { FacultyAccountId = request.FacultyAccountId });

                        string obSql = @"
                            SELECT TOP 1 OfficeBoyAccountId 
                            FROM OfficeBoyAssignedFloors 
                            WHERE FloorId = @FloorId AND Status = 'Active'";

                        int availableOfficeBoyId = await _db.ExecuteScalarAsync<int>(obSql, new { FloorId = facultyFloorId });

                        if (availableOfficeBoyId != 0)
                        {
                            // Dynamically look up fallback Location ID (preferring "InBIIT" campus location)
                            string locSql = "SELECT TOP 1 Id FROM Location WHERE Name = 'InBIIT'";
                            int fallbackLocationId = await _db.ExecuteScalarAsync<int>(locSql);

                            if (fallbackLocationId == 0)
                            {
                                locSql = "SELECT TOP 1 Id FROM Location";
                                fallbackLocationId = await _db.ExecuteScalarAsync<int>(locSql);
                            }

                            if (fallbackLocationId != 0)
                            {
                                string insertTaskSql = @"
                                    INSERT INTO Task (FacultyAccountId, OfficeBoyAccountId, LocationId, Description, TaskTime, IsScheduled, Status)
                                    VALUES (@FacultyAccountId, @OfficeBoyAccountId, @LocationId, @Description, GETDATE(), 0, 'Pending')";

                                await _db.ExecuteAsync(insertTaskSql, new {
                                    FacultyAccountId = request.FacultyAccountId,
                                    OfficeBoyAccountId = availableOfficeBoyId,
                                    LocationId = fallbackLocationId,
                                    Description = geofence.Description
                                });

                                taskTriggered = true;
                            }
                        }
                    }
                }
            }

            if (taskTriggered)
            {
                return Ok(new { message = "Location updated, task triggered!" });
            }

            return Ok(new { message = "Location updated." });
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; 
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; 
            return d;
        }

        private double Deg2Rad(double deg)
        {
            return deg * (Math.PI / 180);
        }
    }

    public class SetTaskRequest
    {
        public int FacultyAccountId { get; set; }
        public string TaskType { get; set; }
        public string Description { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    public class UpdateTeacherLocationRequest
    {
        public int FacultyAccountId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }
}
