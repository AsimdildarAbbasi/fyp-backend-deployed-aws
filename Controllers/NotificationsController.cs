using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ObmanagementContext _context;

        public NotificationsController(ObmanagementContext context)
        {
            _context = context;
        }

        // GET api/notifications/{accountId}?unreadOnly=true
        [HttpGet("{accountId}")]
        public async Task<IActionResult> GetNotifications(int accountId, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.RecipientAccountId == accountId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.Id)
                .Select(n => new
                {
                    id = n.Id,
                    type = n.Type,
                    title = n.Title,
                    message = n.Message,
                    taskId = n.TaskId,
                    taskDescription = n.Task != null ? n.Task.Description : null,
                    taskStatus = n.Task != null ? n.Task.Status : null,
                    officeBoyId = n.OfficeBoyAccountId,
                    officeBoyName = n.OfficeBoyAccount != null ? n.OfficeBoyAccount.Name : null,
                    officeBoyImageUrl = n.OfficeBoyAccount != null ? n.OfficeBoyAccount.ProfileImageUrl : null,
                    floorId = n.FloorId,
                    floorNumber = n.Floor != null ? n.Floor.Number : null,
                    locationId = n.LocationId,
                    locationName = n.Location != null ? n.Location.Name : null,
                    latitude = n.Location != null ? n.Location.Latitude : null,
                    longitude = n.Location != null ? n.Location.Longitude : null,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(notifications);
        }

        // GET api/notifications/{accountId}/unread-count
        [HttpGet("{accountId}/unread-count")]
        public async Task<IActionResult> GetUnreadCount(int accountId)
        {
            var count = await _context.Notifications
                .CountAsync(n => n.RecipientAccountId == accountId && !n.IsRead);

            return Ok(new { unreadCount = count });
        }

        // PUT api/notifications/{id}/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(int id, [FromBody] MarkNotificationReadRequest request)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientAccountId == request.AccountId);

            if (notification == null)
                return NotFound(new { message = "Notification not found" });

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Notification marked as read" });
        }

        // PUT api/notifications/read-all/{accountId}
        [HttpPut("read-all/{accountId}")]
        public async Task<IActionResult> MarkAllRead(int accountId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.RecipientAccountId == accountId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
                notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "All notifications marked as read", count = notifications.Count });
        }
    }

    public class MarkNotificationReadRequest
    {
        public int AccountId { get; set; }
    }
}
