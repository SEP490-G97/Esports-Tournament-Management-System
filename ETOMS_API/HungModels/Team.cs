using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("GameID", "Name", Name = "UQ_Team_Name", IsUnique = true)]
[Index("GameID", "Tag", Name = "UQ_Team_Tag", IsUnique = true)]
public partial class Team
{
    [Key]
    public int TeamID { get; set; }

    public int GameID { get; set; }

    public int CaptainAccountID { get; set; }

    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(10)]
    public string Tag { get; set; } = null!;

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? InviteCode { get; set; }

    [Precision(0)]
    public DateTime? InviteExpiresAt { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("SeedTeam")]
    public virtual ICollection<BracketNode> BracketNode { get; set; } = new List<BracketNode>();

    [ForeignKey("CaptainAccountID")]
    [InverseProperty("Team")]
    public virtual Account CaptainAccount { get; set; } = null!;

    [ForeignKey("GameID")]
    [InverseProperty("Team")]
    public virtual Game Game { get; set; } = null!;

    [InverseProperty("WinnerTeam")]
    public virtual ICollection<MatchGame> MatchGame { get; set; } = new List<MatchGame>();

    [InverseProperty("Team")]
    public virtual ICollection<MatchReport> MatchReport { get; set; } = new List<MatchReport>();

    [InverseProperty("Team")]
    public virtual ICollection<MatchRequest> MatchRequest { get; set; } = new List<MatchRequest>();

    [InverseProperty("TeamA")]
    public virtual ICollection<Match> MatchTeamA { get; set; } = new List<Match>();

    [InverseProperty("TeamB")]
    public virtual ICollection<Match> MatchTeamB { get; set; } = new List<Match>();

    [InverseProperty("WinnerTeam")]
    public virtual ICollection<Match> MatchWinnerTeam { get; set; } = new List<Match>();

    [InverseProperty("Team")]
    public virtual ICollection<Penalty> Penalty { get; set; } = new List<Penalty>();

    [InverseProperty("Team")]
    public virtual ICollection<RecruitmentPost> RecruitmentPost { get; set; } = new List<RecruitmentPost>();

    [InverseProperty("Team")]
    public virtual ICollection<Registration> Registration { get; set; } = new List<Registration>();

    [InverseProperty("Team")]
    public virtual ICollection<TeamMember> TeamMember { get; set; } = new List<TeamMember>();
}
