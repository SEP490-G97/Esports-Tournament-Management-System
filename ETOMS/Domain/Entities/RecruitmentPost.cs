using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class RecruitmentPost
{
    public int RecruitmentPostId { get; set; }

    public int GameId { get; set; }

    public int? TeamId { get; set; }

    public int AuthorAccountId { get; set; }

    public string PostType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public string? HighlightUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Account AuthorAccount { get; set; } = null!;

    public virtual Game Game { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
