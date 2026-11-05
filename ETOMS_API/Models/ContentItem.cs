using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class ContentItem
{
    public int ContentItemId { get; set; }

    public string Kind { get; set; } = null!;

    public int? TournamentId { get; set; }

    public string? NotificationType { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool IsPinned { get; set; }

    public int AuthorAccountId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Account AuthorAccount { get; set; } = null!;

    public virtual Tournament? Tournament { get; set; }
}
