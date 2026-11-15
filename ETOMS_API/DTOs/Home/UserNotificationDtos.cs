namespace ETOMS_API.DTOs;

public record UserNotificationItem(int Id, string Type, string Title, string Body, string? LinkUrl, bool IsRead, DateTime SentAt);

public record UserNotificationList(List<UserNotificationItem> Items, int Total, int Unread, int Page, int PageSize);

public record UnreadCount(int Unread);