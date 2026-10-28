using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class TeamMember
{
    public int TeamMemberId { get; set; }

    public int TeamId { get; set; }

    public int GameId { get; set; }

    public int AccountId { get; set; }

    public int? RecruitmentPostId { get; set; }

    public string Slot { get; set; } = null!;

    public string Direction { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Message { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Game Game { get; set; } = null!;

    public virtual RecruitmentPost? RecruitmentPost { get; set; }

    public virtual Team Team { get; set; } = null!;
}
