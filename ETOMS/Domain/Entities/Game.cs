using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Game
{
    public int GameId { get; set; }

    public string Title { get; set; } = null!;

    public string ShortCode { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string? BannerUrl { get; set; }

    public string Status { get; set; } = null!;

    public int MinStarters { get; set; }

    public int MaxStarters { get; set; }

    public int MaxSubstitutes { get; set; }

    public string DefaultFormat { get; set; } = null!;

    public string AllowedEvidenceTypes { get; set; } = null!;

    public int MaxEvidenceFileSizeMb { get; set; }

    public virtual ICollection<PlayerCredential> PlayerCredentials { get; set; } = new List<PlayerCredential>();

    public virtual ICollection<RecruitmentPost> RecruitmentPosts { get; set; } = new List<RecruitmentPost>();

    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
