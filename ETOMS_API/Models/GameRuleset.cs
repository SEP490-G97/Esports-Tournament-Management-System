using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-01. Viết tay trước khi bảng GameRuleset có trong database; đặt tên theo đúng quy ước scaffold
// để lần scaffold sau sinh ra file trùng khớp (khi đó xóa Repositories/EtomsDbContext.PendingTables.cs).
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
}
