using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class MatchSubstitution
{
    public int MatchSubstitutionId { get; set; }

    public int MatchId { get; set; }

    public int TeamId { get; set; }

    /// <summary>
    /// Tuyển thủ chính bị thay ra
    /// </summary>
    public int OutAccountId { get; set; }

    /// <summary>
    /// Dự bị vào sân
    /// </summary>
    public int InAccountId { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public int RequestedByAccountId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? DecidedByAccountId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }
}
