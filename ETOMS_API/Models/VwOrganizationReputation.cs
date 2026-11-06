using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class VwOrganizationReputation
{
    public int OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public long PublishedCount { get; set; }

    public decimal CompletedCount { get; set; }

    public decimal? CompletedPercent { get; set; }

    public long MatchCount { get; set; }

    public long DisputedMatchCount { get; set; }

    public decimal? DisputedPercent { get; set; }

    public decimal? AvgResolveHours { get; set; }

    public long RefundCount { get; set; }
}
