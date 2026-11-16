using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class VwLeaderboard
{
    public int GameId { get; set; }

    public int TeamId { get; set; }

    public short? Season { get; set; }

    public long TournamentPlayed { get; set; }

    public decimal? TitleCount { get; set; }

    public decimal? AchievementPoint { get; set; }
}
