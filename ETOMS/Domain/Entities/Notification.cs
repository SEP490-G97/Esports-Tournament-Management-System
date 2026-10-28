using System;
using System.Collections.Generic;

namespace ETOMS.Domain.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int AccountId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Body { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public string Channel { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
