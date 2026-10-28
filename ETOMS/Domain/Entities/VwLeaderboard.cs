using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class VwLeaderboard
{
    public int GameId { get; set; }

    public int TeamId { get; set; }

    public int? Season { get; set; }

    public int? TournamentPlayed { get; set; }

    public int? TitleCount { get; set; }

    public int? AchievementPoint { get; set; }
}
