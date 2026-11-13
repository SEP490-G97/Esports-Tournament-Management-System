using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-07. Bảng MatchPickBan chưa có trong database chung, ánh xạ ở Repositories/EtomsDbContext.PendingTables.cs.
// Mỗi dòng là một lượt cấm/chọn bản đồ của trận, theo thứ tự StepNo.
public partial class MatchPickBan
{
    public int MatchPickBanId { get; set; }

    public int MatchId { get; set; }

    public int StepNo { get; set; }

    // BAN, PICK hoặc DECIDER (bản đồ còn lại, không đội nào chọn).
    public string Action { get; set; } = null!;

    public int? TeamId { get; set; }

    public int GameMapId { get; set; }

    public int RecordedByAccountId { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual Match Match { get; set; } = null!;

    public virtual Team? Team { get; set; }

    public virtual GameMap GameMap { get; set; } = null!;

    public virtual Account RecordedByAccount { get; set; } = null!;
}
