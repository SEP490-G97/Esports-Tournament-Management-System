using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class TeamMember
{
    [Key]
    public int TeamMemberID { get; set; }

    public int TeamID { get; set; }

    public int GameID { get; set; }

    public int AccountID { get; set; }

    public int? RecruitmentPostID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Slot { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Direction { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [StringLength(500)]
    public string? Message { get; set; }

    [Precision(0)]
    public DateTime? JoinedAt { get; set; }

    [Precision(0)]
    public DateTime? LeftAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("TeamMember")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("GameID")]
    [InverseProperty("TeamMember")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("RecruitmentPostID")]
    [InverseProperty("TeamMember")]
    public virtual RecruitmentPost? RecruitmentPost { get; set; }

    [ForeignKey("TeamID")]
    [InverseProperty("TeamMember")]
    public virtual Team Team { get; set; } = null!;
}
