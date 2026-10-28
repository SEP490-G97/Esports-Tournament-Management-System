using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class BracketNode
{
    public int BracketNodeId { get; set; }

    public int TournamentId { get; set; }

    public int? ParentNodeId { get; set; }

    public int StageNo { get; set; }

    public string StageType { get; set; } = null!;

    public string? GroupLabel { get; set; }

    public int? RoundNo { get; set; }

    public int? PositionInRound { get; set; }

    public string NodeType { get; set; } = null!;

    public int? SeedTeamId { get; set; }

    public int? SeedNo { get; set; }

    public bool IsBye { get; set; }

    public string SeedingMethod { get; set; } = null!;

    public int? SeedingRandomSeed { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? GeneratedAt { get; set; }

    public virtual ICollection<BracketNode> InverseParentNode { get; set; } = new List<BracketNode>();

    public virtual ICollection<Match> Matches { get; set; } = new List<Match>();

    public virtual BracketNode? ParentNode { get; set; }

    public virtual Team? SeedTeam { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;
}
