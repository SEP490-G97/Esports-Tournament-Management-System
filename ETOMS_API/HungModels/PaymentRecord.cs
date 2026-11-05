using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

public partial class PaymentRecord
{
    [Key]
    public int PaymentRecordID { get; set; }

    public int RegistrationID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string RecordKind { get; set; } = null!;

    [StringLength(500)]
    public string? FileUrl { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DeclaredAmount { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public bool IsCurrent { get; set; }

    public int RecordedByAccountID { get; set; }

    [Precision(0)]
    public DateTime RecordedAt { get; set; }

    [ForeignKey("RecordedByAccountID")]
    [InverseProperty("PaymentRecord")]
    public virtual Account RecordedByAccount { get; set; } = null!;

    [ForeignKey("RegistrationID")]
    [InverseProperty("PaymentRecord")]
    public virtual Registration Registration { get; set; } = null!;
}
