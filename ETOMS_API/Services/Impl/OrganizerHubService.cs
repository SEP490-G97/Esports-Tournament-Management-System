using ETOMS_API.Utils;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Trang "Giải của tôi" của Ban tổ chức: mỗi giải kèm giai đoạn thật, số đội, đơn chờ duyệt và việc cần làm tiếp.
// Chỉ đọc; mọi thao tác vẫn ở Bảng điều hành của từng giải.
public sealed class OrganizerHubService
{
    private readonly IUnitOfWork _uow;
    public OrganizerHubService(IUnitOfWork uow) => _uow = uow;

    public sealed class Item
    {
        public int TournamentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string GameTitle { get; set; } = string.Empty;
        public string FormatLabel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        // DRAFT | REG_UPCOMING | REG_OPEN | WAITING_START | ONGOING | COMPLETED | CANCELLED
        public string Phase { get; set; } = string.Empty;
        public string PhaseLabel { get; set; } = string.Empty;
        public DateTime RegOpenAt { get; set; }
        public DateTime RegCloseAt { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MaxTeams { get; set; }
        public int ApprovedTeams { get; set; }
        public int PendingRegistrations { get; set; }
        public bool HasBracket { get; set; }
        public bool BracketLocked { get; set; }
        public string? BannerUrl { get; set; }
        // Việc nên làm tiếp (gợi ý cho BTC), null nếu không có gì cần làm.
        public string? NextAction { get; set; }
    }

    public async Task<IReadOnlyList<Item>> ListAsync(int organizerAccountId)
    {
        var now = DateTime.UtcNow;
        var rows = await _uow.Repository<Tournament>().Query().AsNoTracking()
            .Where(t => t.OrganizerAccountId == organizerAccountId)
            .Select(t => new
            {
                t.TournamentId, t.Name, GameTitle = t.Game.Title, t.Format, t.Status, t.RegOpenAt, t.RegCloseAt, t.StartAt, t.EndAt,
                t.CreatedAt, t.MaxTeams, t.BannerUrl, t.BracketLockedAt,
                Approved = t.Registrations.Count(r => r.Status == "APPROVED"),
                Pending = t.Registrations.Count(r => r.Status == "PENDING"),
                HasBracket = t.BracketNodes.Any(),
                OpenDisputes = t.Matches.SelectMany(m => m.Disputes).Count(x => x.Status == "OPEN" || x.Status == "UNDER_REVIEW")
            })
            .ToListAsync();

        return rows.Select(t =>
        {
            var phase = t.Status switch
            {
                "DRAFT" => "DRAFT",
                "CANCELLED" => "CANCELLED",
                "COMPLETED" => "COMPLETED",
                "ONGOING" => "ONGOING",
                _ => now < t.RegOpenAt ? "REG_UPCOMING" : now < t.RegCloseAt ? "REG_OPEN"
                    : TournamentLifecycle.IsShortOfTeams(t.Status, t.BracketLockedAt != null, t.RegCloseAt, now, t.Approved, t.Format, t.MaxTeams) ? "SHORT_OF_TEAMS"
                    : "WAITING_START"
            };
            string? next = phase switch
            {
                "DRAFT" => "Hoàn tất thiết lập rồi công bố giải",
                "REG_UPCOMING" => null,
                "REG_OPEN" => t.Pending > 0 ? $"{t.Pending} đơn chờ duyệt" : null,
                "SHORT_OF_TEAMS" => $"Mới có {t.Approved} đội được duyệt — gia hạn đăng ký hoặc hủy giải",
                "WAITING_START" when t.Pending > 0 => $"{t.Pending} đơn chờ duyệt",
                "WAITING_START" when !t.HasBracket => "Đã đóng đăng ký — sinh nhánh đấu",
                "WAITING_START" when t.BracketLockedAt is null => "Kiểm tra rồi khóa nhánh đấu",
                "WAITING_START" => "Xếp lịch và phân công trọng tài",
                "ONGOING" => t.OpenDisputes > 0 ? $"{t.OpenDisputes} tranh chấp chờ xử lý" : null,
                _ => null
            };
            return new Item
            {
                TournamentId = t.TournamentId, Name = t.Name, GameTitle = t.GameTitle,
                FormatLabel = t.Format switch
                {
                    "SINGLE_ELIMINATION" => "Loại trực tiếp", "DOUBLE_ELIMINATION" => "Loại kép",
                    "ROUND_ROBIN" => "Vòng tròn", "HYBRID" => "Vòng bảng rồi loại trực tiếp", _ => t.Format
                },
                Status = t.Status, Phase = phase,
                PhaseLabel = phase switch
                {
                    "DRAFT" => "Bản nháp", "REG_UPCOMING" => "Sắp mở đăng ký", "REG_OPEN" => "Đang nhận đăng ký",
                    "SHORT_OF_TEAMS" => TournamentLifecycle.ShortOfTeamsLabel,
                    "WAITING_START" => "Chờ khai mạc", "ONGOING" => "Đang diễn ra", "COMPLETED" => "Đã kết thúc", _ => "Đã hủy"
                },
                RegOpenAt = t.RegOpenAt, RegCloseAt = t.RegCloseAt, StartAt = t.StartAt, EndAt = t.EndAt, CreatedAt = t.CreatedAt,
                MaxTeams = t.MaxTeams, ApprovedTeams = t.Approved, PendingRegistrations = t.Pending,
                HasBracket = t.HasBracket, BracketLocked = t.BracketLockedAt is not null, BannerUrl = t.BannerUrl, NextAction = next
            };
        }).ToList();
    }
}
