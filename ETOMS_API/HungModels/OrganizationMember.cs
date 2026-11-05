using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

public partial class OrganizationMember
{
    [Key]
    public int OrganizationMemberID { get; set; }

    public int OrganizationID { get; set; }

    public int AccountID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string Role { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime InvitedAt { get; set; }

    [Precision(0)]
    public DateTime? JoinedAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("OrganizationMember")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("OrganizationID")]
    [InverseProperty("OrganizationMember")]
    public virtual Organization Organization { get; set; } = null!;
}
