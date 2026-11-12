using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class VwTeamPenaltyPoint
{
    public int TournamentId { get; set; }

    public int TeamId { get; set; }

    public decimal? ActivePoints { get; set; }

    public int? PenaltyThreshold { get; set; }
}
