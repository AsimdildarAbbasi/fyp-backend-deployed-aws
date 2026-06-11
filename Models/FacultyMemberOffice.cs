using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class FacultyMemberOffice
{
    public int Id { get; set; }

    public int FacultyAccountId { get; set; }

    public int OfficeId { get; set; }

    public string? Status { get; set; }

    public virtual Account FacultyAccount { get; set; } = null!;

    public virtual Office Office { get; set; } = null!;
}
