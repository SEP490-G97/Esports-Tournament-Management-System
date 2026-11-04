using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class RecruitmentPost
{
    [Key]
    public int RecruitmentPostID { get; set; }

    public int GameID { get; set; }

    public int? TeamID { get; set; }

    public int AuthorAccountID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string PostType { get; set; } = null!;

    [StringLength(200)]
    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    [StringLength(500)]
    public string? HighlightUrl { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("AuthorAccountID")]
    [InverseProperty("RecruitmentPost")]
    public virtual Account AuthorAccount { get; set; } = null!;

    [ForeignKey("GameID")]
    [InverseProperty("RecruitmentPost")]
    public virtual Game Game { get; set; } = null!;

    [ForeignKey("TeamID")]
    [InverseProperty("RecruitmentPost")]
    public virtual Team? Team { get; set; }

    [InverseProperty("RecruitmentPost")]
    public virtual ICollection<TeamMember> TeamMember { get; set; } = new List<TeamMember>();
}
