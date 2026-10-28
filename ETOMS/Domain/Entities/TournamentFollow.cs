using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class TournamentFollow
{
    public int TournamentFollowId { get; set; }

    public int AccountId { get; set; }

    public int TournamentId { get; set; }

    public DateTime FollowedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
