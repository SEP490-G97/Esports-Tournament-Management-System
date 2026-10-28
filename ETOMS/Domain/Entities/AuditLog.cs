using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public int? ActorAccountId { get; set; }

    public int? TournamentId { get; set; }

    public string ActionType { get; set; } = null!;

    public string ObjectType { get; set; } = null!;

    public int? ObjectId { get; set; }

    public string? BeforeValue { get; set; }

    public string? AfterValue { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account? ActorAccount { get; set; }

    public virtual Tournament? Tournament { get; set; }
}
