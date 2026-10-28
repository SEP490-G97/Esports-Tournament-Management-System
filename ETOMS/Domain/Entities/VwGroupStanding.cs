using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class VwGroupStanding
{
    public int TournamentId { get; set; }

    public string? GroupLabel { get; set; }

    public int? TeamId { get; set; }

    public int? Played { get; set; }

    public int? Won { get; set; }

    public int? Lost { get; set; }

    public int? ScoreDiff { get; set; }

    public int? Point { get; set; }
}
