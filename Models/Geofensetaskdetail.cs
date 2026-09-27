using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OBManagementAPI.Models
{
    [Table("GeofenceTaskDetail")]
    public class GeofenceTaskDetail
    {
        [Key]
        public int TaskId { get; set; }   // same value as Task.Id - this is a 1-to-1 extension table, not its own identity

        public int FacultyAccountId { get; set; }

        public int GeofenceId { get; set; }

        [Required, MaxLength(10)]
        public string TriggerType { get; set; } = null!;   // "Enter" or "Exit"

        public int? TaskCategoryId { get; set; }

        public bool IsVisibleToOfficeBoy { get; set; } = false;

        public DateTime? TriggeredAt { get; set; }

        // Navigation properties
        [ForeignKey("TaskId")]
        public Task Task { get; set; } = null!;

        [ForeignKey("FacultyAccountId")]
        public Account Faculty { get; set; } = null!;

        [ForeignKey("GeofenceId")]
        public Geofence Geofence { get; set; } = null!;

        [ForeignKey("TaskCategoryId")]
        public TaskCategory? TaskCategory { get; set; }
    }
}