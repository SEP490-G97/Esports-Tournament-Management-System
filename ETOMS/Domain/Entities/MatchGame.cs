using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class MatchGame
{
    public int MatchGameId { get; set; }

    public int MatchId { get; set; }

    public int GameNo { get; set; }

    public int? ScoreA { get; set; }

    public int? ScoreB { get; set; }

    public int? WinnerTeamId { get; set; }

    public int? UpdatedByAccountId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Account? UpdatedByAccount { get; set; }

    public virtual Team? WinnerTeam { get; set; }
}
