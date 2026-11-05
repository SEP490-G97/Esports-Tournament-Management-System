using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TournamentID", "Status", Name = "IX_Livestream_Tour")]
public partial class Livestream
{
    [Key]
    public int LivestreamID { get; set; }

    public int TournamentID { get; set; }

    public int? MatchID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Platform { get; set; } = null!;

    [StringLength(500)]
    public string StreamUrl { get; set; } = null!;

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int CreatedByAccountID { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CreatedByAccountID")]
    [InverseProperty("Livestream")]
    public virtual Account CreatedByAccount { get; set; } = null!;

    [ForeignKey("MatchID")]
    [InverseProperty("Livestream")]
    public virtual Match? Match { get; set; }

    [ForeignKey("TournamentID")]
    [InverseProperty("Livestream")]
    public virtual Tournament Tournament { get; set; } = null!;
}
