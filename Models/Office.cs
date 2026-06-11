using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class Office
{
    public int Id { get; set; }

    public string OfficeName { get; set; } = null!;

    public int BuildingFloorId { get; set; }

    public virtual BuildingFloor BuildingFloor { get; set; } = null!;

    public virtual ICollection<FacultyMemberOffice> FacultyMemberOffices { get; set; } = new List<FacultyMemberOffice>();

    public virtual ICollection<OfficeBoyAssignedFloor> OfficeBoyAssignedFloors { get; set; } = new List<OfficeBoyAssignedFloor>();
}
