using System;

namespace OBManagementAPI.Models;

public partial class TaskAttachment
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    public string FileName { get; set; } = null!;

    public string StoredPath { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public virtual Task Task { get; set; } = null!;
}
