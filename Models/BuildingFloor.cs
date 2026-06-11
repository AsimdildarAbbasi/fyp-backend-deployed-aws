using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class BuildingFloor
{
    public int Id { get; set; }

    public string? Number { get; set; }

    public virtual ICollection<OfficeBoyAssignedFloor> OfficeBoyAssignedFloors { get; set; } = new List<OfficeBoyAssignedFloor>();

    public virtual ICollection<Office> Offices { get; set; } = new List<Office>();
}
