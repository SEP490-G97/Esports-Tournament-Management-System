using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class RegistrationLineup
{
    public int RegistrationLineupId { get; set; }

    public int RegistrationId { get; set; }

    public int AccountId { get; set; }

    public string Slot { get; set; } = null!;

    public string? InGameNameSnapshot { get; set; }

    public int? PlayerCredentialId { get; set; }

    public int? GamePositionId { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual GamePosition? GamePosition { get; set; }

    public virtual PlayerCredential? PlayerCredential { get; set; }

    public virtual Registration Registration { get; set; } = null!;
}
