using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class TournamentSponsor
{
    public int TournamentSponsorId { get; set; }

    public int TournamentId { get; set; }

    public string Name { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? ContactInfo { get; set; }

    public string Tier { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public int CreatedByAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account CreatedByAccount { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
