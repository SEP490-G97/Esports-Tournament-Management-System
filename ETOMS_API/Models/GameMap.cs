using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-02. Bản đồ thuộc một bộ luật. Viết tay trước khi bảng GameMap có trong database; đặt tên theo
// đúng quy ước scaffold (khi scaffold lại: xóa Repositories/EtomsDbContext.PendingTables.cs).
public partial class GameMap
{
    public int GameMapId { get; set; }

    public int GameRulesetId { get; set; }

    public string Name { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual GameRuleset GameRuleset { get; set; } = null!;
}
