using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class Task
{
    public int Id { get; set; }

    public int FacultyAccountId { get; set; }

    public int OfficeBoyAccountId { get; set; }

    public int LocationId { get; set; }

    public string? Description { get; set; }

    public int? Rating { get; set; }

    public string? Remarks { get; set; }

    public DateTime? TaskTime { get; set; }


    public string Status { get; set; } = null!;


    public virtual Account FacultyAccount { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;

    public virtual Account OfficeBoyAccount { get; set; } = null!;

    public DateTime? ScheduledAt { get; set; } // NEW

    public bool IsScheduled { get; set; } = false; // NEW

    public int? CurrentLocationId { get; set; }

    public DateTime? LocationUpdatedAt { get; set; }

    public virtual Location? CurrentLocation { get; set; }
}
