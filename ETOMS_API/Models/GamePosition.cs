using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class GamePosition
{
    public int GamePositionId { get; set; }

    public int GameRulesetId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual GameRuleset GameRuleset { get; set; } = null!;

    public virtual ICollection<RegistrationLineup> RegistrationLineups { get; set; } = new List<RegistrationLineup>();
}
