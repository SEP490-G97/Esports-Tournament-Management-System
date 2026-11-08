using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

// GRL-02. Viết tay trước khi bảng GameMap có trong database; đặt tên theo đúng quy ước scaffold
// để lần scaffold sau sinh ra file trùng khớp (khi đó xóa Repositories/EtomsDbContext.GameMap.cs).
public partial class GameMap
{
    public int GameMapId { get; set; }

    public int GameId { get; set; }

    public string Name { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Game Game { get; set; } = null!;
}
