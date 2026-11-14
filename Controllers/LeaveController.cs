using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LeaveRequestController : ControllerBase
    {
        private const string Pending = "Pending";
        private const string Approved = "Approved";
        private const string Rejected = "Rejected";

        private readonly ObmanagementContext _context;

        public LeaveRequestController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/leaverequest/policy
        [HttpGet("policy")]
        public async Task<IActionResult> GetPolicy()
        {
            var policy = await GetActivePolicyAsync();

            return Ok(new
            {
                yearlyLimit = policy.YearlyLimit,
                monthlyLimit = policy.MonthlyLimit,
                effectiveFrom = policy.EffectiveFrom
            });
        }

        // PUT api/leaverequest/policy
        [HttpPut("policy")]
        public async Task<IActionResult> UpdatePolicy([FromBody] UpdateLeavePolicyRequest request)
        {
            var supervisorExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.SupervisorAccountId && a.Role == 3);

            if (!supervisorExists)
                return BadRequest(new { message = "Supervisor not found" });

            if (request.YearlyLimit <= 0 || request.MonthlyLimit <= 0)
                return BadRequest(new { message = "Leave limits must be greater than zero" });

            if (request.MonthlyLimit > request.YearlyLimit)
                return BadRequest(new { message = "Monthly limit cannot exceed the yearly limit" });

            var policy = await _context.LeavePolicies
                .Where(p => p.Role == 1 && p.IsActive)
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (policy == null)
            {
                policy = new LeavePolicy
                {
                    Role = 1,
                    YearlyLimit = request.YearlyLimit,
                    MonthlyLimit = request.MonthlyLimit,
                    EffectiveFrom = DateTime.Now,
                    IsActive = true
                };
                _context.LeavePolicies.Add(policy);
            }
            else
            {
                policy.YearlyLimit = request.YearlyLimit;
                policy.MonthlyLimit = request.MonthlyLimit;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Leave policy updated",
                yearlyLimit = policy.YearlyLimit,
                monthlyLimit = policy.MonthlyLimit
            });
        }

        // POST api/leaverequest/apply
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyLeaveRequest request)
        {
            var officeBoyExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.OfficeBoyAccountId && a.Role == 1);

            if (!officeBoyExists)
                return BadRequest(new { message = "OfficeBoy not found" });

            var start = request.StartDate.Date;
            var end = request.EndDate.Date;

            if (end < start)
                return BadRequest(new { message = "EndDate cannot be before StartDate" });

            var existingLeaves = await _context.LeaveRequests
                .Where(l => l.OfficeBoyAccountId == request.OfficeBoyAccountId
                    && (l.Status == Pending || l.Status == Approved))
                .ToListAsync();

            if (existingLeaves.Any(l => start <= l.EndDate.Date && end >= l.StartDate.Date))
                return BadRequest(new { message = "This leave overlaps with an existing leave request" });

            var policy = await GetActivePolicyAsync();
            var limitError = ValidateLimits(start, end, existingLeaves, policy);

            if (limitError != null)
                return BadRequest(new { message = limitError });

            var leave = new LeaveRequest
            {
                OfficeBoyAccountId = request.OfficeBoyAccountId,
                StartDate = start,
                EndDate = end,
                Reason = request.Reason,
                Status = Pending,
                RequestedAt = DateTime.Now
            };

            _context.LeaveRequests.Add(leave);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Leave request submitted", leaveId = leave.Id });
        }

        // GET api/leaverequest/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var pending = await _context.LeaveRequests
                .Where(l => l.Status == Pending)
                .OrderByDescending(l => l.Id)
                .Select(l => new
                {
                    leaveId = l.Id,
                    officeBoyId = l.OfficeBoyAccountId,
                    officeBoyName = l.OfficeBoyAccount != null ? l.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = l.OfficeBoyAccount != null ? l.OfficeBoyAccount.ProfileImageUrl : null,
                    startDate = l.StartDate,
                    endDate = l.EndDate,
                    reason = l.Reason,
                    requestedAt = l.RequestedAt,
                    hasActiveAssignment = _context.OfficeBoyAssignedFloors
                        .Any(a => a.OfficeBoyAccountId == l.OfficeBoyAccountId && a.Status == "Active"),
                    assignedFloors = _context.OfficeBoyAssignedFloors
                        .Where(a => a.OfficeBoyAccountId == l.OfficeBoyAccountId && a.Status == "Active")
                        .Select(a => a.Floor.Number)
                        .ToList()
                })
                .ToListAsync();

            return Ok(pending);
        }

        // GET api/leaverequest/officeboy/{id}
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
                    substituteOfficeBoyImageUrl = l.SubstituteOfficeBoyAccount != null
                        ? l.SubstituteOfficeBoyAccount.ProfileImageUrl
                        : null,
                    requestedAt = l.RequestedAt,
                    decidedAt = l.DecidedAt
                })
                .ToListAsync();

            return Ok(leaves);
        }

        // GET api/leaverequest/balance/{officeBoyId}
        [HttpGet("balance/{officeBoyId}")]
        public async Task<IActionResult> GetBalance(int officeBoyId)
        {
            var officeBoyExists = await _context.Accounts
                .AnyAsync(a => a.Id == officeBoyId && a.Role == 1);

            if (!officeBoyExists)
                return NotFound(new { message = "OfficeBoy not found" });

            var policy = await GetActivePolicyAsync();

            var approvedLeaves = await _context.LeaveRequests
                .Where(l => l.OfficeBoyAccountId == officeBoyId && l.Status == Approved)
                .ToListAsync();

            var pendingLeaves = await _context.LeaveRequests
                .Where(l => l.OfficeBoyAccountId == officeBoyId && l.Status == Pending)
                .ToListAsync();

            var today = DateTime.Today;

            var yearlyUsed = approvedLeaves.Sum(l =>
                CountDaysInPeriod(l.StartDate.Date, l.EndDate.Date, today.Year, null));

            var monthlyUsed = approvedLeaves.Sum(l =>
                CountDaysInPeriod(l.StartDate.Date, l.EndDate.Date, today.Year, today.Month));

            var pendingDays = pendingLeaves.Sum(l =>
                CountDaysInPeriod(l.StartDate.Date, l.EndDate.Date, today.Year, null));

            return Ok(new
            {
                yearlyLimit = policy.YearlyLimit,
                monthlyLimit = policy.MonthlyLimit,
                yearlyUsed,
                monthlyUsed,
                pendingDays,
                yearlyRemaining = Math.Max(0, policy.YearlyLimit - yearlyUsed),
                monthlyRemaining = Math.Max(0, policy.MonthlyLimit - monthlyUsed)
            });
        }

        // GET api/leaverequest/free-officeboys?officeBoyId=
        // PURPOSE: Ranked list of possible substitutes: unassigned first, then no duty,
        // then lowest active task load. Office boys on leave are excluded.
        [HttpGet("free-officeboys")]
        [HttpGet("substitute-candidates")]
        public async Task<IActionResult> GetFreeOfficeBoys(int? officeBoyId = null)
        {
            var today = DateTime.Today;

            var onLeaveIds = await _context.LeaveRequests
                .Where(l => l.Status == Approved
                    && l.StartDate.Date <= today
                    && l.EndDate.Date >= today)
                .Select(l => l.OfficeBoyAccountId)
                .Distinct()
                .ToListAsync();

            var candidates = await _context.Accounts
                .Where(a => a.Role == 1
                    && (!officeBoyId.HasValue || a.Id != officeBoyId.Value)
                    && !onLeaveIds.Contains(a.Id))
                .Select(a => new { id = a.Id, name = a.Name, profileImageUrl = a.ProfileImageUrl })
                .ToListAsync();

            var assignments = await _context.OfficeBoyAssignedFloors
                .Where(a => a.Status == "Active")
                .Select(a => new
                {
                    a.OfficeBoyAccountId,
                    a.FloorId,
                    FloorNumber = a.Floor != null ? a.Floor.Number : null
                })
                .ToListAsync();

            var load = await _context.Tasks
                .Where(t => t.Status == "Pending" || t.Status == "In Progress")
                .GroupBy(t => t.OfficeBoyAccountId)
                .Select(g => new { OfficeBoyAccountId = g.Key, Count = g.Count() })
                .ToListAsync();

            var result = candidates
                .Select(c =>
                {
                    var floors = assignments
                        .Where(a => a.OfficeBoyAccountId == c.id)
                        .Select(a => a.FloorNumber)
                        .Where(f => f != null)
                        .Distinct()
                        .ToList();

                    var activeTaskCount = load
                        .FirstOrDefault(l => l.OfficeBoyAccountId == c.id)?.Count ?? 0;

                    return new
                    {
                        id = c.id,
                        name = c.name,
                        profileImageUrl = c.profileImageUrl,
                        isAssigned = floors.Count > 0,
                        assignedFloors = floors,
                        activeTaskCount
                    };
                })
                .OrderBy(c => c.isAssigned)
                .ThenBy(c => c.activeTaskCount)
                .ThenBy(c => c.name)
                .ToList();

            return Ok(result);
        }

        // PUT api/leaverequest/{id}/decide
        [HttpPut("{id}/decide")]
        public async Task<IActionResult> Decide(int id, [FromBody] DecideLeaveRequest request)
        {
            var supervisorExists = await _context.Accounts
                .AnyAsync(a => a.Id == request.SupervisorAccountId && a.Role == 3);

            if (!supervisorExists)
                return BadRequest(new { message = "Supervisor not found" });

            var leave = await _context.LeaveRequests
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leave == null)
                return NotFound(new { message = "Leave request not found" });

            if (leave.Status != Pending)
                return BadRequest(new { message = "This leave request has already been decided" });

            if (!request.Approve)
            {
                leave.Status = Rejected;
                leave.SupervisorAccountId = request.SupervisorAccountId;
                leave.SupervisorRemarks = request.Remarks;
                leave.DecidedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Leave rejected", leaveId = leave.Id, status = leave.Status });
            }

            if (!request.EndDate.HasValue)
                return BadRequest(new { message = "Approved end date is required" });

            var approvedStart = (request.StartDate ?? leave.StartDate).Date;
            var approvedEnd = request.EndDate.Value.Date;

            if (approvedEnd < approvedStart)
                return BadRequest(new { message = "Approved end date cannot be before the start date" });

            if (approvedStart < leave.StartDate.Date || approvedEnd > leave.EndDate.Date)
                return BadRequest(new { message = "Approved dates must stay within the requested leave period" });

            var existingLeaves = await _context.LeaveRequests
                .Where(l => l.OfficeBoyAccountId == leave.OfficeBoyAccountId
                    && l.Id != leave.Id
                    && (l.Status == Pending || l.Status == Approved))
                .ToListAsync();

            var policy = await GetActivePolicyAsync();
            var limitError = ValidateLimits(approvedStart, approvedEnd, existingLeaves, policy);

            if (limitError != null)
                return BadRequest(new { message = limitError });

            var originalAssignment = await _context.OfficeBoyAssignedFloors
                .FirstOrDefaultAsync(a =>
                    a.OfficeBoyAccountId == leave.OfficeBoyAccountId &&
                    a.Status == "Active");

            if (originalAssignment != null)
            {
                if (!request.SubstituteOfficeBoyAccountId.HasValue)
                    return BadRequest(new { message = "A substitute office boy is required for this assigned office boy" });

                if (request.SubstituteOfficeBoyAccountId.Value == leave.OfficeBoyAccountId)
                    return BadRequest(new { message = "The substitute cannot be the same office boy" });

                var substituteExists = await _context.Accounts
                    .AnyAsync(a => a.Id == request.SubstituteOfficeBoyAccountId.Value && a.Role == 1);

                if (!substituteExists)
                    return BadRequest(new { message = "Substitute office boy not found" });
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

                    // Move the office boy's open tasks to the substitute until he returns.
                    var openTasks = await _context.Tasks
                        .Where(t => t.OfficeBoyAccountId == leave.OfficeBoyAccountId
                            && (t.Status == "Pending" || t.Status == "In Progress"))
                        .ToListAsync();

                    foreach (var task in openTasks)
                    {
                        task.OriginalOfficeBoyAccountId = leave.OfficeBoyAccountId;
                        task.OfficeBoyAccountId = request.SubstituteOfficeBoyAccountId.Value;
                        task.UpdatedAt = DateTime.Now;
                    }

                    leave.SubstituteOfficeBoyAccountId = request.SubstituteOfficeBoyAccountId.Value;
                    leave.OriginalAssignmentId = originalAssignment.Id;
                    leave.SubstituteAssignmentId = substituteAssignment.Id;
                }

                leave.StartDate = approvedStart;
                leave.EndDate = approvedEnd;
                leave.Status = Approved;
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

            return Ok(new
            {
                message = "Leave approved",
                leaveId = leave.Id,
                status = leave.Status,
                approvedStartDate = leave.StartDate,
                approvedEndDate = leave.EndDate
            });
        }

        private async Task<LeavePolicy> GetActivePolicyAsync()
        {
            var policy = await _context.LeavePolicies
                .Where(p => p.Role == 1 && p.IsActive)
                .OrderByDescending(p => p.EffectiveFrom)
                .FirstOrDefaultAsync();

            return policy ?? new LeavePolicy
            {
                YearlyLimit = 25,
                MonthlyLimit = 4
            };
        }

        /// <summary>
        /// Returns an error message when the requested period would exceed the
        /// monthly or yearly allowance. Returns null when the request is valid.
        /// </summary>
        private static string? ValidateLimits(
            DateTime start,
            DateTime end,
            IEnumerable<LeaveRequest> existingLeaves,
            LeavePolicy policy)
        {
            var existing = existingLeaves.ToList();

            var months = new HashSet<(int Year, int Month)>();
            var years = new HashSet<int>();

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                months.Add((day.Year, day.Month));
                years.Add(day.Year);
            }

            foreach (var (year, month) in months)
            {
                var requested = CountDaysInPeriod(start, end, year, month);
                var used = existing.Sum(l =>
                    CountDaysInPeriod(l.StartDate.Date, l.EndDate.Date, year, month));

                if (used + requested > policy.MonthlyLimit)
                    return $"This request exceeds the monthly leave limit of {policy.MonthlyLimit} days";
            }

            foreach (var year in years)
            {
                var requested = CountDaysInPeriod(start, end, year, null);
                var used = existing.Sum(l =>
                    CountDaysInPeriod(l.StartDate.Date, l.EndDate.Date, year, null));

                if (used + requested > policy.YearlyLimit)
                    return $"This request exceeds the yearly leave limit of {policy.YearlyLimit} days";
            }

            return null;
        }

        private static int CountDaysInPeriod(DateTime start, DateTime end, int year, int? month)
        {
            var count = 0;

            for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            {
                if (day.Year == year && (!month.HasValue || day.Month == month.Value))
                    count++;
            }

            return count;
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
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Remarks { get; set; }
        public int? SubstituteOfficeBoyAccountId { get; set; }
    }

    public class UpdateLeavePolicyRequest
    {
        public int SupervisorAccountId { get; set; }
        public int YearlyLimit { get; set; }
        public int MonthlyLimit { get; set; }
    }
}
