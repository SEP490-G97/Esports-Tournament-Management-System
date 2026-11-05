using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TournamentID", "CreatedAt", Name = "IX_AuditLog_Timeline")]
public partial class AuditLog
{
    [Key]
    public long AuditLogID { get; set; }

    public int? ActorAccountID { get; set; }

    public int? TournamentID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string ActionType { get; set; } = null!;

    [StringLength(30)]
    [Unicode(false)]
    public string ObjectType { get; set; } = null!;

    public int? ObjectID { get; set; }

    public string? BeforeValue { get; set; }

    public string? AfterValue { get; set; }

    [StringLength(1000)]
    public string? Reason { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ActorAccountID")]
    [InverseProperty("AuditLog")]
    public virtual Account? ActorAccount { get; set; }

    [ForeignKey("TournamentID")]
    [InverseProperty("AuditLog")]
    public virtual Tournament? Tournament { get; set; }
}
