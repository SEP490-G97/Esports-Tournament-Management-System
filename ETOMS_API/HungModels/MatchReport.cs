using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("MatchID", "TeamID", Name = "UQ_MatchReport_TeamMatch", IsUnique = true)]
public partial class MatchReport
{
    [Key]
    public int MatchReportID { get; set; }

    public int MatchID { get; set; }

    public int TeamID { get; set; }

    public int ReportedByAccountID { get; set; }

    public int ReportedScoreA { get; set; }

    public int ReportedScoreB { get; set; }

    [StringLength(200)]
    public string? PerGameScores { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime SubmittedAt { get; set; }

    [ForeignKey("MatchID")]
    [InverseProperty("MatchReport")]
    public virtual Match Match { get; set; } = null!;

    [ForeignKey("ReportedByAccountID")]
    [InverseProperty("MatchReport")]
    public virtual Account ReportedByAccount { get; set; } = null!;

    [ForeignKey("TeamID")]
    [InverseProperty("MatchReport")]
    public virtual Team Team { get; set; } = null!;
}
