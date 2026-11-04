using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class Registration
{
    [Key]
    public int RegistrationID { get; set; }

    public int TournamentID { get; set; }

    public int TeamID { get; set; }

    public int SubmittedByAccountID { get; set; }

    public int? RulebookVersionAccepted { get; set; }

    [Precision(0)]
    public DateTime? ConsentAt { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int? WaitlistPosition { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string PaymentStatus { get; set; } = null!;

    [StringLength(500)]
    public string? ReviewNote { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string FinalPlacement { get; set; } = null!;

    public int? FinalRank { get; set; }

    [Precision(0)]
    public DateTime? SubmittedAt { get; set; }

    [Precision(0)]
    public DateTime? ReviewedAt { get; set; }

    [InverseProperty("Registration")]
    public virtual PaymentRecord? PaymentRecord { get; set; }

    [InverseProperty("Registration")]
    public virtual ICollection<RegistrationLineup> RegistrationLineup { get; set; } = new List<RegistrationLineup>();

    [ForeignKey("SubmittedByAccountID")]
    [InverseProperty("Registration")]
    public virtual Account SubmittedByAccount { get; set; } = null!;

    [ForeignKey("TeamID")]
    [InverseProperty("Registration")]
    public virtual Team Team { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("Registration")]
    public virtual Tournament Tournament { get; set; } = null!;
}
