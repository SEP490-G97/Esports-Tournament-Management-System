using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TournamentID", "Name", Name = "UQ_TournamentSponsor_Name", IsUnique = true)]
public partial class TournamentSponsor
{
    [Key]
    public int TournamentSponsorID { get; set; }

    public int TournamentID { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [StringLength(300)]
    public string? ContactInfo { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Tier { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool ShowWhenCancelled { get; set; }

    public int CreatedByAccountID { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CreatedByAccountID")]
    [InverseProperty("TournamentSponsor")]
    public virtual Account CreatedByAccount { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("TournamentSponsor")]
    public virtual Tournament Tournament { get; set; } = null!;
}
