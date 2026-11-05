using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("ShortCode", Name = "UQ_Game_Code", IsUnique = true)]
[Index("Title", Name = "UQ_Game_Title", IsUnique = true)]
public partial class Game
{
    [Key]
    public int GameID { get; set; }

    [StringLength(150)]
    public string Title { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string ShortCode { get; set; } = null!;

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    public string? BannerUrl { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int MinStarters { get; set; }

    public int MaxStarters { get; set; }

    public int MaxSubstitutes { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string DefaultFormat { get; set; } = null!;

    [StringLength(200)]
    public string AllowedEvidenceTypes { get; set; } = null!;

    public int MaxEvidenceFileSizeMB { get; set; }

    [InverseProperty("Game")]
    public virtual ICollection<PlayerCredential> PlayerCredential { get; set; } = new List<PlayerCredential>();

    [InverseProperty("Game")]
    public virtual ICollection<RecruitmentPost> RecruitmentPost { get; set; } = new List<RecruitmentPost>();

    [InverseProperty("Game")]
    public virtual ICollection<Team> Team { get; set; } = new List<Team>();

    [InverseProperty("Game")]
    public virtual ICollection<TeamMember> TeamMember { get; set; } = new List<TeamMember>();

    [InverseProperty("Game")]
    public virtual ICollection<Tournament> Tournament { get; set; } = new List<Tournament>();
}
