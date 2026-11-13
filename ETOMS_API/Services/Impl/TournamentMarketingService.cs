using System.Security.Cryptography;
using System.Text;
using ETOMS_API.DTOs.Marketing;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class TournamentMarketingService : ITournamentMarketingService
{
    private static readonly string[] PublicTournamentStatuses = ["PUBLISHED", "ONGOING", "COMPLETED", "CANCELLED"];
    private static readonly string[] CountedRegistrationStatuses = ["PENDING", "WAITLISTED", "APPROVED"];

    private readonly IUnitOfWork _unitOfWork;

    public TournamentMarketingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private IQueryable<TEntity> Query<TEntity>() where TEntity : class
        => _unitOfWork.Repository<TEntity>().Query();

    public async Task<Result<TournamentLandingResponse>> GetLandingAsync(
        int tournamentId,
        CancellationToken cancellationToken)
    {
        var tournament = await Query<Tournament>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId
                && PublicTournamentStatuses.Contains(item.Status))
            .Select(item => new
            {
                item.TournamentId,
                item.Name,
                GameName = item.Game.Title,
                GameShortCode = item.Game.ShortCode,
                item.Description,
                item.BannerUrl,
                item.OrganizingUnit,
                item.Region,
                item.ContactInfo,
                item.IsOnline,
                item.Venue,
                item.Format,
                item.StartAt,
                item.RegCloseAt,
                item.EntryFee,
                item.PrizeStructure,
                item.Status,
                item.MaxTeams,
                item.RegOpenAt,
                item.BracketLockedAt,
                RegisteredTeams = item.Registrations.Count(registration =>
                    CountedRegistrationStatuses.Contains(registration.Status))
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tournament is null)
        {
            return Result<TournamentLandingResponse>.Failure(
                "Không tìm thấy giải đấu công khai.",
                StatusCodes.Status404NotFound);
        }

        var sponsors = await Query<TournamentSponsor>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId
                && (tournament.Status != "CANCELLED" || item.ShowWhenCancelled))
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new TournamentSponsorResponse(
                item.Name,
                item.LogoUrl,
                item.WebsiteUrl,
                item.Tier))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var registrationOpen = tournament.Status == "PUBLISHED"
            && tournament.BracketLockedAt is null
            && tournament.RegOpenAt <= now
            && now < tournament.RegCloseAt;

        return Result.Success(new TournamentLandingResponse(
            tournament.TournamentId,
            tournament.Name,
            tournament.GameName,
            tournament.GameShortCode,
            tournament.Description,
            tournament.BannerUrl,
            tournament.OrganizingUnit,
            tournament.Region,
            tournament.ContactInfo,
            tournament.IsOnline,
            tournament.Venue,
            tournament.Format,
            AsUtcOffset(tournament.StartAt),
            AsUtcOffset(tournament.RegCloseAt),
            tournament.EntryFee,
            tournament.PrizeStructure,
            tournament.Status,
            tournament.RegisteredTeams,
            tournament.MaxTeams,
            registrationOpen,
            sponsors));
    }

    public async Task<Result<IReadOnlyList<MarketingTournamentResponse>>> GetOrganizerTournamentsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var tournaments = await Query<Tournament>().AsNoTracking()
            .Where(item => item.OrganizerAccountId == accountId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new MarketingTournamentResponse(
                item.TournamentId,
                item.Name,
                item.Game.Title,
                item.Status,
                AsUtcOffset(item.StartAt)))
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<MarketingTournamentResponse>>(tournaments);
    }

    public async Task<Result<IReadOnlyList<PromotionLinkResponse>>> GetPromotionLinksAsync(
        int tournamentId,
        int accountId,
        Uri publicBaseUri,
        CancellationToken cancellationToken)
    {
        if (!await IsOrganizerAsync(tournamentId, accountId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<PromotionLinkResponse>>(
                "Chỉ ban tổ chức giải mới được quản lý liên kết cộng tác viên.",
                StatusCodes.Status403Forbidden);
        }

        var links = await Query<PromotionLink>().AsNoTracking()
            .Where(item => item.TournamentId == tournamentId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                item.PromotionLinkId,
                item.TournamentId,
                item.Code,
                item.Label,
                item.VisitCount,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var linkIds = links.Select(item => item.PromotionLinkId).ToArray();
        var counts = await Query<Registration>().AsNoTracking()
            .Where(item => item.PromotionLinkId.HasValue
                && linkIds.Contains(item.PromotionLinkId.Value)
                && CountedRegistrationStatuses.Contains(item.Status))
            .GroupBy(item => item.PromotionLinkId!.Value)
            .Select(group => new { PromotionLinkId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PromotionLinkId, item => item.Count, cancellationToken);

        return Result.Success<IReadOnlyList<PromotionLinkResponse>>(
            links.Select(item => ToPromotionLinkResponse(item.PromotionLinkId, item.TournamentId,
                item.Code, item.Label, item.VisitCount,
                counts.GetValueOrDefault(item.PromotionLinkId),
                item.CreatedAt, publicBaseUri)).ToArray());
    }

    public async Task<Result<PromotionLinkResponse>> CreatePromotionLinkAsync(
        int tournamentId,
        int accountId,
        CreatePromotionLinkRequest request,
        Uri publicBaseUri,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Result<PromotionLinkResponse>.FieldFailure(
                nameof(request.Label),
                "Tên cộng tác viên không được để trống.");
        }

        if (!await IsOrganizerAsync(tournamentId, accountId, cancellationToken))
        {
            return Result<PromotionLinkResponse>.Failure(
                "Chỉ ban tổ chức giải mới được tạo liên kết cộng tác viên.",
                StatusCodes.Status403Forbidden);
        }

        string code;
        do
        {
            code = GenerateCode();
        }
        while (await Query<PromotionLink>().AsNoTracking().AnyAsync(
            item => item.Code == code,
            cancellationToken));

        var link = new PromotionLink
        {
            TournamentId = tournamentId,
            CreatedByAccountId = accountId,
            Code = code,
            Label = request.Label.Trim(),
            VisitCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _unitOfWork.Repository<PromotionLink>().Add(link);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToPromotionLinkResponse(
            link.PromotionLinkId,
            link.TournamentId,
            link.Code,
            link.Label,
            link.VisitCount,
            0,
            link.CreatedAt,
            publicBaseUri), "Đã tạo liên kết cộng tác viên.");
    }

    public async Task<Result> TrackVisitAsync(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var link = await Query<PromotionLink>()
            .FirstOrDefaultAsync(item => item.Code == normalizedCode, cancellationToken);
        if (link is null)
        {
            return Result.Failure("Không tìm thấy liên kết cộng tác viên.", StatusCodes.Status404NotFound);
        }

        link.VisitCount = checked(link.VisitCount + 1);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success("Đã ghi nhận lượt truy cập.");
    }

    public async Task<Result<string>> GetRegistrationUrlAsync(
        int tournamentId,
        string? code,
        Uri publicBaseUri,
        CancellationToken cancellationToken)
    {
        var isPublic = await Query<Tournament>().AsNoTracking()
            .AnyAsync(item => item.TournamentId == tournamentId
                && PublicTournamentStatuses.Contains(item.Status), cancellationToken);
        if (!isPublic)
        {
            return Result<string>.Failure("Không tìm thấy giải đấu công khai.", StatusCodes.Status404NotFound);
        }

        var normalizedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        if (normalizedCode is not null
            && !await Query<PromotionLink>().AsNoTracking().AnyAsync(
                item => item.TournamentId == tournamentId && item.Code == normalizedCode,
                cancellationToken))
        {
            return Result<string>.Failure("Mã cộng tác viên không thuộc giải đấu này.", StatusCodes.Status404NotFound);
        }

        var path = $"share/tournaments/{tournamentId}";
        if (normalizedCode is not null)
        {
            path += $"?ref={Uri.EscapeDataString(normalizedCode)}";
        }

        return Result.Success<string>(new Uri(publicBaseUri, path).ToString());
    }

    private async Task<bool> IsOrganizerAsync(int tournamentId, int accountId, CancellationToken cancellationToken)
        => await Query<Tournament>().AsNoTracking()
            .AnyAsync(item => item.TournamentId == tournamentId
                && item.OrganizerAccountId == accountId,
                cancellationToken);

    private static string GenerateCode()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(6));

    private static PromotionLinkResponse ToPromotionLinkResponse(
        int id,
        int tournamentId,
        string code,
        string label,
        int visitCount,
        int successfulRegistrations,
        DateTime createdAt,
        Uri publicBaseUri)
    {
        var landingPath = $"share/tournaments/{tournamentId}?ref={Uri.EscapeDataString(code)}";
        return new PromotionLinkResponse(
            id,
            tournamentId,
            code,
            label,
            visitCount,
            successfulRegistrations,
            new Uri(publicBaseUri, landingPath).ToString(),
            AsUtcOffset(createdAt));
    }

    private static DateTimeOffset AsUtcOffset(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
