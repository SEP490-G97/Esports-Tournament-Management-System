using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TournamentID", "TeamID", "Status", Name = "IX_Penalty_TeamTour")]
public partial class Penalty
{
    [Key]
    public int PenaltyID { get; set; }

    public int TournamentID { get; set; }

    public int TeamID { get; set; }

    public int? MatchID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Severity { get; set; } = null!;

    public int Points { get; set; }

    [StringLength(1000)]
    public string Reason { get; set; } = null!;

    public int IssuedByAccountID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int? CancelsPenaltyID { get; set; }

    [StringLength(500)]
    public string? CancelReason { get; set; }

    [Precision(0)]
    public DateTime IssuedAt { get; set; }

    [ForeignKey("CancelsPenaltyID")]
    [InverseProperty("InverseCancelsPenalty")]
    public virtual Penalty? CancelsPenalty { get; set; }

    [InverseProperty("CancelsPenalty")]
    public virtual Penalty? InverseCancelsPenalty { get; set; }

    [ForeignKey("IssuedByAccountID")]
    [InverseProperty("Penalty")]
    public virtual Account IssuedByAccount { get; set; } = null!;

    [ForeignKey("MatchID")]
    [InverseProperty("Penalty")]
    public virtual Match? Match { get; set; }

    [ForeignKey("TeamID")]
    [InverseProperty("Penalty")]
    public virtual Team Team { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("Penalty")]
    public virtual Tournament Tournament { get; set; } = null!;
}
