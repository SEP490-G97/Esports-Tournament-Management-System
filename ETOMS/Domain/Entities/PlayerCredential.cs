using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class PlayerCredential
{
    public int PlayerCredentialId { get; set; }

    public int AccountId { get; set; }

    public int? GameId { get; set; }

    public string CredentialType { get; set; } = null!;

    public string? Value { get; set; }

    public string? FileUrl { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Game? Game { get; set; }

    public virtual ICollection<RegistrationLineup> RegistrationLineups { get; set; } = new List<RegistrationLineup>();
}
