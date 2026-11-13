using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class OrganizationService : IOrganizationService
{
    private static readonly HashSet<string> OrganizationTypes = new(StringComparer.Ordinal)
    {
        "UNIVERSITY_CLUB",
        "EVENT_COMPANY",
        "INDIVIDUAL"
    };

    private static readonly IReadOnlyDictionary<string, string[]> RequiredDocumentTypes =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["UNIVERSITY_CLUB"] = ["Xác nhận hoặc quyết định thành lập của trường"],
            ["EVENT_COMPANY"] = ["Giấy chứng nhận đăng ký doanh nghiệp"],
            ["INDIVIDUAL"] = ["Hồ sơ năng lực hoặc minh chứng hoạt động tổ chức giải"]
        };

    private static readonly HashSet<string> MemberRoles = new(StringComparer.Ordinal)
    {
        "CHAIR",
        "TOURNAMENT_OPERATOR",
        "MARKETING"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivateFileStorage _fileStorage;

    public OrganizationService(IUnitOfWork unitOfWork, PrivateFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
    }































    public async Task<Result<OrganizationCreatedResponse>> CreateAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var type = request.OrganizationType.Trim().ToUpperInvariant();
        if (!OrganizationTypes.Contains(type))
        {
            return Result<OrganizationCreatedResponse>.FieldFailure(
                nameof(request.OrganizationType),
                "Loại đơn vị không hợp lệ.");
        }

        var creatorExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == request.CreatedByAccountId, cancellationToken);
        if (!creatorExists)
        {
            return Result.Failure<OrganizationCreatedResponse>("Không tìm thấy tài khoản đăng ký.", StatusCodes.Status404NotFound);
        }

        var organization = new Organization
        {
            Name = request.Name.Trim(),
            OrganizationType = type,
            ParentOrganization = NormalizeOptional(request.ParentOrganization),
            ContactEmail = NormalizeOptional(request.ContactEmail),
            ContactPhone = NormalizeOptional(request.ContactPhone),
            WebsiteUrl = NormalizeOptional(request.WebsiteUrl),
            LogoUrl = NormalizeOptional(request.LogoUrl),
            Description = NormalizeOptional(request.Description),
            Status = "PENDING",
            CreatedByAccountId = request.CreatedByAccountId
        };
        var chair = new OrganizationMember
        {
            AccountId = request.CreatedByAccountId,
            Role = "CHAIR",
            Status = "ACTIVE",
            JoinedAt = DateTime.UtcNow
        };
        organization.OrganizationMembers.Add(chair);
        _unitOfWork.Repository<Organization>().Add(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new OrganizationCreatedResponse(
                organization.OrganizationId,
                organization.Status,
                OrganizationStatusLabel(organization.Status),
                RequiredDocumentTypes[type]),
            "Đã gửi hồ sơ đăng ký đơn vị tổ chức.");
    }

    public async Task<Result<OrganizationDocumentResponse>> UploadDocumentAsync(
        int organizationId,
        string documentType,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var organization = await FindOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return Result.Failure<OrganizationDocumentResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        if (organization.Status != "PENDING")
        {
            return Result.Failure<OrganizationDocumentResponse>("Chỉ hồ sơ đang chờ xét duyệt mới được bổ sung giấy tờ.");
        }

        var normalizedType = documentType.Trim();
        if (normalizedType.Length == 0)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(nameof(documentType), "Loại giấy tờ không được để trống.");
        }
        if (!RequiredDocumentTypes[organization.OrganizationType].Contains(normalizedType, StringComparer.Ordinal))
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(
                nameof(documentType),
                "Loại giấy tờ không thuộc danh sách yêu cầu của loại đơn vị này.");
        }

        StoredPrivateFile storedFile;
        try
        {
            storedFile = await _fileStorage.SaveAsync(file, "organization-documents", cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(nameof(file), exception.Message);
        }

        var document = new OrganizationDocument
        {
            OrganizationId = organizationId,
            DocumentType = normalizedType,
            FileUrl = storedFile.RelativePath,
            OriginalFileName = storedFile.OriginalFileName
        };
        _unitOfWork.Repository<OrganizationDocument>().Add(document);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(storedFile.RelativePath);
            throw;
        }

        return Result.Success(ToDocumentResponse(document), "Tải giấy tờ đơn vị thành công.");
    }

    public async Task<Result<OrganizationReviewResponse>> ReviewAsync(
        int organizationId,
        ReviewOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await FindOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return Result.Failure<OrganizationReviewResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        if (organization.Status != "PENDING")
        {
            return Result.Failure<OrganizationReviewResponse>("Hồ sơ không còn ở trạng thái chờ xét duyệt.", StatusCodes.Status409Conflict);
        }

        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("APPROVE" or "REJECT"))
        {
            return Result<OrganizationReviewResponse>.FieldFailure(nameof(request.Decision), "Quyết định chỉ được là APPROVE hoặc REJECT.");
        }

        var reviewerExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == request.ReviewerAccountId, cancellationToken);
        if (!reviewerExists)
        {
            return Result.Failure<OrganizationReviewResponse>("Không tìm thấy tài khoản người xét duyệt.", StatusCodes.Status404NotFound);
        }

        var documents = await _unitOfWork.Repository<OrganizationDocument>().Query()
            .AsNoTracking()
            .Where(document => document.OrganizationId == organizationId)
            .OrderBy(document => document.UploadedAt)
            .ToListAsync(cancellationToken);
        var reason = NormalizeOptional(request.Reason);
        if (decision == "REJECT" && reason is null)
        {
            return Result<OrganizationReviewResponse>.FieldFailure(nameof(request.Reason), "Từ chối hồ sơ phải ghi rõ lý do.");
        }
        if (decision == "APPROVE")
        {
            var uploadedTypes = documents
                .Select(document => document.DocumentType)
                .ToHashSet(StringComparer.Ordinal);
            var missingTypes = RequiredDocumentTypes[organization.OrganizationType]
                .Where(requiredType => !uploadedTypes.Contains(requiredType))
                .ToArray();
            if (missingTypes.Length > 0)
            {
                return Result<OrganizationReviewResponse>.FieldFailure(
                    nameof(request.Decision),
                    $"Chưa thể duyệt hồ sơ. Còn thiếu: {string.Join(", ", missingTypes)}.");
            }
        }

        organization.Status = decision == "APPROVE" ? "ACTIVE" : "REJECTED";
        organization.ReviewReason = reason;
        organization.ReviewedByAccountId = request.ReviewerAccountId;
        organization.ReviewedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Organization>().Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new OrganizationReviewResponse(
            organization.OrganizationId,
            organization.Name,
            organization.OrganizationType,
            organization.Status,
            OrganizationStatusLabel(organization.Status),
            organization.ReviewReason,
            organization.CreatedAt,
            RequiredDocumentTypes[organization.OrganizationType],
            documents.Select(ToDocumentResponse).ToArray()));
    }

    public async Task<Result<IReadOnlyList<OrganizationReviewResponse>>> GetReviewQueueAsync(
        CancellationToken cancellationToken)
    {
        var organizations = await _unitOfWork.Repository<Organization>().Query()
            .AsNoTracking()
            .Where(item => item.Status == "PENDING")
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var organizationIds = organizations.Select(item => item.OrganizationId).ToArray();
        var documents = await _unitOfWork.Repository<OrganizationDocument>().Query()
            .AsNoTracking()
            .Where(item => organizationIds.Contains(item.OrganizationId))
            .OrderBy(item => item.UploadedAt)
            .ToListAsync(cancellationToken);
        var documentsByOrganization = documents
            .GroupBy(item => item.OrganizationId)
            .ToDictionary(group => group.Key, group => group.Select(ToDocumentResponse).ToArray());

        var queue = organizations.Select(organization => new OrganizationReviewResponse(
            organization.OrganizationId,
            organization.Name,
            organization.OrganizationType,
            organization.Status,
            OrganizationStatusLabel(organization.Status),
            organization.ReviewReason,
            organization.CreatedAt,
            RequiredDocumentTypes[organization.OrganizationType],
            documentsByOrganization.TryGetValue(organization.OrganizationId, out var organizationDocuments)
                ? organizationDocuments
                : []))
            .ToArray();

        return Result.Success<IReadOnlyList<OrganizationReviewResponse>>(queue);
    }

    public async Task<Result<PublicOrganizationResponse>> GetPublicProfileAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await _unitOfWork.Repository<Organization>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId && item.Status == "ACTIVE", cancellationToken);
        if (organization is null)
        {
            return Result.Failure<PublicOrganizationResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        return Result.Success(await ToPublicResponseAsync(organization, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<PublicOrganizationResponse>>> GetPublicOrganizationsAsync(
        CancellationToken cancellationToken)
    {
        var organizations = await _unitOfWork.Repository<Organization>().Query()
            .AsNoTracking()
            .Where(item => item.Status == "ACTIVE")
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var stats = await LoadOrganizationStatsAsync(organizations.Select(item => item.OrganizationId).ToArray(), cancellationToken);
        var responses = organizations.Select(organization => ToPublicResponse(organization, stats)).ToList();

        return Result.Success<IReadOnlyList<PublicOrganizationResponse>>(responses);
    }

    private async Task<PublicOrganizationResponse> ToPublicResponseAsync(
        Organization organization,
        CancellationToken cancellationToken)
        => ToPublicResponse(organization, await LoadOrganizationStatsAsync([organization.OrganizationId], cancellationToken));

    private static PublicOrganizationResponse ToPublicResponse(Organization organization, OrganizationStats stats)
        => new(
            organization.OrganizationId,
            organization.Name,
            organization.OrganizationType,
            OrganizationTypeLabel(organization.OrganizationType),
            organization.ParentOrganization,
            organization.ContactEmail,
            organization.ContactPhone,
            organization.WebsiteUrl,
            organization.LogoUrl,
            organization.Description,
            stats.TournamentsOf(organization.OrganizationId),
            stats.ReputationOf(organization.OrganizationId));

    public async Task<Result<IReadOnlyList<OrganizationMemberResponse>>> GetMembersAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organizationExists = await _unitOfWork.Repository<Organization>().Query()
            .AnyAsync(item => item.OrganizationId == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Result.Failure<IReadOnlyList<OrganizationMemberResponse>>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        var members = await _unitOfWork.Repository<OrganizationMember>().Query()
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.Status != "REMOVED")
            .OrderBy(member => member.Role)
            .ThenBy(member => member.Account.FullName)
            .Select(ToMemberProjection())
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<OrganizationMemberResponse>>(members);
    }

    public async Task<Result<OrganizationMemberResponse>> InviteMemberAsync(
        int organizationId,
        InviteOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await FindOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return Result.Failure<OrganizationMemberResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        if (organization.Status != "ACTIVE")
        {
            return Result.Failure<OrganizationMemberResponse>("Chỉ đơn vị đang hoạt động mới được mời thành viên.");
        }

        var role = request.Role.Trim().ToUpperInvariant();
        if (!MemberRoles.Contains(role))
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Role), "Vai trò thành viên không hợp lệ.");
        }

        var account = await _unitOfWork.Repository<Account>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.AccountId == request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<OrganizationMemberResponse>("Không tìm thấy tài khoản được mời.", StatusCodes.Status404NotFound);
        }

        var existing = await _unitOfWork.Repository<OrganizationMember>().Query()
            .AnyAsync(member => member.OrganizationId == organizationId
                && member.AccountId == request.AccountId
                && member.Status != "REMOVED", cancellationToken);
        if (existing)
        {
            return Result.Failure<OrganizationMemberResponse>("Tài khoản đã là thành viên hoặc đang có lời mời của đơn vị.");
        }

        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            AccountId = request.AccountId,
            Role = role,
            Status = "INVITED"
        };
        _unitOfWork.Repository<OrganizationMember>().Add(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new OrganizationMemberResponse(
            member.OrganizationMemberId,
            account.AccountId,
            account.FullName,
            member.Role,
            MemberRoleLabel(member.Role),
            member.Status,
            MemberStatusLabel(member.Status),
            member.InvitedAt,
            member.JoinedAt),
            "Đã gửi lời mời tham gia đơn vị.");
    }

    public async Task<Result<OrganizationMemberResponse>> AcceptInvitationAsync(
        int organizationId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Repository<OrganizationMember>().Query()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId
                && item.OrganizationMemberId == memberId
                && item.Status == "INVITED", cancellationToken);
        if (member is null)
        {
            return Result.Failure<OrganizationMemberResponse>("Không tìm thấy lời mời đang chờ xác nhận.", StatusCodes.Status404NotFound);
        }

        member.Status = "ACTIVE";
        member.JoinedAt = DateTime.UtcNow;
        _unitOfWork.Repository<OrganizationMember>().Update(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var account = await _unitOfWork.Repository<Account>().Query()
            .AsNoTracking()
            .FirstAsync(item => item.AccountId == member.AccountId, cancellationToken);
        return Result.Success(new OrganizationMemberResponse(
            member.OrganizationMemberId,
            account.AccountId,
            account.FullName,
            member.Role,
            MemberRoleLabel(member.Role),
            member.Status,
            MemberStatusLabel(member.Status),
            member.InvitedAt,
            member.JoinedAt),
            "Đã chấp nhận lời mời tham gia đơn vị.");
    }

    public async Task<Result<OrganizationMemberResponse>> UpdateMemberRoleAsync(
        int organizationId,
        int memberId,
        string role,
        CancellationToken cancellationToken)
    {
        var normalizedRole = role.Trim().ToUpperInvariant();
        if (normalizedRole is not ("TOURNAMENT_OPERATOR" or "MARKETING"))
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(role), "Vai trò mới chỉ được là TOURNAMENT_OPERATOR hoặc MARKETING.");
        }

        var member = await _unitOfWork.Repository<OrganizationMember>().Query()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId
                && item.OrganizationMemberId == memberId
                && item.Status != "REMOVED", cancellationToken);
        if (member is null)
        {
            return Result.Failure<OrganizationMemberResponse>("Không tìm thấy thành viên của đơn vị.", StatusCodes.Status404NotFound);
        }

        member.Role = normalizedRole;
        _unitOfWork.Repository<OrganizationMember>().Update(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var account = await _unitOfWork.Repository<Account>().Query()
            .AsNoTracking()
            .FirstAsync(item => item.AccountId == member.AccountId, cancellationToken);
        return Result.Success(new OrganizationMemberResponse(
            member.OrganizationMemberId,
            account.AccountId,
            account.FullName,
            member.Role,
            MemberRoleLabel(member.Role),
            member.Status,
            MemberStatusLabel(member.Status),
            member.InvitedAt,
            member.JoinedAt),
            "Cập nhật vai trò thành viên thành công.");
    }

    public async Task<Result> TransferChairAsync(
        int organizationId,
        int newChairAccountId,
        CancellationToken cancellationToken)
    {
        var organizationExists = await _unitOfWork.Repository<Organization>().Query()
            .AnyAsync(item => item.OrganizationId == organizationId && item.Status == "ACTIVE", cancellationToken);
        if (!organizationExists)
        {
            return Result.Failure("Không tìm thấy đơn vị đang hoạt động.", StatusCodes.Status404NotFound);
        }

        var members = _unitOfWork.Repository<OrganizationMember>();
        var currentChair = await members.Query().FirstOrDefaultAsync(
            item => item.OrganizationId == organizationId && item.Role == "CHAIR" && item.Status == "ACTIVE",
            cancellationToken);
        var nextChair = await members.Query().FirstOrDefaultAsync(
            item => item.OrganizationId == organizationId
                && item.AccountId == newChairAccountId
                && item.Status == "ACTIVE",
            cancellationToken);
        if (currentChair is null || nextChair is null)
        {
            return Result.Failure("Chủ nhiệm hiện tại hoặc thành viên nhận bàn giao không hợp lệ.", StatusCodes.Status409Conflict);
        }

        if (currentChair.OrganizationMemberId == nextChair.OrganizationMemberId)
        {
            return Result.Failure("Tài khoản này hiện đã là chủ nhiệm.", StatusCodes.Status409Conflict);
        }

        currentChair.Role = "TOURNAMENT_OPERATOR";
        nextChair.Role = "CHAIR";
        members.Update(currentChair);
        members.Update(nextChair);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success("Đã bàn giao quyền chủ nhiệm đơn vị.");
    }

    public async Task<Result<OrganizationReputationResponse>> GetReputationAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        var organizationExists = await _unitOfWork.Repository<Organization>().Query()
            .AnyAsync(item => item.OrganizationId == organizationId && item.Status == "ACTIVE", cancellationToken);
        if (!organizationExists)
        {
            return Result.Failure<OrganizationReputationResponse>("Không tìm thấy đơn vị đang hoạt động.", StatusCodes.Status404NotFound);
        }

        var stats = await LoadOrganizationStatsAsync([organizationId], cancellationToken);
        return Result.Success(stats.ReputationOf(organizationId));
    }

    // Số liệu giải + uy tín cho nhiều đơn vị cùng lúc: luôn 4 câu SQL dù có bao nhiêu đơn vị
    // (trước đây mỗi đơn vị 7 câu → danh sách đơn vị bị N+1, rất chậm khi database ở xa).
    private sealed record TournamentRow(int OrganizationId, int TournamentId, string Name, string Status, DateTime StartAt);
    private sealed record DisputeRow(int OrganizationId, int MatchId, DateTime OpenedAt, DateTime? ResolvedAt);

    private sealed class OrganizationStats(
        IReadOnlyList<TournamentRow> tournaments,
        IReadOnlyDictionary<int, int> matchCounts,
        IReadOnlyList<DisputeRow> disputes,
        IReadOnlyDictionary<int, int> refundCounts)
    {
        public IReadOnlyList<OrganizationTournamentResponse> TournamentsOf(int organizationId)
            => tournaments
                .Where(item => item.OrganizationId == organizationId && item.Status != "DRAFT")
                .OrderByDescending(item => item.StartAt)
                .Select(item => new OrganizationTournamentResponse(
                    item.TournamentId,
                    item.Name,
                    item.Status,
                    TournamentStatusLabel(item.Status),
                    item.StartAt))
                .ToList();

        public OrganizationReputationResponse ReputationOf(int organizationId)
        {
            var own = tournaments.Where(item => item.OrganizationId == organizationId).ToList();
            var totalTournaments = own.Count;
            var completedTournaments = own.Count(item => item.Status == "COMPLETED");
            var totalMatches = matchCounts.GetValueOrDefault(organizationId);
            var ownDisputes = disputes.Where(item => item.OrganizationId == organizationId).ToList();
            var disputedMatches = ownDisputes.Select(item => item.MatchId).Distinct().Count();
            var resolvedDisputes = ownDisputes.Where(item => item.ResolvedAt != null).ToList();
            var refundCount = refundCounts.GetValueOrDefault(organizationId);

            var completionRate = totalTournaments == 0
                ? 0
                : decimal.Round(completedTournaments * 100m / totalTournaments, 2);
            var disputeRate = totalMatches == 0
                ? 0
                : decimal.Round(disputedMatches * 100m / totalMatches, 2);
            double? averageResolutionHours = resolvedDisputes.Count == 0
                ? null
                : resolvedDisputes.Average(item => (item.ResolvedAt!.Value - item.OpenedAt).TotalHours);

            return new OrganizationReputationResponse(
                organizationId,
                completedTournaments,
                totalTournaments,
                completionRate,
                disputedMatches,
                totalMatches,
                disputeRate,
                averageResolutionHours,
                refundCount);
        }
    }

    private async Task<OrganizationStats> LoadOrganizationStatsAsync(
        int[] organizationIds,
        CancellationToken cancellationToken)
    {
        if (organizationIds.Length == 0)
        {
            return new OrganizationStats([], new Dictionary<int, int>(), [], new Dictionary<int, int>());
        }

        var tournaments = await _unitOfWork.Repository<Tournament>().Query()
            .AsNoTracking()
            .Where(item => item.OrganizationId != null && organizationIds.Contains(item.OrganizationId.Value))
            .Select(item => new TournamentRow(item.OrganizationId!.Value, item.TournamentId, item.Name, item.Status, item.StartAt))
            .ToListAsync(cancellationToken);
        var matchCounts = await _unitOfWork.Repository<Match>().Query()
            .Where(item => item.Tournament.OrganizationId != null && organizationIds.Contains(item.Tournament.OrganizationId.Value))
            .GroupBy(item => item.Tournament.OrganizationId!.Value)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);
        var disputes = await _unitOfWork.Repository<Dispute>().Query()
            .Where(item => item.Match.Tournament.OrganizationId != null && organizationIds.Contains(item.Match.Tournament.OrganizationId.Value))
            .Select(item => new DisputeRow(item.Match.Tournament.OrganizationId!.Value, item.MatchId, item.OpenedAt, item.ResolvedAt))
            .ToListAsync(cancellationToken);
        var refundCounts = await _unitOfWork.Repository<PaymentRecord>().Query()
            .Where(item => item.RecordKind == "REFUND"
                && item.Registration.Tournament.OrganizationId != null
                && organizationIds.Contains(item.Registration.Tournament.OrganizationId.Value))
            .GroupBy(item => item.Registration.Tournament.OrganizationId!.Value)
            .Select(group => new { OrganizationId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.OrganizationId, item => item.Count, cancellationToken);

        return new OrganizationStats(tournaments, matchCounts, disputes, refundCounts);
    }

    public async Task<Result> SuspendAsync(
        int organizationId,
        string reason,
        CancellationToken cancellationToken)
    {
        var organization = await FindOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return Result.Failure("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        if (organization.Status != "ACTIVE")
        {
            return Result.Failure("Chỉ đơn vị đang hoạt động mới có thể bị đình chỉ.", StatusCodes.Status409Conflict);
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length == 0)
        {
            return Result.FieldFailure(nameof(reason), "Lý do đình chỉ không được để trống.");
        }

        organization.Status = "SUSPENDED";
        organization.SuspensionReason = normalizedReason;
        organization.SuspendedAt = DateTime.UtcNow;
        _unitOfWork.Repository<Organization>().Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success("Đã đình chỉ đơn vị tổ chức.");
    }

    private async Task<Organization?> FindOrganizationAsync(int organizationId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<Organization>().Query()
            .FirstOrDefaultAsync(item => item.OrganizationId == organizationId, cancellationToken);

    private static System.Linq.Expressions.Expression<Func<OrganizationMember, OrganizationMemberResponse>> ToMemberProjection()
        => member => new OrganizationMemberResponse(
            member.OrganizationMemberId,
            member.AccountId,
            member.Account.FullName,
            member.Role,
            MemberRoleLabel(member.Role),
            member.Status,
            MemberStatusLabel(member.Status),
            member.InvitedAt,
            member.JoinedAt);

    private static string OrganizationTypeLabel(string type) => type switch
    {
        "UNIVERSITY_CLUB" => "Câu lạc bộ cấp trường",
        "EVENT_COMPANY" => "Công ty tổ chức sự kiện",
        "INDIVIDUAL" => "Cá nhân",
        _ => "Không xác định"
    };

    private static string OrganizationStatusLabel(string status) => status switch
    {
        "PENDING" => "Chờ xét duyệt",
        "ACTIVE" => "Đang hoạt động",
        "REJECTED" => "Bị từ chối",
        "SUSPENDED" => "Bị đình chỉ",
        _ => "Không xác định"
    };

    private static string MemberRoleLabel(string role) => role switch
    {
        "CHAIR" => "Chủ nhiệm đơn vị",
        "TOURNAMENT_OPERATOR" => "Điều hành giải",
        "MARKETING" => "Truyền thông",
        _ => "Không xác định"
    };

    private static string MemberStatusLabel(string status) => status switch
    {
        "INVITED" => "Đã gửi lời mời",
        "ACTIVE" => "Đang hoạt động",
        "REMOVED" => "Đã rời đơn vị",
        _ => "Không xác định"
    };

    private static string TournamentStatusLabel(string status) => status switch
    {
        "DRAFT" => "Bản nháp",
        "PUBLISHED" => "Đã công bố",
        "ONGOING" => "Đang diễn ra",
        "COMPLETED" => "Đã kết thúc",
        "CANCELLED" => "Đã hủy",
        _ => "Không xác định"
    };

    private static OrganizationDocumentResponse ToDocumentResponse(OrganizationDocument document)
        => new(
            document.OrganizationDocumentId,
            document.OrganizationId,
            document.DocumentType,
            document.OriginalFileName,
            document.UploadedAt);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
