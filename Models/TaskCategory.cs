using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class TaskCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
