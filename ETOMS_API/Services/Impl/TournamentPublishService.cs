using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Impl;

// Module TOUR-09. Chỉ chuyển trạng thái DRAFT <-> PUBLISHED; hủy giải là TOUR-11.
public class TournamentPublishService : ITournamentPublishService
{
    private const string UnpublishBlockedMessage = "Giải đã có đơn đăng ký nên không thể gỡ xuất bản.";

    private readonly IUnitOfWork _uow;

    public TournamentPublishService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool?> IsOrganizerAsync(int tournamentId, int accountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        return tournament is null ? null : tournament.OrganizerAccountId == accountId;
    }

    public async Task<PublishStatusDto?> GetPublishStatusAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var checks = BuildChecks(tournament);
        var isPublished = tournament.Status == "PUBLISHED";
        var hasRegistration = isPublished && await HasRegistrationAsync(tournamentId);

        return new PublishStatusDto
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.Name,
            Status = tournament.Status,
            StatusLabel = StatusText.Tournament(tournament.Status),
            StatusClass = StatusText.TournamentClass(tournament.Status),
            CanPublish = tournament.Status == "DRAFT" && checks.All(c => c.IsOk),
            CanUnpublish = isPublished && !hasRegistration,
            UnpublishBlockedReason = hasRegistration ? UnpublishBlockedMessage : null,
            Checks = checks
        };
    }

    // ===== TOUR-09: Xuất bản giải =====
    public async Task<Result> PublishTournamentAsync(int tournamentId, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId)
            return Result.Failure("Chỉ Ban tổ chức của giải được xuất bản giải.");
        if (tournament.Status != "DRAFT")
            return Result.Failure("Chỉ giải đang nháp mới xuất bản được.");

        var missing = BuildChecks(tournament).Where(c => !c.IsOk).Select(c => c.Message).ToList();
        if (missing.Count > 0) return Result.Failure(string.Join(" ", missing));

        tournament.Status = "PUBLISHED";
        _uow.Repository<Tournament>().Update(tournament);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã xuất bản giải đấu.");
    }

    // ===== TOUR-09: Gỡ xuất bản (UC-14 A1) =====
    public async Task<Result> UnpublishTournamentAsync(int tournamentId, int organizerAccountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return Result.Failure("Không tìm thấy giải đấu.");
        if (tournament.OrganizerAccountId != organizerAccountId)
            return Result.Failure("Chỉ Ban tổ chức của giải được gỡ xuất bản.");
        if (tournament.Status != "PUBLISHED")
            return Result.Failure("Chỉ giải đã xuất bản và chưa bắt đầu mới gỡ được.");
        if (await HasRegistrationAsync(tournamentId))
            return Result.Failure(UnpublishBlockedMessage);

        tournament.Status = "DRAFT";
        _uow.Repository<Tournament>().Update(tournament);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã gỡ xuất bản, giải trở về trạng thái nháp.");
    }

    private Task<bool> HasRegistrationAsync(int tournamentId)
        => _uow.Repository<Registration>().AnyAsync(r => r.TournamentId == tournamentId);

    // Điều kiện xuất bản. Vòng bảng phải đủ cấu hình vì DB từ chối giải ngoài DRAFT thiếu số bảng.
    private static List<PublishCheckDto> BuildChecks(Tournament t)
    {
        var hasRulebook = !string.IsNullOrWhiteSpace(t.RulebookContent) && RichTextSanitizer.HasText(t.RulebookContent);
        var timelineOrdered = t.RegOpenAt < t.RegCloseAt && t.RegCloseAt < t.StartAt;
        var timelineOpen = t.RegCloseAt > DateTime.UtcNow;
        var groupsOk = t.Format switch
        {
            "ROUND_ROBIN" => t.GroupCount >= 1,
            "HYBRID" => t.GroupCount >= 1 && t.AdvancePerGroup >= 1,
            _ => true
        };

        return new List<PublishCheckDto>
        {
            Check("RULEBOOK", "Thể lệ giải", hasRulebook, "Giải chưa có thể lệ. Hãy soạn thể lệ trước khi xuất bản."),
            Check("TIMELINE", "Mốc thời gian", timelineOrdered && timelineOpen,
                timelineOrdered ? "Thời gian đăng ký đã qua. Hãy sửa lại lịch giải." : "Thời gian đóng đăng ký phải trước ngày khai mạc."),
            Check("MAX_TEAMS", "Số đội tối đa", t.MaxTeams >= 2, "Giải phải cho phép ít nhất hai đội."),
            Check("VENUE", "Địa điểm thi đấu", t.IsOnline || !string.IsNullOrWhiteSpace(t.Venue), "Giải không thi đấu trực tuyến phải có địa điểm."),
            Check("GROUPS", "Cấu hình vòng bảng", groupsOk, "Giải có vòng bảng phải khai số bảng và số đội đi tiếp."),
            Check("FEE", "Tài khoản nhận lệ phí", t.EntryFee == 0 || !string.IsNullOrWhiteSpace(t.BankAccountInfo), "Giải có lệ phí phải có tài khoản nhận tiền.")
        };
    }

    private static PublishCheckDto Check(string key, string label, bool isOk, string message)
        => new() { Key = key, Label = label, IsOk = isOk, Message = isOk ? null : message };
}
