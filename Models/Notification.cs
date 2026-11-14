using System;

namespace OBManagementAPI.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int RecipientAccountId { get; set; }

    public string Type { get; set; } = "OutsideCampusTask";

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public int? TaskId { get; set; }

    public int? OfficeBoyAccountId { get; set; }

    public int? FloorId { get; set; }

    public int? LocationId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual Account RecipientAccount { get; set; } = null!;

    public virtual Task? Task { get; set; }

    public virtual Account? OfficeBoyAccount { get; set; }

    public virtual BuildingFloor? Floor { get; set; }

    public virtual Location? Location { get; set; }
}
