using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class Organization
{
    public int OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public string OrganizationType { get; set; } = null!;

    public string? ParentOrganization { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? LogoUrl { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public string? ReviewReason { get; set; }

    public int? ReviewedByAccountId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public int CreatedByAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account CreatedByAccount { get; set; } = null!;

    public virtual ICollection<OrganizationDocument> OrganizationDocuments { get; set; } = new List<OrganizationDocument>();

    public virtual ICollection<OrganizationMember> OrganizationMembers { get; set; } = new List<OrganizationMember>();

    public virtual Account? ReviewedByAccount { get; set; }

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
