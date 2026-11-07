using System.Text.Encodings.Web;
using System.Text.Json;
using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-08. Tournament chỉ lưu bản thể lệ hiện hành; các bản cũ nằm trong AuditLog
// (BeforeValue/AfterValue dạng JSON { version, content }), vì schema không có bảng phiên bản.
public class RulebookService : IRulebookService
{
    private const string AuditAction = "RULEBOOK_EDIT";
    private const string AuditObject = "Tournament";

    // Giữ nguyên chữ tiếng Việt trong JSON nhật ký để đọc được trên trang Audit Log.
    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IUnitOfWork _uow;

    public RulebookService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool?> IsOrganizerAsync(int tournamentId, int accountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        return tournament is null ? null : tournament.OrganizerAccountId == accountId;
    }

    public async Task<RulebookDto?> GetRulebookAsync(int tournamentId, int? version, int? viewerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null || !CanView(tournament, viewerAccountId)) return null;

        var dto = new RulebookDto
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.Name,
            TournamentStatus = tournament.Status,
            Version = tournament.RulebookVersion,
            CurrentVersion = tournament.RulebookVersion,
            Content = SanitizeForDisplay(tournament.RulebookContent),
            CanEdit = tournament.OrganizerAccountId == viewerAccountId && IsEditable(tournament)
        };

        if (version is null || version == tournament.RulebookVersion) return dto;

        var snapshot = (await LoadHistoryAsync(tournamentId)).FirstOrDefault(h => h.Version == version);
        if (snapshot is null) return null;

        dto.Version = snapshot.Version;
        dto.Content = SanitizeForDisplay(snapshot.Content);
        return dto;
    }

    public async Task<IReadOnlyList<RulebookVersionDto>?> GetVersionsAsync(int tournamentId, int? viewerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null || !CanView(tournament, viewerAccountId)) return null;

        var versions = (await LoadHistoryAsync(tournamentId))
            .Select(h => new RulebookVersionDto
            {
                Version = h.Version,
                SavedAt = h.SavedAt,
                SavedByName = h.SavedByName,
                IsCurrent = h.Version == tournament.RulebookVersion
            })
            .ToList();

        // Bản hiện hành được lưu trước khi có nhật ký thì chưa nằm trong lịch sử.
        if (!string.IsNullOrWhiteSpace(tournament.RulebookContent) && versions.All(v => !v.IsCurrent))
            versions.Add(new RulebookVersionDto { Version = tournament.RulebookVersion, IsCurrent = true });

        return versions.OrderByDescending(v => v.Version).ToList();
    }

    // ===== TOUR-08: Lưu thể lệ. Giải còn nháp giữ nguyên phiên bản vì chưa đội nào xem được;
    // từ khi xuất bản, mỗi lần đổi nội dung tăng 1 và báo cho các đội đã đăng ký =====
    public async Task<Result<RulebookSavedDto>> SaveRulebookAsync(int tournamentId, SaveRulebookRequest request, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result<RulebookSavedDto>.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId)
            return Result<RulebookSavedDto>.Failure("Chỉ Ban tổ chức của giải được sửa thể lệ.");
        if (!IsEditable(tournament))
            return Result<RulebookSavedDto>.Failure("Giải đã kết thúc hoặc đã hủy, không thể sửa thể lệ.");
        if (request.BaseVersion != tournament.RulebookVersion)
            return Result<RulebookSavedDto>.Failure($"Thể lệ đã được lưu thành phiên bản {tournament.RulebookVersion} ở nơi khác. Vui lòng tải lại trang trước khi sửa.");

        var content = RichTextSanitizer.Sanitize(request.Content);
        if (!RichTextSanitizer.HasText(content))
            return Result<RulebookSavedDto>.FieldFailure(nameof(request.Content), "Vui lòng nhập đầy đủ thông tin bắt buộc.");

        if (content == tournament.RulebookContent)
            return Result<RulebookSavedDto>.Success(
                new RulebookSavedDto { Version = tournament.RulebookVersion, Changed = false },
                "Thể lệ không có thay đổi nên không tạo phiên bản mới.");

        var previousContent = tournament.RulebookContent;
        var previousVersion = tournament.RulebookVersion;
        var isFirstSave = string.IsNullOrWhiteSpace(previousContent);
        var createsNewVersion = !isFirstSave && tournament.Status != "DRAFT";
        var newVersion = createsNewVersion ? previousVersion + 1 : previousVersion;

        tournament.RulebookContent = content;
        tournament.RulebookVersion = newVersion;
        _uow.Repository<Tournament>().Update(tournament);

        var now = DateTime.UtcNow;
        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = organizerAccountId,
            TournamentId = tournamentId,
            ActionType = AuditAction,
            ObjectType = AuditObject,
            ObjectId = tournamentId,
            BeforeValue = isFirstSave ? null : Serialize(previousVersion, previousContent!),
            AfterValue = Serialize(newVersion, content),
            CreatedAt = now
        });

        var notified = 0;
        if (createsNewVersion)
        {
            var captains = await GetCaptainsToNotifyAsync(tournamentId);
            foreach (var captainId in captains)
            {
                await _uow.Repository<Notification>().AddAsync(new Notification
                {
                    AccountId = captainId,
                    NotificationType = "NEWS",
                    Title = "Thể lệ giải đã được cập nhật",
                    Body = $"Giải {tournament.Name} có thể lệ phiên bản {newVersion}. Bản đội bạn đã chấp nhận không thay đổi.",
                    LinkUrl = $"/tournament/rulebook-history.html?id={tournamentId}",
                    Channel = "IN_APP",
                    IsRead = false,
                    SentAt = now
                });
            }
            notified = captains.Count;
        }

        await _uow.SaveChangesAsync();

        return Result<RulebookSavedDto>.Success(
            new RulebookSavedDto { Version = newVersion, Changed = true, NotifiedCaptains = notified },
            "Lưu thông tin thành công.");
    }

    private static bool IsEditable(Tournament t) => t.Status != "COMPLETED" && t.Status != "CANCELLED";

    // Trang hiển thị dùng innerHTML, mà cột này còn được ghi từ dữ liệu mẫu và chức năng tạo giải.
    private static string? SanitizeForDisplay(string? content)
        => content is null ? null : RichTextSanitizer.Sanitize(content);

    // Giải nháp không công khai: người ngoài nhận 404 như giải không tồn tại (UC-36 E2).
    private static bool CanView(Tournament t, int? viewerAccountId)
        => t.Status != "DRAFT" || t.OrganizerAccountId == viewerAccountId;

    // Đổi thể lệ ràng buộc các đội nên luôn gửi, không xét Account.NotifyInApp.
    private async Task<List<int>> GetCaptainsToNotifyAsync(int tournamentId)
        => await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId
                && (r.Status == "PENDING" || r.Status == "APPROVED" || r.Status == "WAITLISTED"))
            .Select(r => r.Team.CaptainAccountId)
            .Distinct()
            .ToListAsync();

    private static string Serialize(int version, string content)
        => JsonSerializer.Serialize(new RulebookSnapshot { Version = version, Content = content }, SnapshotJson);

    private static RulebookSnapshot? Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<RulebookSnapshot>(json, SnapshotJson); }
        catch (JsonException) { return null; }
    }

    // Dựng lại các phiên bản từ nhật ký. Bản được nhắc tới ở BeforeValue mà chưa từng là
    // AfterValue là bản lưu trước khi có nhật ký nên không biết thời điểm và người lưu.
    private async Task<List<RulebookHistoryItem>> LoadHistoryAsync(int tournamentId)
    {
        var logs = await _uow.Repository<AuditLog>().Query()
            .Where(a => a.TournamentId == tournamentId && a.ActionType == AuditAction && a.ObjectType == AuditObject)
            .OrderBy(a => a.AuditLogId)
            .Select(a => new { a.BeforeValue, a.AfterValue, a.CreatedAt, ActorName = a.ActorAccount != null ? a.ActorAccount.FullName : null })
            .ToListAsync();

        var byVersion = new Dictionary<int, RulebookHistoryItem>();
        foreach (var log in logs)
        {
            var before = Deserialize(log.BeforeValue);
            if (before is not null && !byVersion.ContainsKey(before.Version))
                byVersion[before.Version] = new RulebookHistoryItem(before.Version, before.Content, null, null);

            var after = Deserialize(log.AfterValue);
            if (after is not null)
                byVersion[after.Version] = new RulebookHistoryItem(after.Version, after.Content, log.CreatedAt, log.ActorName);
        }
        return byVersion.Values.ToList();
    }

    private sealed class RulebookSnapshot
    {
        public int Version { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    private sealed record RulebookHistoryItem(int Version, string Content, DateTime? SavedAt, string? SavedByName);
}
