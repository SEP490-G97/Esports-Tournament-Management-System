using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-03. Viết tay trước khi bảng GamePosition có trong database; đặt tên theo đúng quy ước scaffold
// để lần scaffold sau sinh ra file trùng khớp (khi đó xóa Repositories/EtomsDbContext.PendingTables.cs).
public partial class GamePosition
{
    public int GamePositionId { get; set; }

    public int GameId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Game Game { get; set; } = null!;
}
