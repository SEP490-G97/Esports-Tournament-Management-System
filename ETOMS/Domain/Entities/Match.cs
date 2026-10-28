using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Match
{
    public int MatchId { get; set; }

    public int TournamentId { get; set; }

    public int BracketNodeId { get; set; }

    public int? TeamAid { get; set; }

    public int? TeamBid { get; set; }

    public int? RefereeAccountId { get; set; }

    public int BestOf { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public string? RoomCode { get; set; }

    public string? RoomPassword { get; set; }

    public DateTime? CheckInOpenAt { get; set; }

    public DateTime? TeamAcheckedInAt { get; set; }

    public DateTime? TeamBcheckedInAt { get; set; }

    public string CheckInMode { get; set; } = null!;

    public string? OverrideReason { get; set; }

    public int? ScoreA { get; set; }

    public int? ScoreB { get; set; }

    public int? WinnerTeamId { get; set; }

    public string Status { get; set; } = null!;

    public string? ResultSource { get; set; }

    public DateTime? ReportDeadlineAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual BracketNode BracketNode { get; set; } = null!;

    public virtual Dispute? Dispute { get; set; }

    public virtual ICollection<Livestream> Livestreams { get; set; } = new List<Livestream>();

    public virtual ICollection<MatchChatMessage> MatchChatMessages { get; set; } = new List<MatchChatMessage>();

    public virtual ICollection<MatchGame> MatchGames { get; set; } = new List<MatchGame>();

    public virtual ICollection<MatchReport> MatchReports { get; set; } = new List<MatchReport>();

    public virtual ICollection<MatchRequest> MatchRequests { get; set; } = new List<MatchRequest>();

    public virtual ICollection<Penalty> Penalties { get; set; } = new List<Penalty>();

    public virtual Account? RefereeAccount { get; set; }

    public virtual Team? TeamA { get; set; }

    public virtual Team? TeamB { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;

    public virtual Team? WinnerTeam { get; set; }
}
