using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class FacultyTracking
{
    public int Id { get; set; }

    public int FacultyAccountId { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? Latitude { get; set; }

    public DateOnly? Date { get; set; }

    public TimeOnly? Time { get; set; }

    public virtual Account FacultyAccount { get; set; } = null!;
}
