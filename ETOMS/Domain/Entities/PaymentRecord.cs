using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class PaymentRecord
{
    public int PaymentRecordId { get; set; }

    public int RegistrationId { get; set; }

    public string RecordKind { get; set; } = null!;

    public string? FileUrl { get; set; }

    public decimal? DeclaredAmount { get; set; }

    public string? Note { get; set; }

    public bool IsCurrent { get; set; }

    public int RecordedByAccountId { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual Account RecordedByAccount { get; set; } = null!;

    public virtual Registration Registration { get; set; } = null!;
}
