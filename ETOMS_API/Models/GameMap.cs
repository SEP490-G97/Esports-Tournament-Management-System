using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class GameMap
{
    public int GameMapId { get; set; }

    public int GameRulesetId { get; set; }

    public string Name { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual GameRuleset GameRuleset { get; set; } = null!;

    public virtual ICollection<MatchPickBan> MatchPickBans { get; set; } = new List<MatchPickBan>();
}
