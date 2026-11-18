namespace ETOMS_API.Utils;

/// <summary>Hằng dùng riêng cho nhóm chức năng System Admin (không phụ thuộc nơi khai báo AccountRoles của dự án).</summary>
public static class AdminConstants
{
    public const string AdminRole = "ADMIN";

    // ---- Game (ADM-03)
    public const string GameActive = "ACTIVE";
    public const string GameInactive = "INACTIVE";

    // Game.DefaultFormat (CK_Game_Format)
    public static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };

    // Giá trị cho Game.AllowedEvidenceTypes (lưu dạng chuỗi phân tách bằng dấu phẩy).
    // GIẢ ĐỊNH: schema không có CHECK và file SQL không có dữ liệu mẫu; đối chiếu với module nộp bằng chứng.
    public static readonly string[] EvidenceTypes = { "IMAGE", "VIDEO", "REPLAY", "DOCUMENT" };

    /// <summary>GameRuleset có CK_GameRuleset_File: MaxEvidenceFileSizeMB trong 1..100, nên Game cũng giới hạn như vậy.</summary>
    public const int MaxEvidenceFileSizeMb = 100;

    // ---- Thông báo & Banner (ADM-08)
    public static readonly string[] Roles = { "PLAYER", "ORGANIZER", "ADMIN" };
    public const string RolesAll = "ALL";
    public const string AccountActive = "ACTIVE";

    /// <summary>Notification.NotificationType (CK_Notification_Type).</summary>
    public const string NotificationSystem = "SYSTEM";

    /// <summary>SystemAnnouncement.Channel / Notification.Channel (CK_..._Channel).</summary>
    public const string ChannelInApp = "IN_APP";
    public const string ChannelEmail = "EMAIL";
    public const string ChannelBoth = "BOTH";
    public static readonly string[] Channels = { ChannelInApp, ChannelEmail, ChannelBoth };

    /// <summary>SystemBanner.BannerType (CK_SystemBanner_Type).</summary>
    public const string BannerEvent = "EVENT";
    public const string BannerGiveaway = "GIVEAWAY";
    public static readonly string[] BannerTypes = { BannerEvent, BannerGiveaway };

    /// <summary>Số banner "đang áp dụng" (chưa hết hạn) tối đa, để màn hình sắp xếp luôn gói trong một trang.</summary>
    public const int MaxCurrentBanners = 50;
}

/// <summary>Mã ActionType hợp lệ theo CK_AuditLog_Action (bản update.sql mới nhất).</summary>
public static class AdminAuditActions
{
    public const string GameCreate = "GAME_CREATE";
    public const string GameUpdate = "GAME_UPDATE";
    public const string GameStatus = "GAME_STATUS";
    public const string GameDelete = "GAME_DELETE";

    public const string AnnounceSend = "ANNOUNCE_SEND";
    public const string BannerCreate = "BANNER_CREATE";
    public const string BannerUpdate = "BANNER_UPDATE";
    public const string BannerDelete = "BANNER_DELETE";
    public const string BannerReorder = "BANNER_REORDER";
}