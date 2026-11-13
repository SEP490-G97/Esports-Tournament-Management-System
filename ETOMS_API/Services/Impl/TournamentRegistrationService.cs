using System.Data;
using ETOMS_API.DTOs.Registration;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class TournamentRegistrationService : ITournamentRegistrationService
{
    private const string InGameNameType = "IN_GAME_NAME";
    private const string IdentityDocumentType = "IDENTITY_DOCUMENT";
    private static readonly TimeSpan WaitlistConfirmationWindow = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivateFileStorage _fileStorage;

    public TournamentRegistrationService(IUnitOfWork unitOfWork, PrivateFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }

    private IQueryable<TEntity> Query<TEntity>() where TEntity : class
        => _unitOfWork.Repository<TEntity>().Query();

    private void Add<TEntity>(TEntity entity) where TEntity : class
        => _unitOfWork.Repository<TEntity>().Add(entity);

    public async Task<Result<RegistrationOptionsResponse>> GetOptionsAsync(
        int tournamentId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var tournament = await Query<Tournament>().AsNoTracking()
            .Include(item => item.Game)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationOptionsResponse>("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        }

        var account = await Query<Account>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (account is null || account.EmailVerifiedAt is null || account.Status != "ACTIVE")
        {
            return Result.Failure<RegistrationOptionsResponse>(
                "Tài khoản phải hoạt động và xác minh email trước khi đăng ký giải.",
                StatusCodes.Status403Forbidden);
        }

        if (!IsRegistrationWindowOpen(tournament, DateTime.UtcNow))
        {
            return Result.Failure<RegistrationOptionsResponse>("Giải hiện không nhận đăng ký.", StatusCodes.Status409Conflict);
        }

        var registeredTeamIds = await Query<Registration>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId
                && item.Status != "DRAFT"
                && item.Status != "WITHDRAWN"
                && item.Status != "REJECTED"
                && item.Status != "EXPIRED")
            .Select(item => item.TeamId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var teams = await Query<Team>().AsNoTracking()
            .Where(item => item.CaptainAccountId == accountId
                && item.GameId == tournament.GameId
                && item.Status == "ACTIVE"
                && !registeredTeamIds.Contains(item.TeamId))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(item => item.TeamId).ToArray();
        var members = await Query<TeamMember>().AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.Status == "ACTIVE")
            .Select(item => new
            {
                item.TeamId,
                item.AccountId,
                item.Slot,
                item.Account.FullName,
                item.Account.Status
            })
            .ToListAsync(cancellationToken);
        var memberIds = members.Select(item => item.AccountId).Distinct().ToArray();
        var credentials = await Query<PlayerCredential>().AsNoTracking()
            .Where(item => memberIds.Contains(item.AccountId)
                && ((item.CredentialType == InGameNameType && item.GameId == tournament.GameId)
                    || (item.CredentialType == IdentityDocumentType && item.FileUrl != null)))
            .Select(item => new { item.AccountId, item.CredentialType, item.Value, item.FileUrl })
            .ToListAsync(cancellationToken);
        var teamOptions = teams.Select(team => new RegistrationTeamOptionResponse(
            team.TeamId,
            team.Name,
            team.Tag,
            members.Where(member => member.TeamId == team.TeamId && member.Status == "ACTIVE")
                .OrderBy(member => member.Slot == "STARTER" ? 0 : 1)
                .ThenBy(member => member.FullName)
                .Select(member => new RegistrationRosterMemberResponse(
                    member.AccountId,
                    member.FullName,
                    member.Slot,
                    credentials.FirstOrDefault(credential =>
                        credential.AccountId == member.AccountId
                        && credential.CredentialType == InGameNameType
                        && !string.IsNullOrWhiteSpace(credential.Value))?.Value,
                    credentials.Any(credential =>
                        credential.AccountId == member.AccountId
                        && credential.CredentialType == IdentityDocumentType
                        && !string.IsNullOrWhiteSpace(credential.FileUrl))))
                .ToArray()))
            .ToArray();

        var occupied = await CountOccupiedSlotsAsync(tournamentId, DateTime.UtcNow, cancellationToken);
        return Result.Success(new RegistrationOptionsResponse(
            tournament.TournamentId,
            tournament.Name,
            tournament.Game.Title,
            tournament.RegOpenAt,
            tournament.RegCloseAt,
            tournament.MaxTeams,
            tournament.StarterCount,
            tournament.MaxSubstitutes,
            tournament.RequireInGameName,
            tournament.RequireIdentityDocument,
            tournament.RequirePaymentProof,
            tournament.EntryFee,
            tournament.BankAccountInfo,
            tournament.RulebookVersion,
            tournament.RulebookContent,
            Math.Max(0, tournament.MaxTeams - occupied),
            teamOptions));
    }

    public async Task<Result<RegistrationResponse>> CreateAsync(
        int tournamentId,
        int accountId,
        CreateRegistrationRequest request,
        IFormFile? paymentProof,
        decimal? declaredAmount,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var requestedLineup = request.Lineup ?? [];
        var now = DateTime.UtcNow;
        var account = await Query<Account>().FirstOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (account is null || account.EmailVerifiedAt is null || account.Status != "ACTIVE")
        {
            return Result.Failure<RegistrationResponse>(
                "Tài khoản phải hoạt động và xác minh email trước khi đăng ký giải.",
                StatusCodes.Status403Forbidden);
        }

        var tournament = await Query<Tournament>().Include(item => item.Game)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy giải đấu.", StatusCodes.Status404NotFound);
        }
        if (!IsRegistrationWindowOpen(tournament, now))
        {
            return Result.Failure<RegistrationResponse>("Giải hiện không nhận đăng ký.", StatusCodes.Status409Conflict);
        }
        if (!request.SaveAsDraft && !request.AcceptRulebook)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.AcceptRulebook), "Bạn cần xác nhận đồng ý với thể lệ giải.");
        }
        if (!request.SaveAsDraft && tournament.RequirePaymentProof && tournament.EntryFee > 0 && paymentProof is null)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(paymentProof), "Giải yêu cầu tải biên lai lệ phí trước khi gửi đơn.");
        }
        if (declaredAmount < 0)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(declaredAmount), "Số tiền khai báo không được âm.");
        }

        var team = await Query<Team>().FirstOrDefaultAsync(item => item.TeamId == request.TeamId, cancellationToken);
        if (team is null || team.CaptainAccountId != accountId || team.Status != "ACTIVE")
        {
            return Result.Failure<RegistrationResponse>("Bạn không phải đội trưởng của đội hợp lệ.", StatusCodes.Status403Forbidden);
        }
        if (team.GameId != tournament.GameId)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.TeamId), "Đội và giải phải thuộc cùng một tựa game.");
        }

        var existingDraft = await Query<Registration>()
            .Include(item => item.RegistrationLineups)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId
                && item.TeamId == team.TeamId
                && item.Status == "DRAFT",
                cancellationToken);
        var duplicate = await Query<Registration>().AnyAsync(item =>
            item.TournamentId == tournamentId
            && item.TeamId == team.TeamId
            && item.Status != "DRAFT"
            && item.Status != "WITHDRAWN"
            && item.Status != "REJECTED"
            && item.Status != "EXPIRED",
            cancellationToken);
        if (duplicate)
        {
            return Result.Failure<RegistrationResponse>("Đội đã có đơn còn hiệu lực trong giải này.", StatusCodes.Status409Conflict);
        }

        var lineupValidation = request.SaveAsDraft
            ? await ValidateDraftLineupAsync(tournament, team.TeamId, requestedLineup, cancellationToken)
            : await ValidateLineupAsync(tournament, team.TeamId, requestedLineup, cancellationToken);
        if (!lineupValidation.IsSuccess)
        {
            return Result<RegistrationResponse>.Failure(
                lineupValidation.Message,
                lineupValidation.StatusCode);
        }

        if (!request.SaveAsDraft)
        {
            var selectedAccountIds = requestedLineup.Select(item => item.AccountId).Distinct().ToArray();
            var playersAlreadyRegistered = await Query<RegistrationLineup>().AsNoTracking()
                .Where(lineup => selectedAccountIds.Contains(lineup.AccountId)
                    && lineup.Registration.TournamentId == tournamentId
                    && (lineup.Registration.Status == "PENDING"
                        || lineup.Registration.Status == "APPROVED"
                        || lineup.Registration.Status == "WAITLISTED"))
                .Select(lineup => lineup.AccountId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            if (playersAlreadyRegistered.Length > 0)
            {
                return Result<RegistrationResponse>.FieldFailure(
                    nameof(request.Lineup),
                    $"Thành viên {playersAlreadyRegistered[0]} đã có mặt trong đội hình đăng ký khác của giải.");
            }
        }

        if (request.SaveAsDraft)
        {
            var draft = existingDraft ?? new Registration
            {
                TournamentId = tournamentId,
                TeamId = team.TeamId,
                SubmittedByAccountId = accountId,
                FinalPlacement = "NONE"
            };
            draft.Status = "DRAFT";
            draft.RulebookVersionAccepted = null;
            draft.ConsentAt = null;
            draft.WaitlistPosition = null;
            draft.WaitlistExpiresAt = null;
            draft.PaymentStatus = "UNPAID";
            draft.SubmittedAt = null;
            draft.ReviewNote = null;
            draft.ReviewedAt = null;
            draft.RegistrationLineups.Clear();
            foreach (var member in lineupValidation.Lineup)
            {
                draft.RegistrationLineups.Add(new RegistrationLineup
                {
                    AccountId = member.AccountId,
                    Slot = member.Slot,
                    PlayerCredentialId = member.CredentialId,
                    InGameNameSnapshot = member.InGameName
                });
            }
            if (existingDraft is null)
            {
                Add(draft);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success(
                await GetRegistrationResponseAsync(draft.RegistrationId, cancellationToken),
                "Đã lưu bản nháp riêng cho đội trưởng; bản nháp chưa được gửi cho ban tổ chức.");
        }

        var occupied = await CountOccupiedSlotsAsync(tournamentId, now, cancellationToken);
        var isWaitlisted = occupied >= tournament.MaxTeams;
        StoredPrivateFile? storedPaymentProof = null;
        if (paymentProof is not null)
        {
            try
            {
                storedPaymentProof = await _fileStorage.SaveAsync(paymentProof, "payment-proofs", cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                return Result<RegistrationResponse>.FieldFailure(nameof(paymentProof), exception.Message);
            }
        }
        var registration = existingDraft ?? new Registration
        {
            TournamentId = tournamentId,
            TeamId = team.TeamId,
            SubmittedByAccountId = accountId,
            FinalPlacement = "NONE"
        };
        registration.RulebookVersionAccepted = tournament.RulebookVersion;
        registration.ConsentAt = now;
        registration.Status = isWaitlisted ? "WAITLISTED" : "PENDING";
        registration.WaitlistPosition = isWaitlisted
            ? (await Query<Registration>()
                .Where(item => item.TournamentId == tournamentId && item.Status == "WAITLISTED")
                .Select(item => (int?)item.WaitlistPosition)
                .MaxAsync(cancellationToken) ?? 0) + 1
            : null;
        registration.WaitlistExpiresAt = null;
        registration.PaymentStatus = storedPaymentProof is null ? "UNPAID" : "PROOF_SUBMITTED";
        registration.ReviewNote = null;
        registration.ReviewedAt = null;
        registration.SubmittedAt = now;
        registration.RegistrationLineups.Clear();

        foreach (var member in lineupValidation.Lineup)
        {
            registration.RegistrationLineups.Add(new RegistrationLineup
            {
                AccountId = member.AccountId,
                Slot = member.Slot,
                PlayerCredentialId = member.CredentialId,
                InGameNameSnapshot = member.InGameName
            });
        }
        if (storedPaymentProof is not null)
        {
            registration.PaymentRecords.Add(new PaymentRecord
            {
                RecordKind = "PROOF",
                FileUrl = storedPaymentProof.RelativePath,
                DeclaredAmount = declaredAmount,
                IsCurrent = true,
                RecordedByAccountId = accountId,
                RecordedAt = now
            });
        }

        if (existingDraft is null)
        {
            Add(registration);
        }
        await AddNotificationAsync(
            tournament.OrganizerAccountId,
            "Đơn đăng ký mới",
            $"{team.Name} đã gửi đơn đăng ký cho giải {tournament.Name}.",
            $"/registration/review.html?tournamentId={tournamentId}",
            cancellationToken);
        await AddNotificationAsync(
            accountId,
            isWaitlisted ? "Đã vào danh sách chờ" : "Đã gửi đơn đăng ký",
            isWaitlisted
                ? $"Đội {team.Name} đang ở vị trí chờ {registration.WaitlistPosition}."
                : $"Đơn đăng ký của đội {team.Name} đang chờ xét duyệt.",
            "/registration/mine.html",
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (storedPaymentProof is not null)
            {
                await _fileStorage.DeleteAsync(storedPaymentProof.RelativePath);
            }
            throw;
        }
        return Result.Success(
            await GetRegistrationResponseAsync(registration.RegistrationId, cancellationToken),
            isWaitlisted ? "Đội đã được thêm vào danh sách chờ." : "Đã gửi đơn đăng ký, đang chờ ban tổ chức xét duyệt.");
    }

    public async Task<Result<IReadOnlyList<RegistrationResponse>>> GetMineAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var registrations = await FullRegistrationQuery()
            .Where(item => item.Team.CaptainAccountId == accountId)
            .OrderByDescending(item => item.SubmittedAt)
            .ToListAsync(cancellationToken);
        var responses = new List<RegistrationResponse>(registrations.Count);
        foreach (var registration in registrations)
        {
            responses.Add(await MapRegistrationAsync(registration, false, cancellationToken));
        }

        return Result.Success<IReadOnlyList<RegistrationResponse>>(responses);
    }

    public async Task<Result<RegistrationResponse>> GetAsync(
        int registrationId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var registration = await FullRegistrationQuery()
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Team.CaptainAccountId != accountId
            && registration.Tournament.OrganizerAccountId != accountId)
        {
            return Result.Failure<RegistrationResponse>("Bạn không có quyền xem đơn đăng ký này.", StatusCodes.Status403Forbidden);
        }
        if (registration.Status == "DRAFT" && registration.Team.CaptainAccountId != accountId)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        return Result.Success(await MapRegistrationAsync(
            registration,
            registration.Tournament.OrganizerAccountId == accountId,
            cancellationToken));
    }

    public async Task<Result<RegistrationListResponse>> GetTournamentRegistrationsAsync(
        int tournamentId,
        int accountId,
        string? status,
        string? search,
        CancellationToken cancellationToken)
    {
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationListResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }

        var query = FullRegistrationQuery()
            .Where(item => item.TournamentId == tournamentId && item.Status != "DRAFT");
        if (!string.IsNullOrWhiteSpace(status) && status.Trim().ToUpperInvariant() != "ALL")
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            if (!RegistrationStatuses.Contains(normalizedStatus))
            {
                return Result<RegistrationListResponse>.FieldFailure(nameof(status), "Trạng thái lọc không hợp lệ.");
            }
            query = query.Where(item => item.Status == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Team.Name.Contains(term) || item.Team.Tag.Contains(term));
        }

        var registrations = await query
            .OrderBy(item => item.Status == "WAITLISTED" ? item.WaitlistPosition : int.MaxValue)
            .ThenBy(item => item.SubmittedAt)
            .ToListAsync(cancellationToken);
        var responses = new List<RegistrationResponse>(registrations.Count);
        foreach (var registration in registrations)
        {
            responses.Add(await MapRegistrationAsync(registration, true, cancellationToken));
        }

        var occupied = await CountOccupiedSlotsAsync(tournamentId, DateTime.UtcNow, cancellationToken);
        var waitlistCount = await Query<Registration>().CountAsync(
            item => item.TournamentId == tournamentId && item.Status == "WAITLISTED",
            cancellationToken);
        return Result.Success(new RegistrationListResponse(
            tournament.Name,
            responses,
            tournament.MaxTeams,
            occupied,
            Math.Max(0, tournament.MaxTeams - occupied),
            waitlistCount,
            ParseTieBreakerOrder(tournament.TieBreakerOrder)));
    }

    public async Task<Result<RegistrationReviewResponse>> GetReviewAsync(
        int tournamentId,
        int registrationId,
        int accountId,
        CancellationToken cancellationToken)
    {
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationReviewResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }

        var registration = await FullRegistrationQuery()
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId && item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationReviewResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Status == "DRAFT")
        {
            return Result.Failure<RegistrationReviewResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }

        var discipline = await Query<Penalty>().AsNoTracking()
            .Where(item => item.TeamId == registration.TeamId)
            .OrderByDescending(item => item.IssuedAt)
            .Select(item => new TeamDisciplineResponse(
                item.PenaltyId,
                item.Tournament.Name,
                item.Severity,
                item.Points,
                item.Reason,
                item.Status,
                item.IssuedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new RegistrationReviewResponse(
            await MapRegistrationAsync(registration, true, cancellationToken),
            discipline));
    }

    public async Task<Result<PaymentRecordResponse>> UploadPaymentProofAsync(
        int registrationId,
        int accountId,
        IFormFile file,
        decimal? declaredAmount,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var registration = await Query<Registration>().Include(item => item.Tournament).Include(item => item.Team)
            .Include(item => item.PaymentRecords)
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<PaymentRecordResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Team.CaptainAccountId != accountId)
        {
            return Result.Failure<PaymentRecordResponse>("Chỉ đội trưởng mới được tải biên lai.", StatusCodes.Status403Forbidden);
        }
        if (registration.Tournament.EntryFee <= 0 || !registration.Tournament.RequirePaymentProof)
        {
            return Result.Failure<PaymentRecordResponse>("Giải này không yêu cầu tải biên lai lệ phí.");
        }
        if (registration.Status is "REJECTED" or "WITHDRAWN" or "EXPIRED" or "DISQUALIFIED"
            || registration.PaymentStatus == "REFUNDED")
        {
            return Result.Failure<PaymentRecordResponse>("Không thể cập nhật lệ phí cho đơn này.", StatusCodes.Status409Conflict);
        }
        if (declaredAmount < 0)
        {
            return Result<PaymentRecordResponse>.FieldFailure(nameof(declaredAmount), "Số tiền khai báo không được âm.");
        }

        StoredPrivateFile stored;
        try
        {
            stored = await _fileStorage.SaveAsync(file, "payment-proofs", cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Result<PaymentRecordResponse>.FieldFailure(nameof(file), exception.Message);
        }

        foreach (var oldProof in registration.PaymentRecords.Where(item => item.RecordKind == "PROOF" && item.IsCurrent))
        {
            oldProof.IsCurrent = false;
        }
        var record = new PaymentRecord
        {
            RegistrationId = registrationId,
            RecordKind = "PROOF",
            FileUrl = stored.RelativePath,
            DeclaredAmount = declaredAmount,
            IsCurrent = true,
            RecordedByAccountId = accountId,
            RecordedAt = DateTime.UtcNow
        };
        registration.PaymentRecords.Add(record);
        registration.PaymentStatus = "PROOF_SUBMITTED";
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (stored is not null)
            {
                await _fileStorage.DeleteAsync(stored.RelativePath);
            }
            throw;
        }

        return Result.Success(ToPaymentRecordResponse(record), "Đã lưu biên lai. Trạng thái chờ ban tổ chức đối soát.");
    }

    public async Task<Result<RegistrationResponse>> ReviewAsync(
        int tournamentId,
        int registrationId,
        int accountId,
        ReviewRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }
        var registration = await Query<Registration>().Include(item => item.Team)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId && item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Status != "PENDING" || registration.WaitlistExpiresAt is not null)
        {
            return Result.Failure<RegistrationResponse>("Chỉ đơn đang chờ xét duyệt mới có thể được xử lý.", StatusCodes.Status409Conflict);
        }

        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("APPROVE" or "REJECT"))
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Decision), "Quyết định chỉ được là APPROVE hoặc REJECT.");
        }
        var reason = NormalizeOptional(request.Reason);
        if (decision == "REJECT" && reason is null)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Reason), "Từ chối đơn phải ghi rõ lý do.");
        }

        var previousStatus = registration.Status;
        registration.Status = decision == "APPROVE" ? "APPROVED" : "REJECTED";
        registration.ReviewNote = reason;
        registration.ReviewedAt = DateTime.UtcNow;
        Add(new AuditLog
        {
            ActorAccountId = accountId,
            TournamentId = tournamentId,
            ActionType = decision,
            ObjectType = "REGISTRATION",
            ObjectId = registrationId,
            BeforeValue = previousStatus,
            AfterValue = registration.Status,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });
        await AddNotificationAsync(
            registration.Team.CaptainAccountId,
            decision == "APPROVE" ? "Đơn đăng ký được duyệt" : "Đơn đăng ký bị từ chối",
            decision == "APPROVE"
                ? $"Đơn đăng ký của đội {registration.Team.Name} đã được duyệt."
                : $"Đơn đăng ký của đội {registration.Team.Name} bị từ chối: {reason}",
            "/registration/mine.html",
            cancellationToken);

        if (decision == "REJECT")
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await PromoteAvailableSlotsAsync(tournament, null, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(
            await GetRegistrationResponseAsync(registrationId, cancellationToken),
            decision == "APPROVE" ? "Đã duyệt đơn đăng ký." : "Đã từ chối đơn và lưu lý do.");
    }

    public async Task<Result<RegistrationResponse>> VerifyPaymentAsync(
        int tournamentId,
        int registrationId,
        int accountId,
        VerifyRegistrationPaymentRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }
        var registration = await Query<Registration>().Include(item => item.Team)
            .Include(item => item.PaymentRecords)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId && item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.PaymentStatus == "REFUNDED")
        {
            return Result.Failure<RegistrationResponse>("Lệ phí đã được ghi nhận hoàn.", StatusCodes.Status409Conflict);
        }
        if (tournament.EntryFee <= 0)
        {
            return Result.Failure<RegistrationResponse>("Không thể đối soát thanh toán cho giải không thu lệ phí.", StatusCodes.Status409Conflict);
        }

        var status = request.Status.Trim().ToUpperInvariant();
        if (status is not ("UNPAID" or "CONFIRMED"))
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Status), "Trạng thái chỉ được là UNPAID hoặc CONFIRMED.");
        }
        var note = NormalizeOptional(request.Note);
        var currentProof = registration.PaymentRecords
            .Where(item => item.RecordKind == "PROOF" && item.IsCurrent)
            .OrderByDescending(item => item.RecordedAt)
            .FirstOrDefault();
        if (status == "CONFIRMED" && currentProof?.FileUrl is null && note is null)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Note), "Cần ghi chú lý do khi xác nhận thanh toán không có biên lai.");
        }
        if (status == "UNPAID" && currentProof is not null && note is null)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Note), "Ghi rõ lý do không chấp nhận biên lai.");
        }

        var previousStatus = registration.PaymentStatus;
        registration.PaymentStatus = status;
        if (status == "UNPAID" && currentProof is not null)
        {
            currentProof.IsCurrent = false;
        }
        else if (status == "CONFIRMED" && currentProof is null)
        {
            registration.PaymentRecords.Add(new PaymentRecord
            {
                RegistrationId = registrationId,
                RecordKind = "PROOF",
                FileUrl = null,
                DeclaredAmount = tournament.EntryFee,
                Note = note,
                IsCurrent = true,
                RecordedByAccountId = accountId,
                RecordedAt = DateTime.UtcNow
            });
        }
        else if (currentProof is not null && note is not null)
        {
            currentProof.Note = note;
        }

        Add(new AuditLog
        {
            ActorAccountId = accountId,
            TournamentId = tournamentId,
            ActionType = "VERIFY_PAYMENT",
            ObjectType = "REGISTRATION",
            ObjectId = registrationId,
            BeforeValue = previousStatus,
            AfterValue = status,
            Reason = note,
            CreatedAt = DateTime.UtcNow
        });
        await AddNotificationAsync(
            registration.Team.CaptainAccountId,
            "Cập nhật trạng thái lệ phí",
            $"Lệ phí đơn đăng ký của đội {registration.Team.Name}: {PaymentStatusLabel(status)}.",
            "/registration/mine.html",
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(
            await GetRegistrationResponseAsync(registrationId, cancellationToken),
            "Đã cập nhật trạng thái thanh toán độc lập với kết quả xét duyệt.");
    }

    public async Task<Result<RegistrationResponse>> RecordRefundAsync(
        int tournamentId,
        int registrationId,
        int accountId,
        RefundRegistrationRequest request,
        IFormFile? evidence,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }
        var registration = await Query<Registration>().Include(item => item.Team)
            .FirstOrDefaultAsync(item => item.TournamentId == tournamentId && item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Status is not ("REJECTED" or "WITHDRAWN")
            && tournament.Status != "CANCELLED")
        {
            return Result.Failure<RegistrationResponse>("Chỉ đơn bị từ chối/rút hoặc giải bị hủy mới được ghi nhận hoàn tiền.");
        }
        if (tournament.EntryFee <= 0)
        {
            return Result.Failure<RegistrationResponse>("Không thể ghi nhận hoàn tiền cho giải không thu lệ phí.", StatusCodes.Status409Conflict);
        }
        if (registration.PaymentStatus != "CONFIRMED")
        {
            return Result.Failure<RegistrationResponse>("Chỉ ghi nhận hoàn cho khoản phí đã được xác nhận.", StatusCodes.Status409Conflict);
        }
        if (request.Amount is null || request.Amount <= 0)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Amount), "Nhập số tiền hoàn lớn hơn 0.");
        }
        if (request.Amount > tournament.EntryFee)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.Amount), "Số tiền hoàn không được vượt quá lệ phí đăng ký.");
        }
        if (request.RefundDate is null || request.RefundDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.RefundDate), "Ngày hoàn phải được nhập và không được ở tương lai.");
        }
        var refundMethod = NormalizeOptional(request.RefundMethod);
        if (refundMethod is null)
        {
            return Result<RegistrationResponse>.FieldFailure(nameof(request.RefundMethod), "Nhập phương thức hoàn lệ phí.");
        }
        var note = NormalizeOptional(request.Note);
        var refundRecordNote = $"Ngày hoàn: {request.RefundDate:dd/MM/yyyy}; Phương thức: {refundMethod}"
            + (note is null ? string.Empty : $"; Ghi chú: {note}");

        StoredPrivateFile? stored = null;
        if (evidence is not null)
        {
            try
            {
                stored = await _fileStorage.SaveAsync(evidence, "refund-evidence", cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                return Result<RegistrationResponse>.FieldFailure(nameof(evidence), exception.Message);
            }
        }

        registration.PaymentStatus = "REFUNDED";
        registration.PaymentRecords.Add(new PaymentRecord
        {
            RegistrationId = registrationId,
            RecordKind = "REFUND",
            FileUrl = stored?.RelativePath,
            DeclaredAmount = request.Amount,
            Note = refundRecordNote,
            IsCurrent = true,
            RecordedByAccountId = accountId,
            RecordedAt = DateTime.UtcNow
        });
        Add(new AuditLog
        {
            ActorAccountId = accountId,
            TournamentId = tournamentId,
            ActionType = "REFUND_RECORDED",
            ObjectType = "REGISTRATION",
            ObjectId = registrationId,
            BeforeValue = "CONFIRMED",
            AfterValue = "REFUNDED",
            Reason = refundRecordNote,
            CreatedAt = DateTime.UtcNow
        });
        await AddNotificationAsync(
            registration.Team.CaptainAccountId,
            "Đã ghi nhận hoàn lệ phí",
            $"Ban tổ chức đã ghi nhận hoàn lệ phí cho đội {registration.Team.Name}. ETOMS không thực hiện giao dịch.",
            "/registration/mine.html",
            cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (stored is not null)
            {
                await _fileStorage.DeleteAsync(stored.RelativePath);
            }
            throw;
        }

        return Result.Success(
            await GetRegistrationResponseAsync(registrationId, cancellationToken),
            "Đã lưu ghi nhận hoàn tiền; giao dịch được thực hiện bên ngoài ETOMS.");
    }

    public async Task<Result<RegistrationResponse>> WithdrawAsync(
        int registrationId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var registration = await Query<Registration>().Include(item => item.Team)
            .Include(item => item.Tournament)
                .ThenInclude(item => item.Game)
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Team.CaptainAccountId != accountId)
        {
            return Result.Failure<RegistrationResponse>("Chỉ đội trưởng mới được rút đơn.", StatusCodes.Status403Forbidden);
        }
        if (registration.Tournament.BracketLockedAt is not null)
        {
            return Result.Failure<RegistrationResponse>("Không thể rút đơn sau khi khóa nhánh đấu.", StatusCodes.Status409Conflict);
        }
        if (registration.Status is not ("DRAFT" or "PENDING" or "WAITLISTED" or "APPROVED"))
        {
            return Result.Failure<RegistrationResponse>("Trạng thái đơn hiện tại không thể rút.", StatusCodes.Status409Conflict);
        }

        registration.Status = "WITHDRAWN";
        registration.WaitlistPosition = null;
        registration.WaitlistExpiresAt = null;
        registration.ReviewNote = null;
        await AddNotificationAsync(
            registration.Tournament.OrganizerAccountId,
            "Đội đã rút đăng ký",
            $"Đội {registration.Team.Name} đã rút đơn khỏi giải {registration.Tournament.Name}.",
            $"/registration/review.html?tournamentId={registration.TournamentId}",
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PromoteAvailableSlotsAsync(registration.Tournament, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(
            await GetRegistrationResponseAsync(registrationId, cancellationToken),
            "Đã rút đơn. Suất trống sẽ được cấp cho đội trong danh sách chờ nếu có.");
    }

    public async Task<Result<RegistrationResponse>> AcceptWaitlistOfferAsync(
        int registrationId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var registration = await Query<Registration>().Include(item => item.Team)
            .Include(item => item.Tournament)
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<RegistrationResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Team.CaptainAccountId != accountId)
        {
            return Result.Failure<RegistrationResponse>("Chỉ đội trưởng mới được xác nhận suất chờ.", StatusCodes.Status403Forbidden);
        }
        if (registration.Status != "PENDING" || registration.WaitlistExpiresAt is null)
        {
            return Result.Failure<RegistrationResponse>("Đơn này không có lời mời suất chờ đang cần xác nhận.", StatusCodes.Status409Conflict);
        }
        if (registration.WaitlistExpiresAt <= DateTime.UtcNow
            || !IsRegistrationWindowOpen(registration.Tournament, DateTime.UtcNow))
        {
            return Result.Failure<RegistrationResponse>("Thời hạn xác nhận đã hết hoặc giải đã đóng đăng ký.", StatusCodes.Status409Conflict);
        }

        registration.WaitlistExpiresAt = null;
        await AddNotificationAsync(
            registration.Tournament.OrganizerAccountId,
            "Đội xác nhận suất chờ",
            $"Đội {registration.Team.Name} đã xác nhận lời mời và đang chờ xét duyệt.",
            $"/registration/review.html?tournamentId={registration.TournamentId}",
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(
            await GetRegistrationResponseAsync(registrationId, cancellationToken),
            "Đã xác nhận suất chờ. Đơn đang chờ ban tổ chức xét duyệt.");
    }

    public async Task<Result<WaitlistPromotionResponse>> PromoteWaitlistAsync(
        int tournamentId,
        int? registrationId,
        int accountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<WaitlistPromotionResponse>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }
        var now = DateTime.UtcNow;
        if (!IsRegistrationWindowOpen(tournament, now))
        {
            return Result.Failure<WaitlistPromotionResponse>("Không thể đôn đội khi giải đã đóng đăng ký.", StatusCodes.Status409Conflict);
        }
        var occupied = await CountOccupiedSlotsAsync(tournamentId, now, cancellationToken);
        if (occupied >= tournament.MaxTeams)
        {
            return Result.Failure<WaitlistPromotionResponse>("Giải hiện không còn suất trống.", StatusCodes.Status409Conflict);
        }

        var candidateQuery = Query<Registration>().Include(item => item.Team)
            .Where(item => item.TournamentId == tournamentId && item.Status == "WAITLISTED");
        var firstCandidate = await candidateQuery
            .OrderBy(item => item.WaitlistPosition)
            .ThenBy(item => item.SubmittedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var candidate = registrationId.HasValue
            ? await candidateQuery.FirstOrDefaultAsync(item => item.RegistrationId == registrationId.Value, cancellationToken)
            : firstCandidate;
        if (candidate is null)
        {
            return Result.Failure<WaitlistPromotionResponse>("Không còn đội trong danh sách chờ.", StatusCodes.Status404NotFound);
        }
        if (!registrationId.HasValue && firstCandidate?.RegistrationId != candidate.RegistrationId)
        {
            return Result.Failure<WaitlistPromotionResponse>(
                "Chỉ có thể đôn theo đúng thứ tự FIFO của danh sách chờ.",
                StatusCodes.Status409Conflict);
        }

        var eligible = await IsCurrentLineupEligibleAsync(candidate, tournament, cancellationToken);
        if (!eligible)
        {
            return Result.Failure<WaitlistPromotionResponse>("Đội hình không còn đáp ứng luật roster/IGN/định danh; hãy cập nhật đội hình trước khi đôn.", StatusCodes.Status409Conflict);
        }
        var formerWaitlistPosition = candidate.WaitlistPosition;
        await PromoteRegistrationAsync(candidate, tournament, accountId, now, cancellationToken);
        await ReindexWaitlistAsync(tournamentId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(new WaitlistPromotionResponse(
            candidate.RegistrationId,
            tournamentId,
            formerWaitlistPosition,
            candidate.WaitlistExpiresAt),
            "Đã gửi lời mời suất chờ; đội trưởng có 24 giờ để xác nhận.");
    }

    public async Task ProcessWaitlistsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tournamentIds = await Query<Registration>().AsNoTracking()
            .Where(item => item.Status == "WAITLISTED" || item.WaitlistExpiresAt != null)
            .Select(item => item.TournamentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var tournamentId in tournamentIds)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var tournament = await Query<Tournament>().Include(item => item.Game).FirstOrDefaultAsync(
                item => item.TournamentId == tournamentId,
                cancellationToken);
            if (tournament is null)
            {
                continue;
            }

            var closed = tournament.Status != "PUBLISHED"
                || tournament.BracketLockedAt is not null
                || tournament.RegCloseAt <= now;
            var expiredOffers = await Query<Registration>().Include(item => item.Team)
                .Where(item => item.TournamentId == tournamentId
                    && item.Status == "PENDING"
                    && item.WaitlistExpiresAt != null
                    && (closed || item.WaitlistExpiresAt <= now))
                .ToListAsync(cancellationToken);
            foreach (var expired in expiredOffers)
            {
                expired.Status = "EXPIRED";
                expired.WaitlistExpiresAt = null;
                expired.ReviewNote = "Hết hạn xác nhận suất được đôn từ danh sách chờ.";
                await AddNotificationAsync(
                    expired.Team.CaptainAccountId,
                    "Hết hạn xác nhận suất chờ",
                    $"Đội {expired.Team.Name} đã hết thời hạn xác nhận suất chờ.",
                    "/registration/mine.html",
                    cancellationToken);
            }

            if (closed)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                var waiting = await Query<Registration>().Include(item => item.Team)
                    .Where(item => item.TournamentId == tournamentId
                        && (item.Status == "WAITLISTED"
                            || (item.Status == "PENDING" && item.WaitlistExpiresAt != null)))
                    .ToListAsync(cancellationToken);
                foreach (var expired in waiting)
                {
                    expired.Status = "EXPIRED";
                    expired.WaitlistPosition = null;
                    expired.WaitlistExpiresAt = null;
                    expired.ReviewNote = "Đã đóng đăng ký khi đội còn ở danh sách chờ.";
                    await AddNotificationAsync(
                        expired.Team.CaptainAccountId,
                        "Danh sách chờ đã đóng",
                        $"Đăng ký giải {tournament.Name} đã đóng; đội {expired.Team.Name} không còn trong danh sách chờ.",
                        "/registration/mine.html",
                        cancellationToken);
                }
            }
            else
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await PromoteAvailableSlotsAsync(tournament, null, cancellationToken);
            }

            await ReindexWaitlistAsync(tournamentId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> SetTieBreakerOrderAsync(
        int tournamentId,
        int accountId,
        IReadOnlyList<string> order,
        CancellationToken cancellationToken)
    {
        var tournament = await GetOrganizerTournamentAsync(tournamentId, accountId, cancellationToken);
        if (tournament is null)
        {
            return Result.Failure<IReadOnlyList<string>>("Không tìm thấy giải hoặc bạn không có quyền quản lý giải này.", StatusCodes.Status404NotFound);
        }
        if (tournament.BracketLockedAt is not null)
        {
            return Result.Failure<IReadOnlyList<string>>("Không thể đổi tie-breaker sau khi khóa nhánh đấu.", StatusCodes.Status409Conflict);
        }

        if (order is null || order.Any(string.IsNullOrWhiteSpace))
        {
            return Result<IReadOnlyList<string>>.FieldFailure(
                "order",
                "Thứ tự phải chứa đúng một lần mỗi tiêu chí: đối đầu, hiệu số ván, tổng ván thắng và bốc thăm.");
        }
        var normalized = order.Select(item => item.Trim().ToUpperInvariant()).ToArray();
        if (normalized.Length != TieBreakerCriteria.DefaultOrder.Length
            || normalized.Distinct(StringComparer.Ordinal).Count() != TieBreakerCriteria.DefaultOrder.Length
            || !normalized.ToHashSet(StringComparer.Ordinal).SetEquals(TieBreakerCriteria.DefaultOrder))
        {
            return Result<IReadOnlyList<string>>.FieldFailure(
                "order",
                "Thứ tự phải chứa đúng một lần mỗi tiêu chí: đối đầu, hiệu số ván, tổng ván thắng và bốc thăm.");
        }

        tournament.TieBreakerOrder = string.Join(',', normalized);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<string>>(normalized, "Đã lưu thứ tự tie-breaker cho giải.");
    }

    public async Task<Result<PrivateRegistrationFileResponse>> GetIdentityDocumentAsync(
        int registrationId,
        int playerAccountId,
        int credentialId,
        int requesterAccountId,
        CancellationToken cancellationToken)
    {
        var registration = await Query<Registration>().AsNoTracking()
            .Include(item => item.Tournament)
            .FirstOrDefaultAsync(item => item.RegistrationId == registrationId, cancellationToken);
        if (registration is null)
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Không tìm thấy đơn đăng ký.", StatusCodes.Status404NotFound);
        }
        if (registration.Tournament.OrganizerAccountId != requesterAccountId)
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Bạn không có quyền xem giấy tờ của giải này.", StatusCodes.Status403Forbidden);
        }
        var playerIsInLineup = await Query<RegistrationLineup>().AnyAsync(
            item => item.RegistrationId == registrationId && item.AccountId == playerAccountId,
            cancellationToken);
        if (!playerIsInLineup)
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Người chơi không thuộc đội hình đăng ký này.", StatusCodes.Status404NotFound);
        }
        var document = await Query<PlayerCredential>().AsNoTracking()
            .Where(item => item.PlayerCredentialId == credentialId
                && item.AccountId == playerAccountId
                && item.CredentialType == IdentityDocumentType
                && item.FileUrl != null)
            .Select(item => item.FileUrl)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(document))
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Không tìm thấy giấy tờ định danh.", StatusCodes.Status404NotFound);
        }
        return Result.Success(new PrivateRegistrationFileResponse(
            document,
            "identity-document" + Path.GetExtension(document)));
    }

    public async Task<Result<PrivateRegistrationFileResponse>> GetPaymentRecordFileAsync(
        int registrationId,
        int paymentRecordId,
        int requesterAccountId,
        CancellationToken cancellationToken)
    {
        var record = await Query<PaymentRecord>().AsNoTracking()
            .Where(item => item.RegistrationId == registrationId
                && item.PaymentRecordId == paymentRecordId
                && item.FileUrl != null)
            .Select(item => new
            {
                item.FileUrl,
                item.RecordKind,
                item.Registration.Team.CaptainAccountId,
                item.Registration.Tournament.OrganizerAccountId
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null || string.IsNullOrWhiteSpace(record.FileUrl))
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Không tìm thấy tệp minh chứng.", StatusCodes.Status404NotFound);
        }
        if (record.CaptainAccountId != requesterAccountId && record.OrganizerAccountId != requesterAccountId)
        {
            return Result.Failure<PrivateRegistrationFileResponse>("Bạn không có quyền xem minh chứng thanh toán này.", StatusCodes.Status403Forbidden);
        }
        return Result.Success(new PrivateRegistrationFileResponse(
            record.FileUrl,
            (record.RecordKind == "REFUND" ? "refund-evidence" : "payment-proof")
                + Path.GetExtension(record.FileUrl)));
    }

    private async Task<LineupValidation> ValidateLineupAsync(
        Tournament tournament,
        int teamId,
        IReadOnlyList<RegistrationLineupRequest> requestedLineup,
        CancellationToken cancellationToken)
    {
        if (requestedLineup.Count == 0
            || requestedLineup.Select(item => item.AccountId).Distinct().Count() != requestedLineup.Count)
        {
            return LineupValidation.Failure("Đội hình phải có thành viên và không được trùng người chơi.");
        }
        var normalized = requestedLineup.Select(item => new
        {
            item.AccountId,
            Slot = item.Slot.Trim().ToUpperInvariant()
        }).ToArray();
        if (normalized.Any(item => item.Slot is not ("STARTER" or "SUBSTITUTE")))
        {
            return LineupValidation.Failure("Vị trí chỉ được là STARTER hoặc SUBSTITUTE.");
        }
        var starters = normalized.Count(item => item.Slot == "STARTER");
        var substitutes = normalized.Length - starters;
        if (tournament.StarterCount < tournament.Game.MinStarters
            || tournament.StarterCount > tournament.Game.MaxStarters
            || starters != tournament.StarterCount)
        {
            return LineupValidation.Failure($"Cần chọn chính xác {tournament.StarterCount} tuyển thủ chính thức theo luật giải.");
        }
        if (substitutes > Math.Min(tournament.MaxSubstitutes, tournament.Game.MaxSubstitutes))
        {
            return LineupValidation.Failure("Số tuyển thủ dự bị vượt quá giới hạn của giải hoặc tựa game.");
        }

        var accountIds = normalized.Select(item => item.AccountId).ToArray();
        var activeRoster = await Query<TeamMember>().AsNoTracking()
            .Where(item => item.TeamId == teamId && item.Status == "ACTIVE" && accountIds.Contains(item.AccountId))
            .Select(item => new { item.AccountId, item.Account.Status })
            .ToListAsync(cancellationToken);
        if (activeRoster.Select(item => item.AccountId).Distinct().Count() != accountIds.Length
            || activeRoster.Any(item => item.Status != "ACTIVE"))
        {
            return LineupValidation.Failure("Mọi thành viên đăng ký phải thuộc roster đang hoạt động của đội.");
        }

        var credentials = await Query<PlayerCredential>().AsNoTracking()
            .Where(item => accountIds.Contains(item.AccountId)
                && ((item.CredentialType == InGameNameType && item.GameId == tournament.GameId)
                    || item.CredentialType == IdentityDocumentType))
            .Select(item => new { item.PlayerCredentialId, item.AccountId, item.CredentialType, item.Value, item.FileUrl })
            .ToListAsync(cancellationToken);
        foreach (var member in normalized)
        {
            var ign = credentials.FirstOrDefault(item =>
                item.AccountId == member.AccountId
                && item.CredentialType == InGameNameType
                && !string.IsNullOrWhiteSpace(item.Value));
            if (tournament.RequireInGameName && ign is null)
            {
                return LineupValidation.Failure($"Thành viên {member.AccountId} chưa khai báo IGN cho tựa game này.");
            }
            if (tournament.RequireIdentityDocument && !credentials.Any(item =>
                item.AccountId == member.AccountId
                && item.CredentialType == IdentityDocumentType
                && !string.IsNullOrWhiteSpace(item.FileUrl)))
            {
                return LineupValidation.Failure($"Thành viên {member.AccountId} còn thiếu giấy tờ định danh bắt buộc.");
            }
        }

        return LineupValidation.Success(normalized.Select(member =>
        {
            var credential = credentials.FirstOrDefault(item =>
                item.AccountId == member.AccountId
                && item.CredentialType == InGameNameType
                && !string.IsNullOrWhiteSpace(item.Value));
            return new ValidatedLineupMember(
                member.AccountId,
                member.Slot,
                credential?.PlayerCredentialId,
                credential?.Value);
        }).ToArray());
    }

    private async Task<LineupValidation> ValidateDraftLineupAsync(
        Tournament tournament,
        int teamId,
        IReadOnlyList<RegistrationLineupRequest> requestedLineup,
        CancellationToken cancellationToken)
    {
        if (requestedLineup.Count == 0)
        {
            return LineupValidation.Success([]);
        }
        if (requestedLineup.Any(item => item is null)
            || requestedLineup.Select(item => item.AccountId).Distinct().Count() != requestedLineup.Count)
        {
            return LineupValidation.Failure("Đội hình nháp không được chứa thành viên trùng nhau.");
        }

        var normalized = requestedLineup.Select(item => new
        {
            item.AccountId,
            Slot = item.Slot?.Trim().ToUpperInvariant()
        }).ToArray();
        if (normalized.Any(item => item.Slot is not ("STARTER" or "SUBSTITUTE")))
        {
            return LineupValidation.Failure("Vị trí chỉ được là STARTER hoặc SUBSTITUTE.");
        }

        var accountIds = normalized.Select(item => item.AccountId).ToArray();
        var activeRoster = await Query<TeamMember>().AsNoTracking()
            .Where(item => item.TeamId == teamId
                && item.Status == "ACTIVE"
                && item.Account.Status == "ACTIVE"
                && accountIds.Contains(item.AccountId))
            .Select(item => item.AccountId)
            .ToListAsync(cancellationToken);
        if (activeRoster.Distinct().Count() != accountIds.Length)
        {
            return LineupValidation.Failure("Thành viên trong bản nháp phải thuộc roster đang hoạt động của đội.");
        }

        var credentials = await Query<PlayerCredential>().AsNoTracking()
            .Where(item => accountIds.Contains(item.AccountId)
                && item.CredentialType == InGameNameType
                && item.GameId == tournament.GameId
                && !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => new { item.PlayerCredentialId, item.AccountId, item.Value })
            .ToListAsync(cancellationToken);
        return LineupValidation.Success(normalized.Select(member =>
        {
            var credential = credentials.FirstOrDefault(item => item.AccountId == member.AccountId);
            return new ValidatedLineupMember(
                member.AccountId,
                member.Slot!,
                credential?.PlayerCredentialId,
                credential?.Value);
        }).ToArray());
    }

    private async Task<bool> IsCurrentLineupEligibleAsync(
        Registration registration,
        Tournament tournament,
        CancellationToken cancellationToken)
    {
        var lineups = await Query<RegistrationLineup>().AsNoTracking()
            .Where(item => item.RegistrationId == registration.RegistrationId)
            .Select(item => new { item.AccountId, item.Slot })
            .ToListAsync(cancellationToken);
        if (tournament.StarterCount < tournament.Game.MinStarters
            || tournament.StarterCount > tournament.Game.MaxStarters
            || lineups.Count(item => item.Slot == "STARTER") != tournament.StarterCount
            || lineups.Count(item => item.Slot == "SUBSTITUTE")
                > Math.Min(tournament.MaxSubstitutes, tournament.Game.MaxSubstitutes))
        {
            return false;
        }
        if (!await Query<Team>().AnyAsync(
            item => item.TeamId == registration.TeamId && item.Status == "ACTIVE",
            cancellationToken))
        {
            return false;
        }

        var accountIds = lineups.Select(item => item.AccountId).ToArray();
        var activeMembers = await Query<TeamMember>().AsNoTracking()
            .Where(item => item.TeamId == registration.TeamId
                && item.Status == "ACTIVE"
                && accountIds.Contains(item.AccountId))
            .Select(item => new { item.AccountId, AccountStatus = item.Account.Status })
            .ToListAsync(cancellationToken);
        if (activeMembers.Select(item => item.AccountId).Distinct().Count() != accountIds.Length
            || activeMembers.Any(item => item.AccountStatus != "ACTIVE"))
        {
            return false;
        }

        var playerAlreadyRegistered = await Query<RegistrationLineup>().AsNoTracking()
            .AnyAsync(item => item.RegistrationId != registration.RegistrationId
                && accountIds.Contains(item.AccountId)
                && item.Registration.TournamentId == tournament.TournamentId
                && (item.Registration.Status == "PENDING"
                    || item.Registration.Status == "APPROVED"
                    || item.Registration.Status == "WAITLISTED"),
                cancellationToken);
        if (playerAlreadyRegistered)
        {
            return false;
        }
        var credentials = await Query<PlayerCredential>().AsNoTracking()
            .Where(item => accountIds.Contains(item.AccountId)
                && ((item.CredentialType == InGameNameType && item.GameId == tournament.GameId)
                    || (item.CredentialType == IdentityDocumentType && item.FileUrl != null)))
            .Select(item => new { item.AccountId, item.CredentialType, item.Value, item.FileUrl })
            .ToListAsync(cancellationToken);
        return lineups.All(member =>
            (!tournament.RequireInGameName || credentials.Any(item =>
                item.AccountId == member.AccountId
                && item.CredentialType == InGameNameType
                && !string.IsNullOrWhiteSpace(item.Value)))
            && (!tournament.RequireIdentityDocument || credentials.Any(item =>
                item.AccountId == member.AccountId
                && item.CredentialType == IdentityDocumentType
                && !string.IsNullOrWhiteSpace(item.FileUrl))));
    }

    private async Task PromoteAvailableSlotsAsync(
        Tournament tournament,
        int? actorAccountId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (!IsRegistrationWindowOpen(tournament, now))
        {
            return;
        }
        var skippedRegistrationIds = new HashSet<int>();
        while (await CountOccupiedSlotsAsync(tournament.TournamentId, now, cancellationToken) < tournament.MaxTeams)
        {
            var candidate = await Query<Registration>().Include(item => item.Team)
                .Where(item => item.TournamentId == tournament.TournamentId
                    && item.Status == "WAITLISTED"
                    && !skippedRegistrationIds.Contains(item.RegistrationId))
                .OrderBy(item => item.WaitlistPosition)
                .ThenBy(item => item.SubmittedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (candidate is null)
            {
                break;
            }
            if (!await IsCurrentLineupEligibleAsync(candidate, tournament, cancellationToken))
            {
                skippedRegistrationIds.Add(candidate.RegistrationId);
                if (candidate.ReviewNote != "Roster không còn đáp ứng điều kiện đăng ký; đang chờ đội cập nhật.")
                {
                    candidate.ReviewNote = "Roster không còn đáp ứng điều kiện đăng ký; đang chờ đội cập nhật.";
                    await AddNotificationAsync(
                        candidate.Team.CaptainAccountId,
                        "Cần cập nhật roster",
                        $"Đội {candidate.Team.Name} vẫn trong danh sách chờ nhưng roster không còn đáp ứng điều kiện. Hãy cập nhật đội trước khi được đôn.",
                        "/registration/mine.html",
                        cancellationToken);
                }
                continue;
            }

            await PromoteRegistrationAsync(candidate, tournament, actorAccountId, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            skippedRegistrationIds.Clear();
        }
        await ReindexWaitlistAsync(tournament.TournamentId, cancellationToken);
    }

    private async Task PromoteRegistrationAsync(
        Registration registration,
        Tournament tournament,
        int? actorAccountId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var oldPosition = registration.WaitlistPosition;
        registration.Status = "PENDING";
        registration.WaitlistPosition = null;
        registration.WaitlistExpiresAt = now.Add(WaitlistConfirmationWindow);
        registration.ReviewNote = null;
        Add(new AuditLog
        {
            ActorAccountId = actorAccountId,
            TournamentId = tournament.TournamentId,
            ActionType = "PROMOTE_WAITLIST",
            ObjectType = "REGISTRATION",
            ObjectId = registration.RegistrationId,
            BeforeValue = $"WAITLISTED:{oldPosition}",
            AfterValue = $"PENDING:{registration.WaitlistExpiresAt:O}",
            CreatedAt = now
        });
        await AddNotificationAsync(
            registration.Team.CaptainAccountId,
            "Được mời nhận suất giải",
            $"Đội {registration.Team.Name} được đôn từ danh sách chờ. Xác nhận trong 24 giờ để giữ suất.",
            "/registration/mine.html",
            cancellationToken);
        await AddNotificationAsync(
            tournament.OrganizerAccountId,
            "Đã đôn đội từ danh sách chờ",
            $"Đội {registration.Team.Name} có 24 giờ để xác nhận trước khi đơn được xét duyệt.",
            $"/registration/review.html?tournamentId={tournament.TournamentId}",
            cancellationToken);
    }

    private async Task ReindexWaitlistAsync(int tournamentId, CancellationToken cancellationToken)
    {
        var waiting = await Query<Registration>()
            .Where(item => item.TournamentId == tournamentId && item.Status == "WAITLISTED")
            .OrderBy(item => item.WaitlistPosition)
            .ThenBy(item => item.SubmittedAt)
            .ToListAsync(cancellationToken);
        for (var index = 0; index < waiting.Count; index++)
        {
            waiting[index].WaitlistPosition = index + 1;
        }
    }

    private async Task<int> CountOccupiedSlotsAsync(
        int tournamentId,
        DateTime now,
        CancellationToken cancellationToken)
        => await Query<Registration>().CountAsync(item =>
            item.TournamentId == tournamentId
            && (item.Status == "APPROVED"
                || (item.Status == "PENDING"
                    && (item.WaitlistExpiresAt == null || item.WaitlistExpiresAt > now))),
            cancellationToken);

    private async Task<Tournament?> GetOrganizerTournamentAsync(
        int tournamentId,
        int accountId,
        CancellationToken cancellationToken)
        => await Query<Tournament>().Include(item => item.Game).FirstOrDefaultAsync(
            item => item.TournamentId == tournamentId && item.OrganizerAccountId == accountId,
            cancellationToken);

    private IQueryable<Registration> FullRegistrationQuery()
        => Query<Registration>().AsNoTracking()
            .Include(item => item.Tournament)
            .Include(item => item.Team)
            .Include(item => item.RegistrationLineups)
                .ThenInclude(item => item.Account)
            .Include(item => item.PaymentRecords);

    private async Task<RegistrationResponse> GetRegistrationResponseAsync(
        int registrationId,
        CancellationToken cancellationToken)
    {
        var registration = await FullRegistrationQuery()
            .FirstAsync(item => item.RegistrationId == registrationId, cancellationToken);
        return await MapRegistrationAsync(registration, false, cancellationToken);
    }

    private async Task<RegistrationResponse> MapRegistrationAsync(
        Registration registration,
        bool includeIdentityDocumentIds,
        CancellationToken cancellationToken)
    {
        var accountIds = registration.RegistrationLineups.Select(item => item.AccountId).Distinct().ToArray();
        var identityDocuments = includeIdentityDocumentIds
            ? await Query<PlayerCredential>().AsNoTracking()
                .Where(item => accountIds.Contains(item.AccountId)
                    && item.CredentialType == IdentityDocumentType
                    && item.FileUrl != null)
                .Select(item => new { item.AccountId, item.PlayerCredentialId })
                .ToListAsync(cancellationToken)
            : [];
        var lineups = registration.RegistrationLineups
            .OrderBy(item => item.Slot == "STARTER" ? 0 : 1)
            .ThenBy(item => item.Account.FullName)
            .Select(item => new RegistrationLineupResponse(
                item.AccountId,
                item.Account.FullName,
                item.Slot,
                item.InGameNameSnapshot,
                identityDocuments.Where(document => document.AccountId == item.AccountId)
                    .Select(document => document.PlayerCredentialId)
                    .ToArray()))
            .ToArray();
        var aheadCount = registration.Status == "WAITLISTED" && registration.WaitlistPosition.HasValue
            ? Math.Max(0, registration.WaitlistPosition.Value - 1)
            : 0;
        return new RegistrationResponse(
            registration.RegistrationId,
            registration.TournamentId,
            registration.Tournament.Name,
            registration.TeamId,
            registration.Team.Name,
            registration.Team.Tag,
            registration.Tournament.EntryFee,
            registration.Tournament.RequirePaymentProof,
            registration.SubmittedAt,
            registration.Status,
            registration.WaitlistPosition,
            aheadCount,
            registration.WaitlistExpiresAt,
            registration.PaymentStatus,
            registration.RulebookVersionAccepted,
            registration.ConsentAt,
            registration.ReviewNote,
            registration.ReviewedAt,
            registration.PaymentStatus == "CONFIRMED"
                && registration.Tournament.EntryFee > 0
                && (registration.Status is "REJECTED" or "WITHDRAWN"
                    || registration.Tournament.Status == "CANCELLED"),
            lineups,
            registration.PaymentRecords
                .OrderByDescending(item => item.RecordedAt)
                .Select(ToPaymentRecordResponse)
                .ToArray());
    }

    private async Task AddNotificationAsync(
        int accountId,
        string title,
        string body,
        string linkUrl,
        CancellationToken cancellationToken)
    {
        var account = await Query<Account>().FirstOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (account?.NotifyInApp != true)
        {
            return;
        }
        Add(new Notification
        {
            AccountId = accountId,
            NotificationType = "REGISTRATION",
            Title = title,
            Body = body,
            LinkUrl = linkUrl,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
    }

    private static PaymentRecordResponse ToPaymentRecordResponse(PaymentRecord record)
        => new(
            record.PaymentRecordId,
            record.RecordKind,
            record.FileUrl is null ? null : Path.GetFileName(record.FileUrl),
            record.DeclaredAmount,
            record.Note,
            record.RecordedAt,
            record.RecordedByAccountId,
            record.IsCurrent,
            record.FileUrl is null
                ? null
                : $"/api/registrations/{record.RegistrationId}/payments/{record.PaymentRecordId}/file");

    private static bool IsRegistrationWindowOpen(Tournament tournament, DateTime now)
        => tournament.Status == "PUBLISHED"
            && now >= tournament.RegOpenAt
            && now < tournament.RegCloseAt
            && tournament.BracketLockedAt is null;

    private static IReadOnlyList<string> ParseTieBreakerOrder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TieBreakerCriteria.DefaultOrder;
        }
        var parsed = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parsed.Length == TieBreakerCriteria.DefaultOrder.Length
            && parsed.ToHashSet(StringComparer.Ordinal).SetEquals(TieBreakerCriteria.DefaultOrder)
                ? parsed
                : TieBreakerCriteria.DefaultOrder;
    }

    private static string PaymentStatusLabel(string status) => status switch
    {
        "UNPAID" => "Chưa nộp",
        "PROOF_SUBMITTED" => "Chờ đối soát",
        "CONFIRMED" => "Đã xác nhận",
        "REFUNDED" => "Đã hoàn",
        _ => status
    };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static readonly HashSet<string> RegistrationStatuses = new(StringComparer.Ordinal)
    {
        "DRAFT",
        "PENDING",
        "APPROVED",
        "REJECTED",
        "WAITLISTED",
        "WITHDRAWN",
        "DISQUALIFIED",
        "EXPIRED"
    };

    private sealed record ValidatedLineupMember(int AccountId, string Slot, int? CredentialId, string? InGameName);

    private sealed class LineupValidation
    {
        public bool IsSuccess { get; init; }
        public int StatusCode { get; init; }
        public string Message { get; init; } = string.Empty;
        public IReadOnlyList<ValidatedLineupMember> Lineup { get; init; } = [];

        public static LineupValidation Success(IReadOnlyList<ValidatedLineupMember> lineup)
            => new() { IsSuccess = true, Lineup = lineup };

        public static LineupValidation Failure(string message)
            => new() { Message = message, StatusCode = StatusCodes.Status400BadRequest };
    }
}
