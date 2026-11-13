using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-06. Bộ luật mà giải đã chọn, tách bảng riêng để không thêm cột vào Tournament.
// Viết tay trước khi bảng có trong database (khi scaffold lại: xóa Repositories/EtomsDbContext.PendingTables.cs).
public partial class TournamentRuleset
{
    public int TournamentRulesetId { get; set; }

    public int TournamentId { get; set; }

    public int GameRulesetId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Tournament Tournament { get; set; } = null!;

    public virtual GameRuleset GameRuleset { get; set; } = null!;
}
