using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OBManagementAPI.Models;

public partial class ArrivalDepartureTask
{
    [Key]
    public int Id { get; set; }

    public int FacultyAccountId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TaskType { get; set; } = null!; // "Arrival" or "Departure"

    [Required]
    [MaxLength(255)]
    public string Description { get; set; } = null!;

    [Column(TypeName = "decimal(9, 6)")]
    public decimal Latitude { get; set; }

    [Column(TypeName = "decimal(9, 6)")]
    public decimal Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey("FacultyAccountId")]
    public virtual Account FacultyAccount { get; set; } = null!;
}
