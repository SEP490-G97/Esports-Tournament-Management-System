using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Attachment
{
    public int AttachmentId { get; set; }

    public string OwnerType { get; set; } = null!;

    public int OwnerId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public long? SizeBytes { get; set; }

    public int UploadedByAccountId { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Account UploadedByAccount { get; set; } = null!;
}
