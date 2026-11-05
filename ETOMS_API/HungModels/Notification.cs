using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.HungModels;

[Index("AccountID", "IsRead", "SentAt", Name = "IX_Notification_Inbox")]
public partial class Notification
{
    [Key]
    public int NotificationID { get; set; }

    public int AccountID { get; set; }

    [StringLength(30)]
    [Unicode(false)]
    public string NotificationType { get; set; } = null!;

    [StringLength(200)]
    public string Title { get; set; } = null!;

    [StringLength(1000)]
    public string Body { get; set; } = null!;

    [StringLength(500)]
    public string? LinkUrl { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Channel { get; set; } = null!;

    public bool IsRead { get; set; }

    [Precision(0)]
    public DateTime SentAt { get; set; }

    [ForeignKey("AccountID")]
    [InverseProperty("Notification")]
    public virtual Account Account { get; set; } = null!;
}
