using ETOMS_API.DTOs.Organization;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class OrganizationService : IOrganizationService
{
    // Góp ý nhóm 18/11: tối thiểu 3 Ban tổ chức (tính cả chủ nhiệm) mới lập được đơn vị; form không hỏi loại hình nữa.
    public const int MinOrganizers = 3;
    private const string DefaultType = "INDIVIDUAL";   // giấy tờ: hồ sơ năng lực / minh chứng hoạt động — dùng được cho mọi đơn vị

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
        "MARKETING",
        "REFEREE"
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
        int creatorAccountId,
        CancellationToken cancellationToken)
    {
        // Góp ý nhóm 18/11: không hỏi loại hình nữa. Cột OrganizationType vẫn NOT NULL (CHECK) nên gán loại chung nhất,
        // giấy tờ yêu cầu là "hồ sơ năng lực / minh chứng hoạt động tổ chức giải" — hợp cho mọi kiểu đơn vị.
        var type = string.IsNullOrWhiteSpace(request.OrganizationType) ? DefaultType : request.OrganizationType.Trim().ToUpperInvariant();
        if (!OrganizationTypes.Contains(type))
        {
            return Result<OrganizationCreatedResponse>.FieldFailure(
                nameof(request.OrganizationType),
                "Loại đơn vị không hợp lệ.");
        }

        // Đơn vị cần tối thiểu MinOrganizers tài khoản Ban tổ chức (tính cả người lập): người lập nhập email các
        // Ban tổ chức cùng lập; họ nhận lời mời và phải chấp nhận trước khi Admin duyệt hồ sơ.
        var emails = (request.MemberEmails ?? new List<string>())
            .Select(e => (e ?? "").Trim()).Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (emails.Count < MinOrganizers - 1)
        {
            return Result<OrganizationCreatedResponse>.FieldFailure(nameof(request.MemberEmails),
                $"Đơn vị cần ít nhất {MinOrganizers} tài khoản Ban tổ chức: nhập email của ít nhất {MinOrganizers - 1} Ban tổ chức cùng lập (ngoài bạn).");
        }
        var cofounders = await _unitOfWork.Repository<Account>().Query().AsNoTracking()
            .Where(a => emails.Contains(a.Email))
            .Select(a => new { a.AccountId, a.Email, a.Role, a.Status })
            .ToListAsync(cancellationToken);
        foreach (var e in emails)
        {
            var acc = cofounders.FirstOrDefault(a => string.Equals(a.Email, e, StringComparison.OrdinalIgnoreCase));
            if (acc is null)
                return Result<OrganizationCreatedResponse>.FieldFailure(nameof(request.MemberEmails), $"Không tìm thấy tài khoản có email {e}.");
            if (acc.AccountId == creatorAccountId)
                return Result<OrganizationCreatedResponse>.FieldFailure(nameof(request.MemberEmails), "Không nhập email của chính bạn — bạn đã là chủ nhiệm.");
            if (acc.Role != "ORGANIZER" || acc.Status != "ACTIVE")
                return Result<OrganizationCreatedResponse>.FieldFailure(nameof(request.MemberEmails),
                    $"{e} không phải tài khoản Ban tổ chức đang hoạt động. Người này cần được Admin cấp tài khoản Ban tổ chức trước.");
        }

        var creator = await _unitOfWork.Repository<Account>().Query().AsNoTracking()
            .Where(account => account.AccountId == creatorAccountId)
            .Select(account => new { account.Role, account.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (creator is null)
        {
            return Result.Failure<OrganizationCreatedResponse>("Không tìm thấy tài khoản đăng ký.", StatusCodes.Status404NotFound);
        }
        // Ban tổ chức là một tổ chức gồm các tài khoản Ban tổ chức (Admin cấp riêng). Tuyển thủ muốn tổ chức giải
        // thì xin làm Ban tổ chức ở Hồ sơ cá nhân trước, không tự lập đơn vị bằng tài khoản tuyển thủ.
        if (creator.Role != "ORGANIZER" || creator.Status != "ACTIVE")
        {
            return Result.Failure<OrganizationCreatedResponse>(
                "Chỉ tài khoản Ban tổ chức được lập đơn vị tổ chức. Tuyển thủ hãy gửi yêu cầu làm Ban tổ chức ở trang Hồ sơ cá nhân.",
                StatusCodes.Status403Forbidden);
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
            CreatedByAccountId = creatorAccountId,
            CreatedAt = DateTime.UtcNow
        };
        var chair = new OrganizationMember
        {
            AccountId = creatorAccountId,
            Role = "CHAIR",
            Status = "ACTIVE",
            JoinedAt = DateTime.UtcNow,
            InvitedAt = DateTime.UtcNow
        };
        organization.OrganizationMembers.Add(chair);
        foreach (var acc in cofounders.Where(a => a.AccountId != creatorAccountId))
            organization.OrganizationMembers.Add(new OrganizationMember { AccountId = acc.AccountId, Role = "TOURNAMENT_OPERATOR", Status = "INVITED", InvitedAt = DateTime.UtcNow });
        _unitOfWork.Repository<Organization>().Add(organization);
        foreach (var acc in cofounders.Where(a => a.AccountId != creatorAccountId))
            _unitOfWork.Repository<Notification>().Add(new Notification
            {
                AccountId = acc.AccountId,
                NotificationType = "SYSTEM",
                Title = "Lời mời cùng lập đơn vị tổ chức",
                Body = $"Bạn được mời cùng lập đơn vị \"{organization.Name}\" (vai trò Điều hành giải). Mở \"Đơn vị của tôi\" để chấp nhận — đơn vị cần đủ {MinOrganizers} Ban tổ chức chấp nhận mới được duyệt.",
                LinkUrl = "/organization/index.html?mine=1",
                Channel = "IN_APP",
                IsRead = false,
                SentAt = DateTime.UtcNow
            });
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
        int uploaderAccountId,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(nameof(file), "Hãy chọn tệp giấy tờ để tải lên.");
        }

        var organization = await FindOrganizationAsync(organizationId, cancellationToken);
        if (organization is null)
        {
            return Result.Failure<OrganizationDocumentResponse>("Không tìm thấy đơn vị tổ chức.", StatusCodes.Status404NotFound);
        }

        if (organization.Status != "PENDING")
        {
            return Result.Failure<OrganizationDocumentResponse>("Chỉ hồ sơ đang chờ xét duyệt mới được bổ sung giấy tờ.");
        }

        var normalizedType = (documentType ?? string.Empty).Trim();
        if (normalizedType.Length == 0)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(nameof(documentType), "Loại giấy tờ không được để trống.");
        }
        var requiredTypes = RequiredDocumentTypes[organization.OrganizationType];
        var typeIndex = Array.IndexOf(requiredTypes, normalizedType);
        if (typeIndex < 0)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(
                nameof(documentType),
                "Loại giấy tờ không thuộc danh sách yêu cầu của loại đơn vị này.");
        }

        var uploadedCount = await _unitOfWork.Repository<Attachment>().Query()
            .CountAsync(item => item.OwnerType == OrganizationOwnerType && item.OwnerId == organizationId, cancellationToken);
        if (uploadedCount >= MaxDocumentsPerOrganization)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(
                nameof(file), $"Mỗi hồ sơ được tải tối đa {MaxDocumentsPerOrganization} giấy tờ.");
        }

        StoredPrivateFile storedFile;
        try
        {
            storedFile = await _fileStorage.SaveAsync(
                file, "organization-documents", cancellationToken, OrganizationDocumentName.Prefix(typeIndex, file.FileName));
        }
        catch (InvalidDataException exception)
        {
            return Result<OrganizationDocumentResponse>.FieldFailure(nameof(file), exception.Message);
        }

        // Lược đồ v2.2: giấy tờ đơn vị nằm ở Attachment (OwnerType = ORGANIZATION), không có bảng riêng.
        var document = new Attachment
        {
            OwnerType = OrganizationOwnerType,
            OwnerId = organizationId,
            FileUrl = storedFile.RelativePath,
            FileType = storedFile.RelativePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "DOCUMENT" : "IMAGE",
            SizeBytes = file.Length,
            UploadedByAccountId = uploaderAccountId,
            UploadedAt = DateTime.UtcNow
        };
        _unitOfWork.Repository<Attachment>().Add(document);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(storedFile.RelativePath);
            throw;
        }

        return Result.Success(ToDocumentResponse(document, organization.OrganizationType), "Tải giấy tờ đơn vị thành công.");
    }

    public async Task<Result<OrganizationReviewResponse>> ReviewAsync(
        int organizationId,
        ReviewOrganizationRequest request,
        int reviewerAccountId,
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

        var documents = (await _unitOfWork.Repository<Attachment>().Query()
            .AsNoTracking()
            .Where(document => document.OwnerType == OrganizationOwnerType && document.OwnerId == organizationId)
            .OrderBy(document => document.UploadedAt)
            .ToListAsync(cancellationToken))
            .Select(document => ToDocumentResponse(document, organization.OrganizationType))
            .ToArray();
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
            var activeOrganizers = await _unitOfWork.Repository<OrganizationMember>().Query()
                .CountAsync(m => m.OrganizationId == organizationId && m.Status == "ACTIVE", cancellationToken);
            if (activeOrganizers < MinOrganizers)
            {
                return Result<OrganizationReviewResponse>.FieldFailure(nameof(request.Decision),
                    $"Chưa thể duyệt: đơn vị mới có {activeOrganizers}/{MinOrganizers} Ban tổ chức đã chấp nhận tham gia.");
            }
        }

        organization.Status = decision == "APPROVE" ? "ACTIVE" : "REJECTED";
        organization.ReviewReason = reason;
        organization.ReviewedByAccountId = reviewerAccountId;
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
            documents));
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
        var typeByOrganization = organizations.ToDictionary(item => item.OrganizationId, item => item.OrganizationType);
        var documents = await _unitOfWork.Repository<Attachment>().Query()
            .AsNoTracking()
            .Where(item => item.OwnerType == OrganizationOwnerType && organizationIds.Contains(item.OwnerId))
            .OrderBy(item => item.UploadedAt)
            .ToListAsync(cancellationToken);
        var documentsByOrganization = documents
            .GroupBy(item => item.OwnerId)
            .ToDictionary(group => group.Key,
                group => group.Select(item => ToDocumentResponse(item, typeByOrganization[group.Key])).ToArray());

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

    public async Task<Result<IReadOnlyList<MyOrganizationResponse>>> GetMyOrganizationsAsync(int accountId, CancellationToken cancellationToken)
    {
        var rows = await _unitOfWork.Repository<OrganizationMember>().Query().AsNoTracking()
            .Where(member => member.AccountId == accountId && member.Status != "REMOVED")
            .Select(member => new
            {
                member.OrganizationId,
                member.Organization.Name,
                OrgStatus = member.Organization.Status,
                member.OrganizationMemberId,
                member.Role,
                member.Status
            })
            .ToListAsync(cancellationToken);
        // Lời mời đang chờ lên đầu để người dùng thấy ngay việc cần làm.
        IReadOnlyList<MyOrganizationResponse> list = rows
            .OrderBy(r => r.Status == "INVITED" ? 0 : 1).ThenBy(r => r.Name)
            .Select(r => new MyOrganizationResponse(r.OrganizationId, r.Name, r.OrgStatus, OrganizationStatusLabel(r.OrgStatus),
                r.OrganizationMemberId, r.Role, MemberRoleLabel(r.Role), r.Status, MemberStatusLabel(r.Status)))
            .ToList();
        return Result.Success(list);
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
        // Mời chỉ cho vai trò thành viên; chủ nhiệm đổi qua "Bàn giao chủ nhiệm" (một đơn vị một chủ nhiệm).
        if (!MemberRoles.Contains(role) || role == "CHAIR")
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Role), "Vai trò thành viên không hợp lệ.");
        }

        // Mời theo email (dễ dùng) hoặc mã tài khoản. Thành viên đơn vị phải là tài khoản Ban tổ chức đang hoạt động.
        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0 && request.AccountId < 1)
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Email), "Nhập email tài khoản Ban tổ chức cần mời.");
        }
        var account = await _unitOfWork.Repository<Account>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => email.Length > 0 ? item.Email == email : item.AccountId == request.AccountId, cancellationToken);
        if (account is null)
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Email), "Không tìm thấy tài khoản có email này.");
        }
        if (account.Role != "ORGANIZER" || account.Status != "ACTIVE")
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(request.Email),
                "Chỉ mời được tài khoản Ban tổ chức. Người này cần được Admin cấp tài khoản Ban tổ chức trước.");
        }

        var existing = await _unitOfWork.Repository<OrganizationMember>().Query()
            .AnyAsync(member => member.OrganizationId == organizationId
                && member.AccountId == account.AccountId
                && member.Status != "REMOVED", cancellationToken);
        if (existing)
        {
            return Result.Failure<OrganizationMemberResponse>("Tài khoản đã là thành viên hoặc đang có lời mời của đơn vị.");
        }

        var member = new OrganizationMember
        {
            OrganizationId = organizationId,
            AccountId = account.AccountId,
            Role = role,
            Status = "INVITED"
        };
        _unitOfWork.Repository<OrganizationMember>().Add(member);
        // Báo người được mời: bấm vào là tới đúng đơn vị để Chấp nhận (trước đây không có thông báo nên không ai biết có lời mời).
        _unitOfWork.Repository<Notification>().Add(new Notification
        {
            AccountId = account.AccountId,
            NotificationType = "SYSTEM",
            Title = "Lời mời vào đơn vị tổ chức",
            Body = $"Bạn được mời vào đơn vị \"{organization.Name}\" với vai trò {MemberRoleLabel(role)}. Mở đơn vị để chấp nhận lời mời.",
            LinkUrl = "/organization/details.html?id=" + organizationId,
            Channel = "IN_APP",
            IsRead = false,
            SentAt = DateTime.UtcNow
        });
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
        int accountId,
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
        if (member.AccountId != accountId)
        {
            return Result.Failure<OrganizationMemberResponse>("Chỉ người được mời mới được tự xác nhận lời mời này.", StatusCodes.Status403Forbidden);
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
        if (normalizedRole is not ("TOURNAMENT_OPERATOR" or "MARKETING" or "REFEREE"))
        {
            return Result<OrganizationMemberResponse>.FieldFailure(nameof(role), "Vai trò mới chỉ được là Điều hành giải, Truyền thông hoặc Trọng tài.");
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
        "REFEREE" => "Trọng tài",
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

    private const string OrganizationOwnerType = "ORGANIZATION";
    private const int MaxDocumentsPerOrganization = 10;

    // Loại giấy tờ + tên gốc đọc lại từ tên tệp đã lưu (OrganizationDocumentName); tệp cũ không theo
    // quy ước thì hiện "Giấy tờ đơn vị" và tên tệp lưu trữ.
    private static OrganizationDocumentResponse ToDocumentResponse(Attachment document, string organizationType)
    {
        var (typeIndex, originalName) = OrganizationDocumentName.Parse(document.FileUrl);
        var types = RequiredDocumentTypes.TryGetValue(organizationType, out var list) ? list : [];
        return new(
            document.AttachmentId,
            document.OwnerId,
            typeIndex is int i && i >= 0 && i < types.Length ? types[i] : "Giấy tờ đơn vị",
            originalName ?? Path.GetFileName(document.FileUrl),
            document.UploadedAt);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<bool> IsAdminAsync(int accountId)
        => await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(a => a.AccountId == accountId && a.Role == "ADMIN");

    public async Task<bool> IsActiveChairAsync(int organizationId, int accountId)
        => await _unitOfWork.Repository<OrganizationMember>().Query()
            .AnyAsync(m => m.OrganizationId == organizationId && m.AccountId == accountId
                && m.Role == "CHAIR" && m.Status == "ACTIVE");
}
