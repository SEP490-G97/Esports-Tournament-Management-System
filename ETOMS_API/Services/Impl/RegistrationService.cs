using ETOMS_API.Utils;
using ETOMS_API.DTOs.Registration;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Phần CỦA DŨNG: REG-03 (tải minh chứng), REG-04 (theo dõi đơn).
// Phần STUB TẠM thay REG-01/06/07 (Cấn Việt Hùng): tạo đơn tối thiểu + duyệt/từ chối
// tối thiểu — chỉ đủ để module BRK (bracket) có đội APPROVED mà sinh nhánh.
public class RegistrationService : IRegistrationService
{
    private readonly IUnitOfWork _uow;

    public RegistrationService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<OpenTournamentDto>> GetOpenTournamentsAsync()
    {
        var now = DateTime.UtcNow;
        return await _uow.Repository<Tournament>().Query()
            .Where(t => t.Status == "PUBLISHED" && t.RegOpenAt <= now && t.RegCloseAt >= now && t.BracketLockedAt == null)
            .Select(t => new OpenTournamentDto
            {
                TournamentId = t.TournamentId,
                Name = t.Name,
                GameTitle = t.Game.Title,
                MaxTeams = t.MaxTeams,
                ApprovedCount = t.Registrations.Count(r => r.Status == "APPROVED"),
                RegCloseAt = t.RegCloseAt,
                RequirePaymentProof = t.RequirePaymentProof ?? false
            })
            .OrderBy(t => t.RegCloseAt)
            .ToListAsync();
    }

    public async Task<Result<int>> CreateRegistrationAsync(CreateRegistrationRequest request, int accountId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(request.TournamentId);
        if (tournament is null)
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Không tìm thấy giải đấu.");

        var now = DateTime.UtcNow;
        if (tournament.Status != "PUBLISHED")
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Giải này hiện không mở đăng ký.");
        if (tournament.BracketLockedAt != null)
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Giải đã khóa nhánh đấu, không thể đăng ký thêm.");
        if (now < tournament.RegOpenAt || now > tournament.RegCloseAt)
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Ngoài thời gian mở đăng ký của giải.");

        var team = await _uow.Repository<Team>().Query()
            .FirstOrDefaultAsync(t => t.CaptainAccountId == accountId && t.GameId == tournament.GameId && t.Status == "ACTIVE");
        if (team is null)
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Bạn phải là đội trưởng của một đội thuộc đúng tựa game của giải này.");

        var starterCount = await _uow.Repository<TeamMember>().Query()
            .CountAsync(m => m.TeamId == team.TeamId && m.Status == "ACTIVE" && m.Slot == "STARTER");
        if (starterCount < tournament.StarterCount)
            return Result<int>.FieldFailure(nameof(request.TournamentId),
                $"Đội chưa đủ số thành viên chính thức tối thiểu ({starterCount}/{tournament.StarterCount} theo yêu cầu giải).");

        if (await _uow.Repository<Registration>().AnyAsync(r =>
                r.TournamentId == tournament.TournamentId && r.TeamId == team.TeamId
                && r.Status != "WITHDRAWN" && r.Status != "REJECTED"))
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Đội của bạn đã đăng ký giải này rồi.");

        var reg = new Registration
        {
            TournamentId = tournament.TournamentId,
            TeamId = team.TeamId,
            SubmittedByAccountId = accountId,
            Status = "PENDING",
            PaymentStatus = "UNPAID",
            FinalPlacement = "NONE",
            SubmittedAt = now
        };
        await _uow.Repository<Registration>().AddAsync(reg);
        try { await _uow.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            return Result<int>.FieldFailure(nameof(request.TournamentId), "Đội của bạn đã đăng ký giải này (có thể vừa được ghi nhận).");
        }
        return Result<int>.Success(reg.RegistrationId, "Nộp đơn đăng ký thành công, đang chờ ban tổ chức duyệt.");
    }

