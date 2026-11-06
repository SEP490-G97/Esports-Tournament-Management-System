using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class PromotionLink
{
    public int PromotionLinkId { get; set; }

    public int TournamentId { get; set; }

    public string Code { get; set; } = null!;

    public string Label { get; set; } = null!;

    public int VisitCount { get; set; }

    public int CreatedByAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account CreatedByAccount { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
