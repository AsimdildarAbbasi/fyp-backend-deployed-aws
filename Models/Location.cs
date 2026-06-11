using System;
using System.Collections.Generic;

namespace OBManagementAPI.Models;

public partial class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }


    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
