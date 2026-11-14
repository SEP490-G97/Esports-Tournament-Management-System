using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class SystemBanner
{
    public int SystemBannerId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public string BannerType { get; set; } = null!;

    public bool? IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public int CreatedByAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Account CreatedByAccount { get; set; } = null!;
}
