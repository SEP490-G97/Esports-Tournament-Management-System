using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Index("Code", Name = "UQ_PromotionLink_Code", IsUnique = true)]
public partial class PromotionLink
{
    [Key]
    public int PromotionLinkID { get; set; }

    public int TournamentID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Code { get; set; } = null!;

    [StringLength(150)]
    public string Label { get; set; } = null!;

    public int VisitCount { get; set; }

    public int CreatedByAccountID { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CreatedByAccountID")]
    [InverseProperty("PromotionLink")]
    public virtual Account CreatedByAccount { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("PromotionLink")]
    public virtual Tournament Tournament { get; set; } = null!;
}
