using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

public partial class Organization
{
    [Key]
    public int OrganizationID { get; set; }

    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string OrganizationType { get; set; } = null!;

    [StringLength(150)]
    public string? ParentOrganization { get; set; }

    [StringLength(256)]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [StringLength(1000)]
    public string? ReviewReason { get; set; }

    public int? ReviewedByAccountID { get; set; }

    [Precision(0)]
    public DateTime? ReviewedAt { get; set; }

    [StringLength(1000)]
    public string? SuspensionReason { get; set; }

    [Precision(0)]
    public DateTime? SuspendedAt { get; set; }

    public int CreatedByAccountID { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CreatedByAccountID")]
    [InverseProperty("OrganizationCreatedByAccount")]
    public virtual Account CreatedByAccount { get; set; } = null!;

    [InverseProperty("Organization")]
    public virtual ICollection<OrganizationDocument> OrganizationDocument { get; set; } = new List<OrganizationDocument>();

    [InverseProperty("Organization")]
    public virtual ICollection<OrganizationMember> OrganizationMember { get; set; } = new List<OrganizationMember>();

    [ForeignKey("ReviewedByAccountID")]
    [InverseProperty("OrganizationReviewedByAccount")]
    public virtual Account? ReviewedByAccount { get; set; }

    [InverseProperty("Organization")]
    public virtual ICollection<Tournament> Tournament { get; set; } = new List<Tournament>();
}
