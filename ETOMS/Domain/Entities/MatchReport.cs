using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class MatchReport
{
    public int MatchReportId { get; set; }

    public int MatchId { get; set; }

    public int TeamId { get; set; }

    public int ReportedByAccountId { get; set; }

    public int ReportedScoreA { get; set; }

    public int ReportedScoreB { get; set; }

    public string? PerGameScores { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account ReportedByAccount { get; set; } = null!;

    public virtual Team Team { get; set; } = null!;
}
