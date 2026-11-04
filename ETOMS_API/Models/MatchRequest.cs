using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

public partial class MatchRequest
{
    [Key]
    public int MatchRequestID { get; set; }

    public int MatchID { get; set; }

    public int? TeamID { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string RequestKind { get; set; } = null!;

    public int? OutAccountID { get; set; }

    public int? InAccountID { get; set; }

    [StringLength(500)]
    public string Reason { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    public int RequestedByAccountID { get; set; }

    public int? DecidedByAccountID { get; set; }

    [Precision(0)]
    public DateTime? DecidedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("DecidedByAccountID")]
    [InverseProperty("MatchRequestDecidedByAccount")]
    public virtual Account? DecidedByAccount { get; set; }

    [ForeignKey("InAccountID")]
    [InverseProperty("MatchRequestInAccount")]
    public virtual Account? InAccount { get; set; }

    [ForeignKey("MatchID")]
    [InverseProperty("MatchRequest")]
    public virtual Match Match { get; set; } = null!;

    [ForeignKey("OutAccountID")]
    [InverseProperty("MatchRequestOutAccount")]
    public virtual Account? OutAccount { get; set; }

    [ForeignKey("RequestedByAccountID")]
    [InverseProperty("MatchRequestRequestedByAccount")]
    public virtual Account RequestedByAccount { get; set; } = null!;

    [ForeignKey("TeamID")]
    [InverseProperty("MatchRequest")]
    public virtual Team? Team { get; set; }
}
