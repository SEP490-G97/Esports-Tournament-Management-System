using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Models;

[Index("ParentNodeID", Name = "IX_BracketNode_Parent")]
[Index("TournamentID", "StageNo", "RoundNo", Name = "IX_BracketNode_Tour")]
public partial class BracketNode
{
    [Key]
    public int BracketNodeID { get; set; }

    public int TournamentID { get; set; }

    public int? ParentNodeID { get; set; }

    public int StageNo { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string StageType { get; set; } = null!;

    [StringLength(10)]
    public string? GroupLabel { get; set; }

    public int? RoundNo { get; set; }

    public int? PositionInRound { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string NodeType { get; set; } = null!;

    public int? SeedTeamID { get; set; }

    public int? SeedNo { get; set; }

    public bool IsBye { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string SeedingMethod { get; set; } = null!;

    public int? SeedingRandomSeed { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime? GeneratedAt { get; set; }

    [InverseProperty("ParentNode")]
    public virtual ICollection<BracketNode> InverseParentNode { get; set; } = new List<BracketNode>();

    [InverseProperty("BracketNode")]
    public virtual ICollection<Match> Match { get; set; } = new List<Match>();

    [ForeignKey("ParentNodeID")]
    [InverseProperty("InverseParentNode")]
    public virtual BracketNode? ParentNode { get; set; }

    [ForeignKey("SeedTeamID")]
    [InverseProperty("BracketNode")]
    public virtual Team? SeedTeam { get; set; }

    [ForeignKey("TournamentID")]
    [InverseProperty("BracketNode")]
    public virtual Tournament Tournament { get; set; } = null!;
}
