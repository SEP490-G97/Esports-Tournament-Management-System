using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Team
{
    public int TeamId { get; set; }

    public int GameId { get; set; }

    public int CaptainAccountId { get; set; }

    public string Name { get; set; } = null!;

    public string Tag { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string? InviteCode { get; set; }

    public DateTime? InviteExpiresAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<BracketNode> BracketNodes { get; set; } = new List<BracketNode>();

    public virtual Account CaptainAccount { get; set; } = null!;

    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<MatchGame> MatchGames { get; set; } = new List<MatchGame>();

    public virtual ICollection<MatchReport> MatchReports { get; set; } = new List<MatchReport>();

    public virtual ICollection<MatchRequest> MatchRequests { get; set; } = new List<MatchRequest>();

    public virtual ICollection<Match> MatchTeamAs { get; set; } = new List<Match>();

    public virtual ICollection<Match> MatchTeamBs { get; set; } = new List<Match>();

    public virtual ICollection<Match> MatchWinnerTeams { get; set; } = new List<Match>();

    public virtual ICollection<Penalty> Penalties { get; set; } = new List<Penalty>();

    public virtual ICollection<RecruitmentPost> RecruitmentPosts { get; set; } = new List<RecruitmentPost>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
