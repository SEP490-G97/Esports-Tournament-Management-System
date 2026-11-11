using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-11 (UC-14 A2, A3, E2; BR-TOUR-02, BR-TOUR-05). Ban tổ chức hủy giải kèm lý do bắt buộc:
// giải chuyển sang CANCELLED, ghi AuditLog CANCEL_TOURNAMENT, báo mọi đội trưởng có đơn kèm lý do,
// và lập danh sách đội cần hoàn lệ phí. ETOMS không chuyển tiền (BR-FIN-01): việc hoàn được ghi nhận
// ở trang đối soát của REG-10, nơi chỉ cho ghi nhận hoàn với khoản đã xác nhận (CONFIRMED).
// Không cho hủy khi nhánh đã khóa (MSG-TOUR-002).
public class TournamentCancelService : ITournamentCancelService
{
    public const string LockedMessage = "Không thể huỷ giải sau khi nhánh đấu đã khoá.";
    // Đơn còn hiệu lực: đội trưởng cần được báo khi giải bị hủy.
    private static readonly string[] ActiveStatuses = { "DRAFT", "PENDING", "APPROVED", "WAITLISTED" };

    private readonly IUnitOfWork _uow;

    public TournamentCancelService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<TournamentCancelBoardDto?> GetCancelBoardAsync(int tournamentId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        return t is null ? null : await BuildAsync(t);
    }

    // ===== TOUR-11: Hủy giải =====
    public async Task<Result<TournamentCancelBoardDto>> CancelAsync(int tournamentId, int organizerAccountId, string reason)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return Result<TournamentCancelBoardDto>.Failure("Không tìm thấy giải đấu.");

        var text = (reason ?? string.Empty).Trim();
        if (text.Length == 0) return Result<TournamentCancelBoardDto>.FieldFailure("Reason", "Vui lòng nhập lý do hủy giải.");
        if (text.Length > 500) return Result<TournamentCancelBoardDto>.FieldFailure("Reason", "Lý do hủy tối đa 500 ký tự.");

        var blocked = BlockedReason(t);
        if (blocked is not null) return Result<TournamentCancelBoardDto>.Failure(blocked);

