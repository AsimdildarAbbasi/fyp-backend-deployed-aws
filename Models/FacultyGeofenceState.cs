using System;

namespace OBManagementAPI.Models;

public partial class FacultyGeofenceState
{
    public int Id { get; set; }

    public int FacultyAccountId { get; set; }

    public int GeofenceId { get; set; }

    public bool IsCurrentlyInside { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public virtual Account FacultyAccount { get; set; } = null!;

    public virtual Geofence Geofence { get; set; } = null!;
}
