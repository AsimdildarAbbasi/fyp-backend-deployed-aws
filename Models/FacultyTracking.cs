using System;

namespace OBManagementAPI.Models;

public partial class FacultyTracking
{
    public long Id { get; set; }

    public int FacultyAccountId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime RecordedAt { get; set; }

    public bool IsInsideGeofence { get; set; }

    public int? GeofenceId { get; set; }

    public virtual Account FacultyAccount { get; set; } = null!;

    public virtual Geofence? Geofence { get; set; }
}
