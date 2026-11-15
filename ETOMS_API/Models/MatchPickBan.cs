using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class MatchPickBan
{
    public int MatchPickBanId { get; set; }

    public int MatchId { get; set; }

    public int StepNo { get; set; }

    public string Action { get; set; } = null!;

    public int? TeamId { get; set; }

    public int GameMapId { get; set; }

    public int RecordedByAccountId { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual GameMap GameMap { get; set; } = null!;

    public virtual Match Match { get; set; } = null!;

    public virtual Account RecordedByAccount { get; set; } = null!;

    public virtual Team? Team { get; set; }
}
