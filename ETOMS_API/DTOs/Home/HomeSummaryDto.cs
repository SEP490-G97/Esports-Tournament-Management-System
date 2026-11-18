using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Home;

public class HomeSummaryDto
{
    public HomeStatsDto Stats { get; set; } = new();
    public List<GameCardDto> Games { get; set; } = new();
    public List<FeaturedTournamentDto> Featured { get; set; } = new();
    public List<EventTickerDto> Events { get; set; } = new();
    public IReadOnlyList<HomeTeamDto> Teams { get; set; } = new List<HomeTeamDto>();
    public List<NewsItemDto> News { get; set; } = new();
    public List<HomeSponsorDto> Sponsors { get; set; } = new();
    public List<HomeLiveDto> LiveNow { get; set; } = new();
}

// UC-46 — livestream đang phát của các giải công khai, để người xem tìm thấy ngay từ trang chủ.
public class HomeLiveDto
{
    public int LivestreamId { get; set; }
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PlatformLabel { get; set; } = string.Empty;
}

// UC-42 — nhà tài trợ luôn gắn với MỘT giải; trang chủ chỉ giới thiệu nhà tài trợ của các giải đang công khai,
// không có "quảng cáo chung toàn nền tảng".
public class HomeSponsorDto
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string TierLabel { get; set; } = string.Empty;
    public int TournamentId { get; set; }
    public string TournamentName { get; set; } = string.Empty;
}

// Tin tức đăng trên trang chủ — lấy từ ContentItem (Kind='NEWS', Status='PUBLISHED').
// Tin được ghim (IsPinned) hiển thị làm tin nổi bật, còn lại xếp vào danh sách bên cạnh.
public class NewsItemDto
{
    public int ContentItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class HomeStatsDto
{
    public int Tournaments { get; set; }
    public int Teams { get; set; }
    public int Players { get; set; }
    public int Games { get; set; }
}

public class GameCardDto
{
    public int GameId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    // Số giải công khai (không tính bản nháp / đã hủy) của game.
    public int TournamentCount { get; set; }
}

// Dữ liệu minh họa khu "Giải đấu nổi bật" (chưa có module Giải đấu đầy đủ nên dùng Tournament thật + nhãn mô tả).
public class FeaturedTournamentDto
{
    public int TournamentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Game { get; set; } = string.Empty;
    public string Prize { get; set; } = string.Empty;
    public int Teams { get; set; }
    public int MaxTeams { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = "badge-soft";
    public bool RegistrationOpen { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

public class EventTickerDto
{
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    // Bấm vào mục: trang đăng ký của giải (đang mở đăng ký) hoặc trang giải.
    public string Url { get; set; } = string.Empty;
    // REGISTER | LIVE | UPCOMING — để tô màu nhãn.
    public string Kind { get; set; } = string.Empty;
}

// Form đăng ký nhận tin sự kiện — có validation.
public class SubscribeRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [StringLength(256, ErrorMessage = "Email quá dài.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

// Đội tuyển nổi bật ở trang chủ: chỉ đội đủ người thi đấu theo luật tựa game.
public class HomeTeamDto
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string GameTitle { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    // Quân số tối đa theo luật game (chính thức + dự bị), vd LMHT 8, FC Online 1.
    public int MaxRoster { get; set; }
    public int Wins { get; set; }
}
