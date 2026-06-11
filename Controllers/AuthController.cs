using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public AuthController(ObmanagementContext context)
        {
            _context = context;
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Name == request.Name && a.Password == request.Password);

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