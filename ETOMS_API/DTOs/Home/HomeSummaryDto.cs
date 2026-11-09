using System.ComponentModel.DataAnnotations;

namespace ETOMS_API.DTOs.Home;

public class HomeSummaryDto
{
    public HomeStatsDto Stats { get; set; } = new();
    public List<GameCardDto> Games { get; set; } = new();
    public List<FeaturedTournamentDto> Featured { get; set; } = new();
    public List<EventTickerDto> Events { get; set; } = new();
    public IReadOnlyList<ETOMS_API.DTOs.Team.TeamListItemDto> Teams { get; set; } = new List<ETOMS_API.DTOs.Team.TeamListItemDto>();
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
    public string Title { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
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
