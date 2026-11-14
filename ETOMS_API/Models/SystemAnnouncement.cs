using System;
using System.Collections.Generic;

namespace ETOMS_API.Models;

public partial class SystemAnnouncement
{
    public int SystemAnnouncementId { get; set; }

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public string TargetRoles { get; set; } = null!;

    public string Channel { get; set; } = null!;

    public int InAppCount { get; set; }

    public int EmailCount { get; set; }

    public int SentByAccountId { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Account SentByAccount { get; set; } = null!;
}
