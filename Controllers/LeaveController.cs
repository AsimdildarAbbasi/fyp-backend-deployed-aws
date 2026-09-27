using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public LeaveRequestController(ObmanagementContext context)
        {
            _context = context;
        }

        // POST api/leaverequest/apply
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyLeaveRequest request)
        {
            var officeBoyExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.OfficeBoyAccountId && a.Role == 1);
            if (!officeBoyExists) return BadRequest(new { message = "OfficeBoy not found" });

            if (request.EndDate < request.StartDate)
                return BadRequest(new { message = "EndDate cannot be before StartDate" });

            var leave = new LeaveRequest
            {
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason,
                Status = "Pending",
                RequestedAt = DateTime.Now
            };

            _context.LeaveRequests.Add(leave);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Leave request submitted", leaveId = leave.Id });
        }

        // GET api/leaverequest/pending
        // Supervisor's inbox of leave requests awaiting decision
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var pending = await _context.LeaveRequests
                .Where(l => l.Status == "Pending")
                .OrderByDescending(l => l.Id)
                .Select(l => new
                {
                    leaveId = l.Id,
                    officeBoyId = l.OfficeBoyAccountId,
                    officeBoyName = l.OfficeBoyAccount != null ? l.OfficeBoyAccount.Name : null,
                    startDate = l.StartDate,
                    endDate = l.EndDate,
                    reason = l.Reason,
                    requestedAt = l.RequestedAt
                })
                .ToListAsync();

            return Ok(pending);
        }

        // GET api/leaverequest/officeboy/{id}
        // Office boy's own leave history
        [HttpGet("officeboy/{id}")]
        public async Task<IActionResult> GetByOfficeBoy(int id)
        {
            var leaves = await _context.LeaveRequests
                .Where(l => l.OfficeBoyAccountId == id)
                .OrderByDescending(l => l.Id)
                .Select(l => new
                {
                    leaveId = l.Id,
                    startDate = l.StartDate,
                    endDate = l.EndDate,
                    reason = l.Reason,
                    status = l.Status,
                    supervisorRemarks = l.SupervisorRemarks,
                    substituteOfficeBoyName = l.SubstituteOfficeBoyAccount != null
                        ? l.SubstituteOfficeBoyAccount.Name
                        : null,
                    requestedAt = l.RequestedAt,
                    decidedAt = l.DecidedAt
                })
                .ToListAsync();

            return Ok(leaves);
        }

        // GET api/leaverequest/free-officeboys
        [HttpGet("free-officeboys")]
        public async Task<IActionResult> GetFreeOfficeBoys()
        {
            var officeBoys = await _context.Accounts
                .Where(a => a.Role == 1 && !_context.OfficeBoyAssignedFloors
                    .Any(assignment => assignment.OfficeBoyAccountId == a.Id && assignment.Status == "Active"))
                .OrderBy(a => a.Name)
                .Select(a => new { id = a.Id, name = a.Name })
                .ToListAsync();

            return Ok(officeBoys);
        }

        // PUT api/leaverequest/{id}/decide
        [HttpPut("{id}/decide")]
        public async Task<IActionResult> Decide(int id, [FromBody] DecideLeaveRequest request)
        {
            var supervisorExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.SupervisorAccountId && a.Role == 3); // adjust Role number to match your Supervisor role
            if (!supervisorExists) return BadRequest(new { message = "Supervisor not found" });

            var leave = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (leave == null) return NotFound(new { message = "Leave request not found" });

            if (leave.Status != "Pending")
                return BadRequest(new { message = "This leave request has already been decided" });

            if (request.Approve != true && request.Approve != false)
                return BadRequest(new { message = "Approve must be true or false" });

            var originalAssignment = request.Approve
                ? await _context.OfficeBoyAssignedFloors.FirstOrDefaultAsync(a =>
                    a.OfficeBoyAccountId == leave.OfficeBoyAccountId && a.Status == "Active")
                : null;

            if (request.Approve && originalAssignment != null)
            {
                if (!request.SubstituteOfficeBoyAccountId.HasValue)
                    return BadRequest(new { message = "A substitute office boy is required for this assigned office boy" });

                var substituteExists = await _context.Accounts
                    .AnyAsync(a => a.Id == request.SubstituteOfficeBoyAccountId.Value && a.Role == 1);
                if (!substituteExists)
                    return BadRequest(new { message = "Substitute office boy not found" });

                var substituteIsAssigned = await _context.OfficeBoyAssignedFloors
                    .AnyAsync(a => a.OfficeBoyAccountId == request.SubstituteOfficeBoyAccountId.Value && a.Status == "Active");
                if (substituteIsAssigned)
                    return BadRequest(new { message = "Substitute office boy is not free" });
            }

            var transaction = originalAssignment != null
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                if (originalAssignment != null)
                {
                    originalAssignment.Status = "OnLeave";

                    var substituteAssignment = new OfficeBoyAssignedFloor
                    {
                        FloorId = originalAssignment.FloorId,
                        OfficeId = originalAssignment.OfficeId,
                        OfficeBoyAccountId = request.SubstituteOfficeBoyAccountId!.Value,
                        Status = "Active"
                    };

                    _context.OfficeBoyAssignedFloors.Add(substituteAssignment);
                    await _context.SaveChangesAsync();

                    leave.SubstituteOfficeBoyAccountId = request.SubstituteOfficeBoyAccountId.Value;
                    leave.OriginalAssignmentId = originalAssignment.Id;
                    leave.SubstituteAssignmentId = substituteAssignment.Id;
                }

                leave.Status = request.Approve ? "Approved" : "Rejected";
                leave.SupervisorAccountId = request.SupervisorAccountId;
                leave.SupervisorRemarks = request.Remarks;
                leave.DecidedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                if (transaction != null)
                    await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }

            return Ok(new { message = $"Leave {leave.Status.ToLower()}", leaveId = leave.Id });
        }
    }

    public class ApplyLeaveRequest
    {
        public int OfficeBoyAccountId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = null!;
    }

    public class DecideLeaveRequest
    {
        public int SupervisorAccountId { get; set; }
        public bool Approve { get; set; }
        public string? Remarks { get; set; }
        public int? SubstituteOfficeBoyAccountId { get; set; }
    }
}