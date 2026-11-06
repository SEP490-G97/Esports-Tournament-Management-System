using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class OrganizationMember
{
    public int OrganizationMemberId { get; set; }

    public int OrganizationId { get; set; }

    public int AccountId { get; set; }

    public string Role { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime InvitedAt { get; set; }

    public DateTime? JoinedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;
}
