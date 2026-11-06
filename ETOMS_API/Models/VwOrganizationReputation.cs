using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class VwOrganizationReputation
{
    public int OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public int? PublishedCount { get; set; }

    public int? CompletedCount { get; set; }

    public decimal? CompletedPercent { get; set; }

    public int? MatchCount { get; set; }

    public int? DisputedMatchCount { get; set; }

    public decimal? DisputedPercent { get; set; }

    public decimal? AvgResolveHours { get; set; }

    public int? RefundCount { get; set; }
}
