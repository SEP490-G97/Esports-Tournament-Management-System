using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-03. Vị trí trong đội thuộc một bộ luật. Viết tay trước khi bảng GamePosition có trong database;
// đặt tên theo đúng quy ước scaffold (khi scaffold lại: xóa Repositories/EtomsDbContext.PendingTables.cs).
public partial class GamePosition
{
    public int GamePositionId { get; set; }

    public int GameRulesetId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual GameRuleset GameRuleset { get; set; } = null!;
}
