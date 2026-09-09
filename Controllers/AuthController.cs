using Microsoft.AspNetCore.Mvc;
using System.Data;
using Dapper;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IDbConnection _db;

        public AuthController(IDbConnection db)
        {
            _db = db;
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var query = "SELECT Id, Name, Role FROM Account WHERE Name = @Name AND Password = @Password";
            var account = await _db.QueryFirstOrDefaultAsync<Account>(query, new { Name = request.Name, Password = request.Password });

            if (account == null)
                return Unauthorized(new { message = "Invalid name or password" });

            return Ok(new
            {
                id = account.Id,
                name = account.Name,
                role = account.Role,
                roleText = account.Role == 1 ? "OfficeBoy" :
                           account.Role == 2 ? "Faculty" : "Supervisor"
            });
        }
    }

    public class LoginRequest
    {
        public string Name { get; set; }
        public string Password { get; set; }
    }
}