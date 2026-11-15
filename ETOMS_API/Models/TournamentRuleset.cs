using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class TournamentRuleset
{
    public int TournamentRulesetId { get; set; }

    public int TournamentId { get; set; }

    public int GameRulesetId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual GameRuleset GameRuleset { get; set; } = null!;

    public virtual Tournament Tournament { get; set; } = null!;
}
