using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class VwTeamPenaltyPoint
{
    public int TournamentId { get; set; }

    public int TeamId { get; set; }

    public int? ActivePoints { get; set; }

    public int? PenaltyThreshold { get; set; }
}
