using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Index("MatchID", "GameNo", Name = "UQ_MatchGame_Number", IsUnique = true)]
public partial class MatchGame
{
    [Key]
    public int MatchGameID { get; set; }

    public int MatchID { get; set; }

    public int GameNo { get; set; }

    public int? ScoreA { get; set; }

    public int? ScoreB { get; set; }

    public int? WinnerTeamID { get; set; }

    public int? UpdatedByAccountID { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("MatchID")]
    [InverseProperty("MatchGame")]
    public virtual Match Match { get; set; } = null!;

    [ForeignKey("UpdatedByAccountID")]
    [InverseProperty("MatchGame")]
    public virtual Account? UpdatedByAccount { get; set; }

    [ForeignKey("WinnerTeamID")]
    [InverseProperty("MatchGame")]
    public virtual Team? WinnerTeam { get; set; }
}
