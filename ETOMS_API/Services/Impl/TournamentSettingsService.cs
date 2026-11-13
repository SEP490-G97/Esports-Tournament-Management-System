using System.Data.Common;
using System.Text.RegularExpressions;
using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-10 (UC-13 A1, A2). Sửa thông tin giải theo trạng thái, chia 4 nhóm trường:
//   INFO     mô tả, ảnh bìa, đơn vị, khu vực, liên hệ, online/địa điểm: sửa được tới khi giải kết thúc hoặc hủy.
//   SETUP    tên, tựa game, lệ phí, tài khoản, giải thưởng, yêu cầu hồ sơ, số người: chỉ khi còn nháp,
//            vì đội đăng ký dựa trên các điều kiện này (muốn sửa thì gỡ xuất bản ở TOUR-09).
//   TIMELINE thời gian và số đội tối đa: tới khi khóa nhánh, kiểm tra lại theo UC-13 A2; mốc đã qua giữ nguyên.
//   FORMAT   thể thức, số bảng, đi tiếp, tranh hạng Ba, số ván: tới khi khóa nhánh.
// Thể lệ sửa ở TOUR-08. Ô yêu cầu giấy tờ tùy thân chưa đưa vào vì tên cột đang khác nhau giữa các nhánh.
public class TournamentSettingsService : ITournamentSettingsService
{
    public const string Info = "INFO", Setup = "SETUP", Timeline = "TIMELINE", Format = "FORMAT";
    private static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };
    private static readonly Regex BestOfPattern = new(@"^(R\d{1,2}|F):(1|3|5|7)(,(R\d{1,2}|F):(1|3|5|7))*$", RegexOptions.Compiled);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public TournamentSettingsService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<TournamentSettingsBoardDto?> GetSettingsAsync(int tournamentId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        return t is null ? null : await BuildAsync(t);
    }

    // ===== TOUR-10: Lưu thông tin giải =====
    public async Task<Result<TournamentSettingsBoardDto>> SaveSettingsAsync(int tournamentId, TournamentSettingsValues v)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return Result<TournamentSettingsBoardDto>.Failure("Không tìm thấy giải đấu.");

        var locks = LockReasons(t);
        var now = DateTime.UtcNow;
        Result<TournamentSettingsBoardDto> Fail(string field, string message) => Result<TournamentSettingsBoardDto>.FieldFailure(field, message);
        string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var regOpen = ToUtc(v.RegOpenAt);
        var regClose = ToUtc(v.RegCloseAt);
        var start = ToUtc(v.StartAt);
        DateTime? end = v.EndAt is DateTime e ? ToUtc(e) : null;
        var bestOf = Clean(v.BestOfPerRound)?.Replace(" ", "").ToUpperInvariant();
        var format = (v.Format ?? string.Empty).Trim().ToUpperInvariant();

        // Trường nào đổi mà nhóm đang khóa thì từ chối, kèm lý do của nhóm.
        var changes = new List<(string Group, string Field, bool Changed)>
        {
            (Info, "Description", Clean(v.Description) != t.Description),
            (Info, "BannerUrl", Clean(v.BannerUrl) != t.BannerUrl),
            (Info, "OrganizingUnit", Clean(v.OrganizingUnit) != t.OrganizingUnit),
            (Info, "Region", Clean(v.Region) != t.Region),
            (Info, "ContactInfo", Clean(v.ContactInfo) != t.ContactInfo),
            (Info, "IsOnline", v.IsOnline != t.IsOnline),
            (Info, "Venue", (v.IsOnline ? null : Clean(v.Venue)) != (t.IsOnline ? null : t.Venue)),
            (Setup, "Name", (v.Name ?? string.Empty).Trim() != t.Name),
            (Setup, "GameId", v.GameId != t.GameId),
            (Setup, "EntryFee", v.EntryFee != t.EntryFee),
            (Setup, "BankAccountInfo", Clean(v.BankAccountInfo) != t.BankAccountInfo),
            (Setup, "PrizeStructure", Clean(v.PrizeStructure) != t.PrizeStructure),
            (Setup, "RequirePaymentProof", v.RequirePaymentProof != t.RequirePaymentProof),
            (Setup, "RequireInGameName", v.RequireInGameName != t.RequireInGameName),
            (Setup, "StarterCount", v.StarterCount != t.StarterCount),
            (Setup, "MaxSubstitutes", v.MaxSubstitutes != t.MaxSubstitutes),
            (Timeline, "RegOpenAt", !SameMinute(regOpen, t.RegOpenAt)),
            (Timeline, "RegCloseAt", !SameMinute(regClose, t.RegCloseAt)),
            (Timeline, "StartAt", !SameMinute(start, t.StartAt)),
            (Timeline, "EndAt", !SameMinute(end, t.EndAt)),
            (Timeline, "MaxTeams", v.MaxTeams != t.MaxTeams),
            (Format, "Format", format != t.Format),
            (Format, "GroupCount", v.GroupCount != t.GroupCount),
            (Format, "AdvancePerGroup", v.AdvancePerGroup != t.AdvancePerGroup),
            (Format, "HasThirdPlaceMatch", v.HasThirdPlaceMatch != t.HasThirdPlaceMatch),
            (Format, "BestOfPerRound", bestOf != t.BestOfPerRound)
        };
        foreach (var c in changes.Where(c => c.Changed))
            if (locks[c.Group] is string reason) return Fail(c.Field, reason);
        if (!changes.Any(c => c.Changed))
            return Result<TournamentSettingsBoardDto>.Success(await BuildAsync(t), "Không có thay đổi nào.");
        bool Changed(string field) => changes.Any(c => c.Field == field && c.Changed);
        bool GroupChanged(string group) => changes.Any(c => c.Group == group && c.Changed);
        // Mỗi nhóm chỉ kiểm tra khi có trường trong nhóm đổi, để dữ liệu cũ hợp lệ theo database
        // không chặn việc sửa những phần khác.

        // ----- Thông tin chung -----
        var banner = Clean(v.BannerUrl);
        if (Changed("BannerUrl") && banner is not null && !(banner.StartsWith("/") || Uri.TryCreate(banner, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")))
            return Fail("BannerUrl", "Ảnh bìa phải là đường dẫn http(s) hoặc đường dẫn trong hệ thống.");
        if ((Changed("IsOnline") || Changed("Venue")) && !v.IsOnline && Clean(v.Venue) is null)
            return Fail("Venue", "Giải thi đấu trực tiếp phải ghi địa điểm.");

        // ----- Thiết lập -----
        if (string.IsNullOrWhiteSpace(v.Name)) return Fail("Name", "Vui lòng nhập tên giải.");
        if (Changed("GameId") && !await _uow.Repository<Game>().AnyAsync(g => g.GameId == v.GameId && g.Status == "ACTIVE"))
            return Fail("GameId", "Tựa game không tồn tại hoặc đã ngừng hoạt động.");
        if ((Changed("EntryFee") || Changed("BankAccountInfo")) && v.EntryFee > 0 && Clean(v.BankAccountInfo) is null)
            return Fail("BankAccountInfo", "Giải có lệ phí phải ghi tài khoản nhận lệ phí.");
        if (Changed("StarterCount") || Changed("MaxSubstitutes"))
        {
            var mismatch = await RulesetMismatchAsync(t, v.StarterCount, v.MaxSubstitutes);
            if (mismatch is not null) return Fail("StarterCount", mismatch);
        }

        // ----- Thời gian và quy mô (UC-13 A2) -----
        var isDraft = t.Status == "DRAFT";
        foreach (var (field, newValue, oldValue, label) in new[]
                 {
                     ("RegOpenAt", (DateTime?)regOpen, (DateTime?)t.RegOpenAt, "mở đăng ký"),
                     ("RegCloseAt", regClose, t.RegCloseAt, "đóng đăng ký"),
                     ("StartAt", start, t.StartAt, "khai mạc"),
                     ("EndAt", end, t.EndAt, "kết thúc")
                 })
        {
            if (!Changed(field)) continue;
            if (!isDraft && oldValue is DateTime old && Utc(old) <= now)
                return Fail(field, $"Mốc {label} đã qua nên không đổi được.");
            if (newValue is DateTime nv && nv <= now)
                return Fail(field, $"Thời gian {label} phải ở tương lai.");
        }
        var timelineChanged = Changed("RegOpenAt") || Changed("RegCloseAt") || Changed("StartAt") || Changed("EndAt");
        var newOpen = Changed("RegOpenAt") ? regOpen : Utc(t.RegOpenAt);
        var newClose = Changed("RegCloseAt") ? regClose : Utc(t.RegCloseAt);
        var newStart = Changed("StartAt") ? start : Utc(t.StartAt);
        var newEnd = Changed("EndAt") ? end : (t.EndAt is DateTime oe ? Utc(oe) : null);
        if (timelineChanged)
        {
            if (newOpen >= newClose) return Fail("RegCloseAt", "Thời gian đóng đăng ký phải sau thời gian mở đăng ký.");
            if (newClose >= newStart) return Fail("RegCloseAt", "Thời gian đóng đăng ký phải trước ngày khai mạc.");
            if (newEnd is DateTime endAt && endAt < newStart) return Fail("EndAt", "Ngày kết thúc không được trước ngày khai mạc.");
        }
        if (Changed("MaxTeams"))
        {
            var approved = await _uow.Repository<Registration>().Query().CountAsync(r => r.TournamentId == t.TournamentId && r.Status == "APPROVED");
            if (v.MaxTeams < approved) return Fail("MaxTeams", $"Giải đã duyệt {approved} đội nên số đội tối đa không được nhỏ hơn {approved}.");
        }

        // ----- Thể thức (kiểm tra lại cả khi đổi số đội tối đa vì số bảng phụ thuộc vào nó) -----
        var formatChanged = GroupChanged(Format) || Changed("MaxTeams");
        int? groups = t.GroupCount, advance = t.AdvancePerGroup;
        if (formatChanged)
        {
            var error = ValidateFormat(format, v, bestOf, Changed("BestOfPerRound"), out groups, out advance);
            if (error is not null) return Fail(error.Value.Field, error.Value.Message);
        }

        // ----- Lưu -----
        var noticeParts = new List<string>();
        if (!isDraft && changes.Any(c => c.Changed && c.Group == Timeline)) noticeParts.Add("thời gian hoặc số đội");
        if (!isDraft && changes.Any(c => c.Changed && c.Group == Format)) noticeParts.Add("thể thức");

        t.Description = Clean(v.Description);
        t.BannerUrl = banner;
        t.OrganizingUnit = Clean(v.OrganizingUnit);
        t.Region = Clean(v.Region);
        t.ContactInfo = Clean(v.ContactInfo);
        t.IsOnline = v.IsOnline;
        t.Venue = v.IsOnline ? null : Clean(v.Venue);
        t.Name = v.Name.Trim();
        if (Changed("GameId")) await DropRulesetLinkAsync(t.TournamentId);
        t.GameId = v.GameId;
        t.EntryFee = v.EntryFee;
        t.BankAccountInfo = Clean(v.BankAccountInfo);
        t.PrizeStructure = Clean(v.PrizeStructure);
        t.RequirePaymentProof = v.RequirePaymentProof;
        t.RequireInGameName = v.RequireInGameName;
        t.StarterCount = v.StarterCount;
        t.MaxSubstitutes = v.MaxSubstitutes;
        if (Changed("RegOpenAt")) t.RegOpenAt = regOpen;
        if (Changed("RegCloseAt")) t.RegCloseAt = regClose;
        if (Changed("StartAt")) t.StartAt = start;
        if (Changed("EndAt")) t.EndAt = end;
        t.MaxTeams = v.MaxTeams;
        if (formatChanged)
        {
            t.Format = format;
            t.GroupCount = groups;
            t.AdvancePerGroup = advance;
            t.HasThirdPlaceMatch = v.HasThirdPlaceMatch;
            t.BestOfPerRound = bestOf;
        }

        if (noticeParts.Count > 0) await NotifyTeamsAsync(t, string.Join(" và ", noticeParts), now);
        await _uow.SaveChangesAsync();

        var message = noticeParts.Count > 0 ? "Lưu thông tin thành công. Đã báo cho các đội đã đăng ký." : "Lưu thông tin thành công.";
        return Result<TournamentSettingsBoardDto>.Success(await BuildAsync(t), message);
    }

    // Số bảng, số đội đi tiếp và tranh hạng Ba phải khớp thể thức (CK_Tournament_Groups của database, BR-BRK-05).
    private static (string Field, string Message)? ValidateFormat(string format, TournamentSettingsValues v, string? bestOf, bool bestOfChanged, out int? groups, out int? advance)
    {
        groups = null;
        advance = null;
        if (!Formats.Contains(format)) return ("Format", "Thể thức không hợp lệ.");
        if (format is "ROUND_ROBIN" or "HYBRID")
        {
            if (v.GroupCount is not int g || g < 1) return ("GroupCount", "Vui lòng nhập số bảng (ít nhất 1).");
            if (g * 2 > v.MaxTeams) return ("GroupCount", $"Với {v.MaxTeams} đội tối đa, mỗi bảng phải có ít nhất 2 đội nên tối đa {v.MaxTeams / 2} bảng.");
            groups = g;
        }
        if (format == "HYBRID")
        {
            var perGroup = (int)Math.Ceiling(v.MaxTeams / (double)groups!.Value);
            if (v.AdvancePerGroup is not int a || a < 1) return ("AdvancePerGroup", "Vui lòng nhập số đội đi tiếp mỗi bảng (ít nhất 1).");
            if (a >= perGroup) return ("AdvancePerGroup", $"Mỗi bảng có tối đa {perGroup} đội nên số đội đi tiếp phải nhỏ hơn {perGroup}.");
            if (a * groups.Value < 2) return ("AdvancePerGroup", "Cần ít nhất 2 đội đi tiếp để có vòng knockout.");
            advance = a;
        }
        if (v.HasThirdPlaceMatch && format is not ("SINGLE_ELIMINATION" or "HYBRID"))
            return ("HasThirdPlaceMatch", "Trận tranh hạng Ba chỉ có ở thể thức loại trực tiếp hoặc vòng bảng rồi loại trực tiếp.");
        if (bestOfChanged && bestOf is not null && !BestOfPattern.IsMatch(bestOf))
            return ("BestOfPerRound", "Số ván mỗi vòng ghi dạng R1:1,R2:3,F:5 (R là vòng, F là chung kết, số ván 1, 3, 5 hoặc 7).");
        return null;
    }

    private static Dictionary<string, string?> LockReasons(Tournament t)
    {
        var ended = t.Status is "COMPLETED" or "CANCELLED" ? "Giải đã kết thúc hoặc đã hủy nên không sửa được." : null;
        var locked = t.BracketLockedAt is not null ? "Nhánh đấu đã khóa nên không đổi được cấu hình này." : null;
        return new Dictionary<string, string?>
        {
            [Info] = ended,
            [Setup] = ended ?? (t.Status == "DRAFT" ? null : "Giải đã xuất bản nên chỉ sửa được khi gỡ xuất bản về nháp (khi chưa có đội đăng ký)."),
            [Timeline] = ended ?? locked ?? (t.Status == "ONGOING" ? "Giải đã bắt đầu nên không đổi được thời gian và quy mô." : null),
            [Format] = ended ?? locked
        };
    }

    private async Task<TournamentSettingsBoardDto> BuildAsync(Tournament t)
    {
        var now = DateTime.UtcNow;
        var games = await _uow.Repository<Game>().Query()
            .Where(g => g.Status == "ACTIVE" || g.GameId == t.GameId)
            .OrderBy(g => g.Title)
            .Select(g => new GameOptionDto { GameId = g.GameId, Title = g.Title })
            .ToListAsync();
        var past = new List<string>();
        if (t.Status != "DRAFT")
        {
            if (Utc(t.RegOpenAt) <= now) past.Add("RegOpenAt");
            if (Utc(t.RegCloseAt) <= now) past.Add("RegCloseAt");
            if (Utc(t.StartAt) <= now) past.Add("StartAt");
            if (t.EndAt is DateTime e && Utc(e) <= now) past.Add("EndAt");
        }

        return new TournamentSettingsBoardDto
        {
            TournamentId = t.TournamentId,
            Status = t.Status,
            StatusLabel = StatusText.Tournament(t.Status),
            StatusClass = StatusText.TournamentClass(t.Status),
            IsBracketLocked = t.BracketLockedAt is not null,
            ApprovedCount = await _uow.Repository<Registration>().Query().CountAsync(r => r.TournamentId == t.TournamentId && r.Status == "APPROVED"),
            Games = games,
            LockReasons = LockReasons(t),
            PastMilestones = past,
            Values = new TournamentSettingsValues
            {
                Description = t.Description,
                BannerUrl = t.BannerUrl,
                OrganizingUnit = t.OrganizingUnit,
                Region = t.Region,
                ContactInfo = t.ContactInfo,
                IsOnline = t.IsOnline,
                Venue = t.Venue,
                Name = t.Name,
                GameId = t.GameId,
                EntryFee = t.EntryFee,
                BankAccountInfo = t.BankAccountInfo,
                PrizeStructure = t.PrizeStructure,
                RequirePaymentProof = t.RequirePaymentProof,
                RequireInGameName = t.RequireInGameName,
                StarterCount = t.StarterCount,
                MaxSubstitutes = t.MaxSubstitutes,
                RegOpenAt = t.RegOpenAt,
                RegCloseAt = t.RegCloseAt,
                StartAt = t.StartAt,
                EndAt = t.EndAt,
                MaxTeams = t.MaxTeams,
                Format = t.Format,
                GroupCount = t.GroupCount,
                AdvancePerGroup = t.AdvancePerGroup,
                HasThirdPlaceMatch = t.HasThirdPlaceMatch,
                BestOfPerRound = t.BestOfPerRound
            }
        };
    }

    // Bộ luật đã chọn (GRL-06) giới hạn số người. Bảng chưa có trong database thì bỏ qua kiểm tra.
    private async Task<string?> RulesetMismatchAsync(Tournament t, int starters, int substitutes)
    {
        try
        {
            var ruleset = await _uow.Repository<TournamentRuleset>().Query()
                .Where(x => x.TournamentId == t.TournamentId)
                .Select(x => x.GameRuleset)
                .FirstOrDefaultAsync();
            if (ruleset is null) return null;
            if (starters < ruleset.MinStarters || starters > ruleset.MaxStarters)
                return ruleset.MinStarters == ruleset.MaxStarters
                    ? $"Bộ luật {ruleset.Name} của giải yêu cầu {ruleset.MinStarters} người chính thức."
                    : $"Bộ luật {ruleset.Name} của giải yêu cầu {ruleset.MinStarters}–{ruleset.MaxStarters} người chính thức.";
            if (substitutes > ruleset.MaxSubstitutes)
                return $"Bộ luật {ruleset.Name} của giải cho tối đa {ruleset.MaxSubstitutes} dự bị.";
            return null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    // Đổi tựa game thì bộ luật cũ không còn hợp lệ (GRL-06), bỏ liên kết để Ban tổ chức chọn lại.
    private async Task DropRulesetLinkAsync(int tournamentId)
    {
        try
        {
            var link = await _uow.Repository<TournamentRuleset>().Query().FirstOrDefaultAsync(x => x.TournamentId == tournamentId);
            if (link is not null) _uow.Repository<TournamentRuleset>().Remove(link);
        }
        catch (DbException)
        {
            // Bảng TournamentRuleset chưa có trong database: không có liên kết nào để bỏ.
        }
    }

    private async Task NotifyTeamsAsync(Tournament t, string what, DateTime now)
    {
        var captains = await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == t.TournamentId && (r.Status == "PENDING" || r.Status == "APPROVED" || r.Status == "WAITLISTED")
                && r.Team.CaptainAccount.NotifyInApp)
            .Select(r => r.Team.CaptainAccountId)
            .Distinct()
            .ToListAsync();
        foreach (var accountId in captains)
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = accountId,
                NotificationType = "SYSTEM",
                Title = "Giải thay đổi thông tin",
                Body = $"Ban tổ chức giải {t.Name} vừa thay đổi {what}. Khai mạc lúc {Vn(t.StartAt)}, đóng đăng ký lúc {Vn(t.RegCloseAt)}.",
                Channel = "IN_APP",
                IsRead = false,
                SentAt = now
            });
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime ToUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
    }

    private static bool SameMinute(DateTime? a, DateTime? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.Value.Ticks / TimeSpan.TicksPerMinute == b.Value.Ticks / TimeSpan.TicksPerMinute;
    }

    private static string Vn(DateTime utc) => (Utc(utc) + VietnamOffset).ToString("HH:mm dd/MM/yyyy");
}
