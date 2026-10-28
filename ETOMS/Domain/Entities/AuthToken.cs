using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class AuthToken
{
    public int AuthTokenId { get; set; }

    public int AccountId { get; set; }

    public string TokenHash { get; set; } = null!;

    public string TokenType { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
