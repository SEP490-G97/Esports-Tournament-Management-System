using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("TokenHash", Name = "UQ_AuthToken_Hash", IsUnique = true)]
public partial class AuthToken
{
    [Key]
    public int AuthTokenID { get; set; }

    public int AccountID { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string TokenHash { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string TokenType { get; set; } = null!;

    [Precision(0)]
    public DateTime ExpiresAt { get; set; }

    [Precision(0)]
    public DateTime? UsedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("AuthToken")]
    public virtual Account Account { get; set; } = null!;
}
