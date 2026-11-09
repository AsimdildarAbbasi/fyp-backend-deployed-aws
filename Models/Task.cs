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

    public DateTime? ScheduledAt { get; set; }

    public bool IsScheduled { get; set; } = false;

    public int? CurrentLocationId { get; set; }

    public DateTime? LocationUpdatedAt { get; set; }

    public bool IsOutsideCampus { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedByAccountId { get; set; }

    // Set when a task is temporarily moved to a substitute office boy during leave.
    public int? OriginalOfficeBoyAccountId { get; set; }

    public virtual ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();

    public virtual Account FacultyAccount { get; set; } = null!;

    public virtual Location Location { get; set; } = null!;

    public virtual Account OfficeBoyAccount { get; set; } = null!;

    public virtual Location? CurrentLocation { get; set; }

    public virtual GeofenceTaskDetail? GeofenceTaskDetail { get; set; }
}