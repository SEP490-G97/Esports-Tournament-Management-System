using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("ScheduledAt", Name = "IX_Match_Schedule")]
[Index("TournamentID", "Status", Name = "IX_Match_Tour")]
public partial class Match
{
    [Key]
    public int MatchID { get; set; }

    public int TournamentID { get; set; }

    public int BracketNodeID { get; set; }

    public int? TeamAID { get; set; }

    public int? TeamBID { get; set; }

    public int? RefereeAccountID { get; set; }

    public int BestOf { get; set; }

    [Precision(0)]
    public DateTime? ScheduledAt { get; set; }

    [StringLength(100)]
    public string? RoomCode { get; set; }

    [StringLength(100)]
    public string? RoomPassword { get; set; }

    [Precision(0)]
    public DateTime? CheckInOpenAt { get; set; }

    [Precision(0)]
    public DateTime? TeamACheckedInAt { get; set; }

    [Precision(0)]
    public DateTime? TeamBCheckedInAt { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string CheckInMode { get; set; } = null!;

    [StringLength(500)]
    public string? OverrideReason { get; set; }

    public int? ScoreA { get; set; }

    public int? ScoreB { get; set; }

    public int? WinnerTeamID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string? ResultSource { get; set; }

    [Precision(0)]
    public DateTime? ReportDeadlineAt { get; set; }

    [Precision(0)]
    public DateTime? VerifiedAt { get; set; }

    [ForeignKey("BracketNodeID")]
    [InverseProperty("Match")]
    public virtual BracketNode BracketNode { get; set; } = null!;

    [InverseProperty("Match")]
    public virtual Dispute? Dispute { get; set; }

    [InverseProperty("Match")]
    public virtual ICollection<Livestream> Livestream { get; set; } = new List<Livestream>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchChatMessage> MatchChatMessage { get; set; } = new List<MatchChatMessage>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchGame> MatchGame { get; set; } = new List<MatchGame>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchReport> MatchReport { get; set; } = new List<MatchReport>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchRequest> MatchRequest { get; set; } = new List<MatchRequest>();

    [InverseProperty("Match")]
    public virtual ICollection<Penalty> Penalty { get; set; } = new List<Penalty>();

    [ForeignKey("RefereeAccountID")]
    [InverseProperty("Match")]
    public virtual Account? RefereeAccount { get; set; }

    [ForeignKey("TeamAID")]
    [InverseProperty("MatchTeamA")]
    public virtual Team? TeamA { get; set; }

    [ForeignKey("TeamBID")]
    [InverseProperty("MatchTeamB")]
    public virtual Team? TeamB { get; set; }

    [ForeignKey("TournamentID")]
    [InverseProperty("Match")]
    public virtual Tournament Tournament { get; set; } = null!;

    [ForeignKey("WinnerTeamID")]
    [InverseProperty("MatchWinnerTeam")]
    public virtual Team? WinnerTeam { get; set; }
}
