using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Livestream
{
    public int LivestreamId { get; set; }

    public int TournamentId { get; set; }

    public int? MatchId { get; set; }

    public string Platform { get; set; } = null!;

    public string StreamUrl { get; set; } = null!;

    public string? Title { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedByAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account CreatedByAccount { get; set; } = null!;

    public virtual Match? Match { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;
}
