using System;

namespace OBManagementAPI.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public int OfficeBoyAccountId { get; set; }
        public Account? OfficeBoyAccount { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = null!;

        public string Status { get; set; } = "Pending";

        public int? SupervisorAccountId { get; set; }
        public Account? SupervisorAccount { get; set; }

        public string? SupervisorRemarks { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.Now;
        public DateTime? DecidedAt { get; set; }
    }
}