using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TournamentID", "Status", Name = "IX_ContentItem_Tour")]
public partial class ContentItem
{
    [Key]
    public int ContentItemID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Kind { get; set; } = null!;

    public int? TournamentID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string? NotificationType { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public bool IsPinned { get; set; }

    public int AuthorAccountID { get; set; }

    [Precision(0)]
    public DateTime? PublishedAt { get; set; }

    [Precision(0)]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("AuthorAccountID")]
    [InverseProperty("ContentItem")]
    public virtual Account AuthorAccount { get; set; } = null!;

    [ForeignKey("TournamentID")]
    [InverseProperty("ContentItem")]
    public virtual Tournament? Tournament { get; set; }
}
