using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Keyless]
public partial class vw_Leaderboard
{
    public int GameID { get; set; }

    public int TeamID { get; set; }

    public int? Season { get; set; }

    public int? TournamentPlayed { get; set; }

    public int? TitleCount { get; set; }

    public int? AchievementPoint { get; set; }
}
