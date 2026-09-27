using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Services
{
    public class LeaveReversionService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LeaveReversionService> _logger;

        public LeaveReversionService(
            IServiceScopeFactory scopeFactory,
            ILogger<LeaveReversionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredLeaveRequests(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to process expired leave requests");
                }

                try
                {
                    await System.Threading.Tasks.Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async System.Threading.Tasks.Task ProcessExpiredLeaveRequests(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ObmanagementContext>();

            var expiredLeaveIds = await context.LeaveRequests
                .AsNoTracking()
                .Where(leave => leave.Status == "Approved"
                    && leave.EndDate < DateTime.Now
                    && leave.ReturnedAt == null
                    && leave.SubstituteAssignmentId != null)
                .Select(leave => leave.Id)
                .ToListAsync(stoppingToken);

            foreach (var leaveId in expiredLeaveIds)
            {
                try
                {
                    var leave = await context.LeaveRequests.FirstOrDefaultAsync(request =>
                        request.Id == leaveId
                        && request.Status == "Approved"
                        && request.EndDate < DateTime.Now
                        && request.ReturnedAt == null
                        && request.SubstituteAssignmentId != null,
                        stoppingToken);

                    if (leave == null)
                        continue;

                    if (!leave.OriginalAssignmentId.HasValue)
                    {
                        _logger.LogWarning("Expired leave request {LeaveId} has no original assignment", leave.Id);
                        continue;
                    }

                    var originalAssignment = await context.OfficeBoyAssignedFloors
                        .FirstOrDefaultAsync(assignment => assignment.Id == leave.OriginalAssignmentId.Value, stoppingToken);
                    var substituteAssignment = await context.OfficeBoyAssignedFloors
                        .FirstOrDefaultAsync(assignment => assignment.Id == leave.SubstituteAssignmentId!.Value, stoppingToken);

                    if (originalAssignment == null)
                    {
                        _logger.LogWarning("Expired leave request {LeaveId} has no original assignment row", leave.Id);
                        continue;
                    }

                    if (substituteAssignment != null)
                        context.OfficeBoyAssignedFloors.Remove(substituteAssignment);
                    originalAssignment.Status = "Active";
                    leave.ReturnedAt = DateTime.Now;

                    await context.SaveChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    context.ChangeTracker.Clear();
                    _logger.LogError(exception, "Failed to revert assignments for leave request {LeaveId}", leaveId);
                }
            }
        }
    }
}