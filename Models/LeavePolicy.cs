using System;

namespace OBManagementAPI.Models;

public partial class LeavePolicy
{
    public int Id { get; set; }

    public int Role { get; set; }

    public int YearlyLimit { get; set; }

    public int MonthlyLimit { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public bool IsActive { get; set; } = true;
}