    public async Task<IReadOnlyList<MyRegistrationDto>> GetMyRegistrationsAsync(int accountId)
    {
        var teamIds = await _uow.Repository<Team>().Query()
            .Where(t => t.CaptainAccountId == accountId)
            .Select(t => t.TeamId).ToListAsync();
        if (teamIds.Count == 0) return Array.Empty<MyRegistrationDto>();

        return await _uow.Repository<Registration>().Query()
            .Where(r => teamIds.Contains(r.TeamId))
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => new MyRegistrationDto
            {
                RegistrationId = r.RegistrationId,
                TournamentId = r.TournamentId,
                TournamentName = r.Tournament.Name,
                GameTitle = r.Tournament.Game.Title,
                Status = r.Status,
                PaymentStatus = r.PaymentStatus,
                ReviewNote = r.ReviewNote,
                WaitlistPosition = r.WaitlistPosition,
                RequirePaymentProof = r.Tournament.RequirePaymentProof ?? false,
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync();
    }

    public async Task<Result> UploadPaymentProofAsync(int registrationId, int accountId, string fileUrl, string? note)
    {
        var reg = await _uow.Repository<Registration>().Query()
            .Include(r => r.Team)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
        if (reg is null) return Result.Failure("Không tìm thấy đơn đăng ký.");
        if (reg.Team.CaptainAccountId != accountId) return Result.Failure("Chỉ đội trưởng của đơn này được tải minh chứng.");
        if (reg.Status == "REJECTED" || reg.Status == "WITHDRAWN" || reg.Status == "DISQUALIFIED")
            return Result.Failure("Đơn không còn hiệu lực, không thể tải minh chứng.");

        var oldCurrents = await _uow.Repository<PaymentRecord>().Query()
            .Where(p => p.RegistrationId == registrationId && p.RecordKind == "PROOF" && p.IsCurrent == true).ToListAsync();
        foreach (var old in oldCurrents) { old.IsCurrent = false; _uow.Repository<PaymentRecord>().Update(old); }

        await _uow.Repository<PaymentRecord>().AddAsync(new PaymentRecord
        {
            RegistrationId = registrationId,
            RecordKind = "PROOF",
            FileUrl = fileUrl,
            Note = note,
            IsCurrent = true,
            RecordedByAccountId = accountId,
            RecordedAt = DateTime.UtcNow
        });

        reg.PaymentStatus = "PROOF_SUBMITTED";
        _uow.Repository<Registration>().Update(reg);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã tải minh chứng lệ phí.");
    }

    public async Task<IReadOnlyList<PendingRegistrationDto>> GetPendingRegistrationsAsync(int tournamentId)
    {
        return await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId && r.Status == "PENDING")
            .OrderBy(r => r.SubmittedAt)
            .Select(r => new PendingRegistrationDto
            {
                RegistrationId = r.RegistrationId,
                TeamName = r.Team.Name,
                TeamTag = r.Team.Tag,
                CaptainName = r.Team.CaptainAccount.FullName,
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync();
    }

    public async Task<Result> ApproveRegistrationAsync(int registrationId, int organizerAccountId)
    {
        var reg = await _uow.Repository<Registration>().Query()
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
        if (reg is null) return Result.Failure("Không tìm thấy đơn.");
        if (reg.Tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ BTC của giải được duyệt đơn.");
        if (reg.Status != "PENDING") return Result.Failure("Đơn không ở trạng thái chờ duyệt.");

        var approvedCount = await _uow.Repository<Registration>().Query()
            .CountAsync(r => r.TournamentId == reg.TournamentId && r.Status == "APPROVED");

        if (approvedCount >= reg.Tournament.MaxTeams)
        {
            var maxWait = await _uow.Repository<Registration>().Query()
                .Where(r => r.TournamentId == reg.TournamentId && r.Status == "WAITLISTED")
                .Select(r => (int?)r.WaitlistPosition).MaxAsync() ?? 0;
            reg.Status = "WAITLISTED";
            reg.WaitlistPosition = maxWait + 1;
        }
        else
        {
            reg.Status = "APPROVED";
        }
        reg.ReviewedAt = DateTime.UtcNow;
        _uow.Repository<Registration>().Update(reg);
        await _uow.SaveChangesAsync();
        return Result.Success(reg.Status == "APPROVED" ? "Đã duyệt đơn." : "Giải đã đủ số đội, đơn được chuyển vào danh sách chờ.");
    }

    public async Task<Result> RejectRegistrationAsync(int registrationId, int organizerAccountId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure("Vui lòng nhập lý do từ chối.");

        var reg = await _uow.Repository<Registration>().Query()
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId);
        if (reg is null) return Result.Failure("Không tìm thấy đơn.");
        if (reg.Tournament.OrganizerAccountId != organizerAccountId) return Result.Failure("Chỉ BTC của giải được từ chối đơn.");
        if (reg.Status != "PENDING") return Result.Failure("Đơn không ở trạng thái chờ duyệt.");

        reg.Status = "REJECTED";
        reg.ReviewNote = reason.Trim();
        reg.ReviewedAt = DateTime.UtcNow;
        _uow.Repository<Registration>().Update(reg);
        await _uow.SaveChangesAsync();
        return Result.Success("Đã từ chối đơn.");
    }

    public async Task<bool> IsOrganizerAsync(int tournamentId, int accountId)
        => await _uow.Repository<Tournament>().AnyAsync(t => t.TournamentId == tournamentId && t.OrganizerAccountId == accountId);
}