        var now = DateTime.UtcNow;
        var before = t.Status;
        t.Status = "CANCELLED";
        t.CancelReason = text;

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            ActorAccountId = organizerAccountId,
            TournamentId = t.TournamentId,
            ActionType = "CANCEL_TOURNAMENT",
            ObjectType = "TOURNAMENT",
            ObjectId = t.TournamentId,
            BeforeValue = before,
            AfterValue = "CANCELLED",
            Reason = text,
            CreatedAt = now
        });

        var registrations = await RegistrationsAsync(t.TournamentId);
        var notified = await NotifyCaptainsAsync(t, registrations, text, now);
        await _uow.SaveChangesAsync();

        var board = await BuildAsync(t);
        var message = $"Đã hủy giải và báo cho {notified} đội trưởng.";
        if (board.ToRefundCount > 0) message += $" Có {board.ToRefundCount} đội đã nộp lệ phí cần được hoàn.";
        if (board.ToVerifyCount > 0) message += $" Có {board.ToVerifyCount} đội đã gửi biên lai nhưng chưa đối soát.";
        return Result<TournamentCancelBoardDto>.Success(board, message);
    }

    private static string? BlockedReason(Tournament t)
    {
        if (t.Status == "CANCELLED") return "Giải đã bị hủy trước đó.";
        if (t.Status == "COMPLETED") return "Giải đã kết thúc nên không hủy được.";
        if (t.BracketLockedAt is not null) return LockedMessage;
        return null;
    }

    private sealed record RegistrationRow(int RegistrationId, string Status, string PaymentStatus, string TeamName, int CaptainId, string CaptainName, string CaptainEmail, bool NotifyEmail);

    private async Task<List<RegistrationRow>> RegistrationsAsync(int tournamentId)
        => await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.Team.Name)
            .Select(r => new RegistrationRow(r.RegistrationId, r.Status, r.PaymentStatus, r.Team.Name, r.Team.CaptainAccountId,
                r.Team.CaptainAccount.FullName, r.Team.CaptainAccount.Email, r.Team.CaptainAccount.NotifyEmail))
            .ToListAsync();

    // BR-TOUR-05: lý do hủy tới mọi đội trưởng có đơn, nên luôn có thông báo trong ứng dụng;
    // ai bật nhận email thì đánh dấu kênh BOTH để phần gửi email của module thông báo gửi đi.
    private async Task<int> NotifyCaptainsAsync(Tournament t, List<RegistrationRow> registrations, string reason, DateTime now)
    {
        var captains = registrations.Where(r => ActiveStatuses.Contains(r.Status) || r.PaymentStatus is "CONFIRMED" or "PROOF_SUBMITTED")
            .GroupBy(r => r.CaptainId).Select(g => g.First()).ToList();
        foreach (var c in captains)
        {
            var paid = t.EntryFee > 0 && registrations.Any(r => r.CaptainId == c.CaptainId && r.PaymentStatus is "CONFIRMED" or "PROOF_SUBMITTED");
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = c.CaptainId,
                NotificationType = "SYSTEM",
                Title = "Giải đấu đã bị hủy",
                Body = $"Giải {t.Name} đã bị hủy. Lý do: {reason}" +
                       (paid ? " Ban tổ chức sẽ liên hệ để hoàn lệ phí đội đã nộp; việc hoàn tiền thực hiện ngoài ETOMS." : string.Empty),
                Channel = c.NotifyEmail ? "BOTH" : "IN_APP",
                IsRead = false,
                SentAt = now
            });
        }
        return captains.Count;
    }

    private async Task<TournamentCancelBoardDto> BuildAsync(Tournament t)
    {
        var registrations = await RegistrationsAsync(t.TournamentId);
        var regIds = registrations.Select(r => r.RegistrationId).ToList();
        var declared = await _uow.Repository<PaymentRecord>().Query()
            .Where(p => regIds.Contains(p.RegistrationId) && p.RecordKind == "PROOF" && p.IsCurrent && p.DeclaredAmount != null)
            .ToDictionaryAsync(p => p.RegistrationId, p => p.DeclaredAmount!.Value);

        // Giải miễn phí không có khoản nào để hoàn (REG-08/REG-10 cũng không đối soát giải không thu phí).
        var refund = registrations
            .Where(r => t.EntryFee > 0 && r.PaymentStatus is "CONFIRMED" or "PROOF_SUBMITTED" or "REFUNDED")
            .Select(r => new RefundTeamDto
            {
                RegistrationId = r.RegistrationId,
                TeamName = r.TeamName,
                CaptainName = r.CaptainName,
                CaptainEmail = r.CaptainEmail,
                RegistrationStatusLabel = StatusText.Registration(r.Status),
                PaymentStatus = r.PaymentStatus,
                PaymentLabel = r.PaymentStatus switch
                {
                    "CONFIRMED" => "Đã xác nhận nộp",
                    "PROOF_SUBMITTED" => "Đã gửi biên lai, chưa đối soát",
                    _ => "Đã ghi nhận hoàn"
                },
                Amount = declared.TryGetValue(r.RegistrationId, out var amount) && amount > 0 ? Math.Min(amount, t.EntryFee) : t.EntryFee,
                Group = r.PaymentStatus switch { "CONFIRMED" => "TO_REFUND", "PROOF_SUBMITTED" => "TO_VERIFY", _ => "REFUNDED" }
            })
            .OrderBy(r => r.Group == "TO_REFUND" ? 0 : r.Group == "TO_VERIFY" ? 1 : 2).ThenBy(r => r.TeamName)
            .ToList();

        AuditLog? log = null;
        string? byName = null;
        if (t.Status == "CANCELLED")
        {
            log = await _uow.Repository<AuditLog>().Query()
                .Where(a => a.TournamentId == t.TournamentId && a.ActionType == "CANCEL_TOURNAMENT")
                .OrderByDescending(a => a.AuditLogId)
                .FirstOrDefaultAsync();
            if (log?.ActorAccountId is int actor)
                byName = await _uow.Repository<Account>().Query().Where(a => a.AccountId == actor).Select(a => a.FullName).FirstOrDefaultAsync();
        }

        var blocked = BlockedReason(t);
        return new TournamentCancelBoardDto
        {
            TournamentId = t.TournamentId,
            TournamentName = t.Name,
            Status = t.Status,
            StatusLabel = StatusText.Tournament(t.Status),
            StatusClass = StatusText.TournamentClass(t.Status),
            IsCancelled = t.Status == "CANCELLED",
            CanCancel = blocked is null,
            BlockedReason = blocked,
            CancelReason = t.CancelReason,
            CancelledAt = log?.CreatedAt,
            CancelledByName = byName,
            CaptainCount = registrations.Where(r => ActiveStatuses.Contains(r.Status) || r.PaymentStatus is "CONFIRMED" or "PROOF_SUBMITTED")
                .Select(r => r.CaptainId).Distinct().Count(),
            EntryFee = t.EntryFee,
            ToRefundCount = refund.Count(r => r.Group == "TO_REFUND"),
            ToVerifyCount = refund.Count(r => r.Group == "TO_VERIFY"),
            RefundTeams = refund
        };
    }
}
