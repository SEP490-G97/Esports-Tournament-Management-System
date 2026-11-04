using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class OrganizationDocument
{
    [Key]
    public int OrganizationDocumentID { get; set; }

    public int OrganizationID { get; set; }

    [StringLength(100)]
    public string DocumentType { get; set; } = null!;

    [StringLength(500)]
    public string FileUrl { get; set; } = null!;

    [StringLength(255)]
    public string OriginalFileName { get; set; } = null!;

    [Precision(0)]
    public DateTime UploadedAt { get; set; }

    [ForeignKey("OrganizationID")]
    [InverseProperty("OrganizationDocument")]
    public virtual Organization Organization { get; set; } = null!;
}
