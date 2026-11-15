using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class VwGroupStanding
{
    public int TournamentId { get; set; }

    public string? GroupLabel { get; set; }

    public int? TeamId { get; set; }

    public long Played { get; set; }

    public decimal? Won { get; set; }

    public decimal? Lost { get; set; }

    public decimal? ScoreDiff { get; set; }

    public decimal? Point { get; set; }
}
