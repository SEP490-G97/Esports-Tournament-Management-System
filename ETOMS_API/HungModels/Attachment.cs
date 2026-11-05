using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("OwnerType", "OwnerID", Name = "IX_Attachment_Owner")]
public partial class Attachment
{
    [Key]
    public int AttachmentID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string OwnerType { get; set; } = null!;

    public int OwnerID { get; set; }

    [StringLength(500)]
    public string FileUrl { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string FileType { get; set; } = null!;

    public long? SizeBytes { get; set; }

    public int UploadedByAccountID { get; set; }

    [Precision(0)]
    public DateTime UploadedAt { get; set; }

    [ForeignKey("UploadedByAccountID")]
    [InverseProperty("Attachment")]
    public virtual Account UploadedByAccount { get; set; } = null!;
}
