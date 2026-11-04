using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Index("AccountID", "TournamentID", Name = "UQ_TournamentFollow", IsUnique = true)]
public partial class TournamentFollow
{
    [Key]
    public int TournamentFollowID { get; set; }

    public int AccountID { get; set; }

    public int TournamentID { get; set; }

    [Precision(0)]
    public DateTime FollowedAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("TournamentFollow")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("TournamentFollow")]
    public virtual Tournament Tournament { get; set; } = null!;
}
