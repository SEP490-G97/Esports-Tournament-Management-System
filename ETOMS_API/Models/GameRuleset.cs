using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class GameRuleset
{
    public int GameRulesetId { get; set; }

    public int GameId { get; set; }

    public string Name { get; set; } = null!;

    public int MinStarters { get; set; }

    public int MaxStarters { get; set; }

    public int MaxSubstitutes { get; set; }

    public string DefaultFormat { get; set; } = null!;

    public string AllowedEvidenceTypes { get; set; } = null!;

    public int MaxEvidenceFileSizeMb { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Game Game { get; set; } = null!;

    public virtual ICollection<GameMap> GameMaps { get; set; } = new List<GameMap>();

    public virtual ICollection<GamePosition> GamePositions { get; set; } = new List<GamePosition>();

    public virtual ICollection<TournamentRuleset> TournamentRulesets { get; set; } = new List<TournamentRuleset>();
}
