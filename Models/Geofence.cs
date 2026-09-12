using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class Geofence
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public double CenterLatitude { get; set; }

    public double CenterLongitude { get; set; }

    public double RadiusMeters { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    public virtual ICollection<FacultyGeofenceState> FacultyGeofenceStates { get; set; } = new List<FacultyGeofenceState>();

    public virtual ICollection<FacultyTracking> FacultyTrackings { get; set; } = new List<FacultyTracking>();
}
