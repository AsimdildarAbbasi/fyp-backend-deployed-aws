using Microsoft.EntityFrameworkCore;
using OBManagementAPI.Models;

namespace OBManagementAPI.Services;

public class NotificationService
{
    private readonly ObmanagementContext _context;
    private readonly IConfiguration _configuration;

    public NotificationService(ObmanagementContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Returns true when the given location sits outside the configured campus geofence.
    /// A location without coordinates is treated as inside (no false alerts).
    /// </summary>
    public async Task<bool> IsOutsideCampusAsync(int locationId, CancellationToken cancellationToken = default)
    {
        var location = await _context.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        if (location?.Latitude == null || location.Longitude == null)
            return false;

        var campusName = _configuration["CampusGeofence:Name"] ?? "BIIT";

        var campus = await _context.Geofences
            .AsNoTracking()
            .Where(g => g.IsActive && g.Name == campusName)
            .FirstOrDefaultAsync(cancellationToken);

        if (campus == null)
            return false;

        var distance = HaversineDistanceMeters(
            location.Latitude.Value,
            location.Longitude.Value,
            campus.CenterLatitude,
            campus.CenterLongitude);

        return distance > campus.RadiusMeters;
    }

    /// <summary>
    /// Creates one notification per faculty member on the office boy's assigned floors.
    /// Duplicate notifications for the same task and recipient are skipped.
    /// </summary>
    public async System.Threading.Tasks.Task NotifyFloorFacultyOfOutsideTaskAsync(
        OBManagementAPI.Models.Task task,
        string taskDescription,
        CancellationToken cancellationToken = default)
    {
        if (!task.IsOutsideCampus)
            return;

        var floorIds = await _context.OfficeBoyAssignedFloors
            .Where(a => a.OfficeBoyAccountId == task.OfficeBoyAccountId && a.Status == "Active")
            .Select(a => a.FloorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (floorIds.Count == 0)
            return;

        var facultyIds = await _context.FacultyMemberOffices
            .Where(f => f.Office != null && floorIds.Contains(f.Office.BuildingFloorId))
            .Select(f => f.FacultyAccountId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (facultyIds.Count == 0)
            return;

        var alreadyNotified = await _context.Notifications
            .Where(n => n.TaskId == task.Id)
            .Select(n => n.RecipientAccountId)
            .ToListAsync(cancellationToken);

        var officeBoyName = await _context.Accounts
            .Where(a => a.Id == task.OfficeBoyAccountId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Office boy";

        var locationName = await _context.Locations
            .Where(l => l.Id == task.LocationId)
            .Select(l => l.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "an outside location";

        var description = string.IsNullOrWhiteSpace(taskDescription)
            ? "a new task"
            : taskDescription.Trim();

        foreach (var facultyId in facultyIds.Except(alreadyNotified))
        {
            _context.Notifications.Add(new Notification
            {
                RecipientAccountId = facultyId,
                Type = "OutsideCampusTask",
                Title = "Office boy is going outside",
                Message = $"{officeBoyName} is heading to {locationName} for: {description}",
                TaskId = task.Id,
                OfficeBoyAccountId = task.OfficeBoyAccountId,
                FloorId = floorIds.FirstOrDefault(),
                LocationId = task.LocationId,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
