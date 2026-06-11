using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class OfficeBoyAssignedFloor
{
    public int Id { get; set; }

    public int FloorId { get; set; }

    public int OfficeId { get; set; }

    public int OfficeBoyAccountId { get; set; }

    public string? Status { get; set; }

    public virtual BuildingFloor Floor { get; set; } = null!;

    public virtual Office Office { get; set; } = null!;

    public virtual Account OfficeBoyAccount { get; set; } = null!;
}
