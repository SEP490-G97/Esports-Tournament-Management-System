using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed class ProfileService : IProfileService
{
    private const string InGameNameType = "IN_GAME_NAME";
    private const string IdentityDocumentType = "IDENTITY_DOCUMENT";

    // Ảnh đại diện là ảnh công khai (hiện trên hồ sơ công khai) nên lưu trong wwwroot, không phải kho riêng tư.
    private const string AvatarUrlPrefix = "/uploads/avatars/";
    private const long MaxAvatarBytes = 2 * 1024 * 1024;
    private const int MinAvatarSide = 64, MaxAvatarSide = 4096;

    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivateFileStorage _fileStorage;
    private readonly string _avatarDirectory;

    public ProfileService(IUnitOfWork unitOfWork, PrivateFileStorage fileStorage, IWebHostEnvironment environment)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _avatarDirectory = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "avatars");
    }

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    // PRF-01: ảnh đại diện tải từ máy (JPG/PNG, <= 2 MB, 64..4096 px mỗi chiều).
    public async Task<Result<ProfileResponse>> UploadAvatarAsync(int accountId, IFormFile? file, CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        if (account is null)
            return Result.Failure<ProfileResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        if (file is null || file.Length == 0)
            return Result<ProfileResponse>.FieldFailure("AvatarFile", "Vui lòng chọn ảnh.");
        if (file.Length > MaxAvatarBytes)
            return Result<ProfileResponse>.FieldFailure("AvatarFile", "Ảnh tối đa 2 MB.");

        ImageHeader.Info? info;
        await using (var check = file.OpenReadStream()) info = ImageHeader.Read(check);
        if (info is null)
            return Result<ProfileResponse>.FieldFailure("AvatarFile", "Tệp không phải ảnh JPG hoặc PNG hợp lệ.");
        if (info.Width < MinAvatarSide || info.Height < MinAvatarSide || info.Width > MaxAvatarSide || info.Height > MaxAvatarSide)
            return Result<ProfileResponse>.FieldFailure("AvatarFile",
                $"Kích thước ảnh phải từ {MinAvatarSide}×{MinAvatarSide} đến {MaxAvatarSide}×{MaxAvatarSide} px (ảnh của bạn {info.Width}×{info.Height} px).");

        Directory.CreateDirectory(_avatarDirectory);
        var fileName = $"{accountId}-{Guid.NewGuid():N}{info.Extension}";
        await using (var target = new FileStream(Path.Combine(_avatarDirectory, fileName), FileMode.CreateNew, FileAccess.Write))
            await file.CopyToAsync(target, cancellationToken);

        var previous = account.AvatarUrl;
        account.AvatarUrl = AvatarUrlPrefix + fileName;
        _unitOfWork.Repository<Account>().Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        DeleteUploadedAvatar(previous);

        return Result.Success(ToProfileResponse(account), "Đã cập nhật ảnh đại diện.");
    }

    // Chỉ xóa ảnh do chính hệ thống lưu (không đụng link ngoài), và chỉ trong thư mục avatars.
    private void DeleteUploadedAvatar(string? url)
    {
        if (url is null || !url.StartsWith(AvatarUrlPrefix, StringComparison.Ordinal)) return;
        var name = Path.GetFileName(url[AvatarUrlPrefix.Length..]);
        var path = Path.Combine(_avatarDirectory, name);
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* ảnh cũ còn bị giữ — bỏ qua */ }
    }



















    public async Task<Result<ProfileResponse>> GetProfileAsync(int accountId, CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        return account is null
            ? Result.Failure<ProfileResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound)
            : Result.Success(ToProfileResponse(account));
    }

    public async Task<Result<ProfileResponse>> UpdateProfileAsync(
        int accountId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<ProfileResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        var fullName = (request.FullName ?? "").Trim();
        if (fullName.Length is < 2 or > 100)
        {
            return Result<ProfileResponse>.FieldFailure(nameof(request.FullName), "Tên hiển thị phải từ 2 đến 100 ký tự.");
        }

        // Ảnh đại diện: ảnh đã tải lên của hệ thống, hoặc liên kết http(s) đầy đủ — trước đây nhận cả "hhh".
        var avatar = NormalizeOptional(request.AvatarUrl);
        if (avatar is not null && !avatar.StartsWith(AvatarUrlPrefix, StringComparison.Ordinal) && !IsHttpUrl(avatar))
        {
            return Result<ProfileResponse>.FieldFailure(nameof(request.AvatarUrl),
                "Ảnh đại diện phải là liên kết bắt đầu bằng http:// hoặc https://, hoặc chọn ảnh từ máy.");
        }

        if (request.DateOfBirth is DateOnly dob)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dob.Year - (dob.AddYears(today.Year - dob.Year) > today ? 1 : 0);
            if (dob > today)
                return Result<ProfileResponse>.FieldFailure(nameof(request.DateOfBirth), "Ngày sinh không được ở tương lai.");
            if (dob.Year < 1900)
                return Result<ProfileResponse>.FieldFailure(nameof(request.DateOfBirth), "Ngày sinh không hợp lệ.");
            if (age < 18)
                return Result<ProfileResponse>.FieldFailure(nameof(request.DateOfBirth), "Bạn phải đủ 18 tuổi (cùng quy định với lúc đăng ký).");
        }

        var social = NormalizeOptional(request.SocialLinks);
        if (social is not null)
        {
            var lines = social.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length > 10)
                return Result<ProfileResponse>.FieldFailure(nameof(request.SocialLinks), "Tối đa 10 liên kết mạng xã hội.");
            var bad = lines.FirstOrDefault(line => !IsHttpUrl(line));
            if (bad is not null)
                return Result<ProfileResponse>.FieldFailure(nameof(request.SocialLinks),
                    $"Liên kết \"{(bad.Length > 40 ? bad[..40] + "…" : bad)}\" không hợp lệ — mỗi dòng phải bắt đầu bằng http:// hoặc https://.");
            social = string.Join("\n", lines);
        }

        var previousAvatar = account.AvatarUrl;
        account.FullName = fullName;
        account.AvatarUrl = avatar;
        account.DateOfBirth = request.DateOfBirth;
        account.Phone = NormalizeOptional(request.Phone);
        account.SocialLinks = social;
        _unitOfWork.Repository<Account>().Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (previousAvatar != avatar) DeleteUploadedAvatar(previousAvatar);

        return Result.Success(ToProfileResponse(account), "Cập nhật hồ sơ thành công.");
    }

    public async Task<Result<CredentialResponse>> UpsertGameAccountAsync(
        int accountId,
        int gameId,
        string inGameName,
        CancellationToken cancellationToken)
    {
        var accountExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == accountId, cancellationToken);
        if (!accountExists)
        {
            return Result.Failure<CredentialResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        var game = await _unitOfWork.Repository<Game>().Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.GameId == gameId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<CredentialResponse>("Không tìm thấy tựa game.", StatusCodes.Status404NotFound);
        }

        if (game.Status != "ACTIVE")
        {
            return Result<CredentialResponse>.FieldFailure(nameof(gameId), "Tựa game hiện không hoạt động.");
        }

        var normalizedName = (inGameName ?? "").Trim();
        if (normalizedName.Length is < 2 or > 150)
        {
            return Result<CredentialResponse>.FieldFailure("InGameName", "Tên trong game phải từ 2 đến 150 ký tự.");
        }

        var repository = _unitOfWork.Repository<PlayerCredential>();
        var credential = await repository.Query().FirstOrDefaultAsync(
            item => item.AccountId == accountId
                && item.GameId == gameId
                && item.CredentialType == InGameNameType,
            cancellationToken);

        if (credential is null)
        {
            credential = new PlayerCredential
            {
                AccountId = accountId,
                GameId = gameId,
                CredentialType = InGameNameType,
                Value = normalizedName
            };
            repository.Add(credential);
        }
        else
        {
            credential.Value = normalizedName;
            repository.Update(credential);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(ToCredentialResponse(credential), "Lưu tài khoản trong game thành công.");
    }

    public async Task<Result<CredentialResponse>> UploadIdentityDocumentAsync(
        int accountId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var accountExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == accountId, cancellationToken);
        if (!accountExists)
        {
            return Result.Failure<CredentialResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        StoredPrivateFile storedFile;
        try
        {
            storedFile = await _fileStorage.SaveAsync(file, "identity-documents", cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return Result<CredentialResponse>.FieldFailure(nameof(file), exception.Message);
        }

        var credential = new PlayerCredential
        {
            AccountId = accountId,
            CredentialType = IdentityDocumentType,
            FileUrl = storedFile.RelativePath
        };
        _unitOfWork.Repository<PlayerCredential>().Add(credential);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteAsync(storedFile.RelativePath);
            throw;
        }

        return Result.Success(ToCredentialResponse(credential), "Tải minh chứng định danh thành công.");
    }

    public async Task<Result<IReadOnlyList<CredentialResponse>>> GetIdentityDocumentsAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var accountExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == accountId, cancellationToken);
        if (!accountExists)
        {
            return Result.Failure<IReadOnlyList<CredentialResponse>>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        var documents = await _unitOfWork.Repository<PlayerCredential>().Query()
            .AsNoTracking()
            .Where(item => item.AccountId == accountId && item.CredentialType == IdentityDocumentType)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new CredentialResponse(
                item.PlayerCredentialId,
                item.GameId,
                item.CredentialType,
                "Giấy tờ định danh",
                null,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CredentialResponse>>(documents);
    }

    public async Task<Result<IdentityDocumentDownload>> GetIdentityDocumentDownloadAsync(
        int accountId,
        int credentialId,
        int requesterAccountId,
        CancellationToken cancellationToken)
    {
        var document = await _unitOfWork.Repository<PlayerCredential>().Query()
            .AsNoTracking()
            .Where(item => item.PlayerCredentialId == credentialId
                && item.AccountId == accountId
                && item.CredentialType == IdentityDocumentType)
            .Select(item => item.FileUrl)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(document))
        {
            return Result.Failure<IdentityDocumentDownload>("Không tìm thấy giấy tờ định danh.", StatusCodes.Status404NotFound);
        }

        if (accountId != requesterAccountId)
        {
            return Result.Failure<IdentityDocumentDownload>(
                "Chỉ chủ tài khoản mới có thể tải giấy tờ tại hồ sơ cá nhân. Ban tổ chức cần dùng đường dẫn hồ sơ đăng ký của giải.",
                StatusCodes.Status403Forbidden);
        }

        return Result.Success(new IdentityDocumentDownload(
            document,
            "identity-document" + Path.GetExtension(document)));
    }

    public async Task<Result<PublicProfileResponse>> GetPublicProfileAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<PublicProfileResponse>("Không tìm thấy người chơi.", StatusCodes.Status404NotFound);
        }

        var gameAccounts = await _unitOfWork.Repository<PlayerCredential>().Query()
            .AsNoTracking()
            .Where(item => item.AccountId == accountId && item.CredentialType == InGameNameType)
            .Select(item => new PublicGameAccountResponse(
                item.GameId!.Value,
                item.Game!.Title,
                item.Value!))
            .ToListAsync(cancellationToken);

        var teams = await _unitOfWork.Repository<TeamMember>().Query()
            .AsNoTracking()
            .Where(member => member.AccountId == accountId && member.Status == "ACTIVE")
            .Select(member => new PublicTeamResponse(
                member.TeamId,
                member.Team.Name,
                member.Team.Game.Title))
            .ToListAsync(cancellationToken);

        var titles = await _unitOfWork.Repository<Registration>().Query()
            .AsNoTracking()
            .Where(registration =>
                registration.RegistrationLineups.Any(lineup => lineup.AccountId == accountId)
                && registration.FinalPlacement != "NONE")
            .Select(registration => new PublicTitleResponse(
                registration.TournamentId,
                registration.Tournament.Name,
                registration.FinalPlacement,
                PlacementLabel(registration.FinalPlacement)))
            .ToListAsync(cancellationToken);

        return Result.Success(new PublicProfileResponse(
            account.AccountId,
            account.FullName,
            account.AvatarUrl,
            gameAccounts,
            teams,
            titles));
    }

    public async Task<Result<IReadOnlyList<MatchHistoryResponse>>> GetMatchHistoryAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var accountExists = await _unitOfWork.Repository<Account>().Query()
            .AnyAsync(account => account.AccountId == accountId, cancellationToken);
        if (!accountExists)
        {
            return Result.Failure<IReadOnlyList<MatchHistoryResponse>>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        var lineups = await _unitOfWork.Repository<RegistrationLineup>().Query()
            .AsNoTracking()
            .Where(lineup => lineup.AccountId == accountId)
            .Select(lineup => new
            {
                lineup.Registration.TournamentId,
                lineup.Registration.TeamId,
                lineup.Registration.Tournament.Name,
                TeamName = lineup.Registration.Team.Name,
                lineup.Slot
            })
            .ToListAsync(cancellationToken);

        if (lineups.Count == 0)
        {
            return Result.Success<IReadOnlyList<MatchHistoryResponse>>([]);
        }

        var teamByTournament = lineups
            .GroupBy(item => item.TournamentId)
            .ToDictionary(group => group.Key, group => group.First());
        var tournamentIds = teamByTournament.Keys.ToArray();
        var teamIds = teamByTournament.Values.Select(item => item.TeamId).Distinct().ToArray();

        var matches = await _unitOfWork.Repository<Match>().Query()
            .AsNoTracking()
            .Where(match => tournamentIds.Contains(match.TournamentId)
                && ((match.TeamAid.HasValue && teamIds.Contains(match.TeamAid.Value))
                    || (match.TeamBid.HasValue && teamIds.Contains(match.TeamBid.Value))))
            .Select(match => new
            {
                match.MatchId,
                match.TournamentId,
                match.TeamAid,
                match.TeamBid,
                match.ScoreA,
                match.ScoreB,
                match.ScheduledAt,
                match.Status,
                TournamentName = match.Tournament.Name,
                TeamAName = match.TeamA == null ? null : match.TeamA.Name,
                TeamBName = match.TeamB == null ? null : match.TeamB.Name
            })
            .ToListAsync(cancellationToken);

        var history = new List<MatchHistoryResponse>();
        foreach (var match in matches)
        {
            if (!teamByTournament.TryGetValue(match.TournamentId, out var participant))
            {
                continue;
            }

            var isTeamA = match.TeamAid == participant.TeamId;
            var isTeamB = match.TeamBid == participant.TeamId;
            if (!isTeamA && !isTeamB)
            {
                continue;
            }

            history.Add(new MatchHistoryResponse(
                match.MatchId,
                match.TournamentName,
                isTeamA ? match.TeamAName! : match.TeamBName!,
                participant.Slot,
                match.ScheduledAt,
                isTeamA ? match.ScoreA : match.ScoreB,
                isTeamA ? match.ScoreB : match.ScoreA,
                match.Status,
                MatchStatusLabel(match.Status)));
        }

        return Result.Success<IReadOnlyList<MatchHistoryResponse>>(
            history.OrderByDescending(item => item.ScheduledAt).ToArray());
    }

    public async Task<Result<NotificationPreferencesResponse>> GetNotificationPreferencesAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        return account is null
            ? Result.Failure<NotificationPreferencesResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound)
            : Result.Success(new NotificationPreferencesResponse(account.NotifyInApp, account.NotifyEmail));
    }

    public async Task<Result<NotificationPreferencesResponse>> UpdateNotificationPreferencesAsync(
        int accountId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var account = await FindAccountAsync(accountId, cancellationToken);
        if (account is null)
        {
            return Result.Failure<NotificationPreferencesResponse>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        account.NotifyInApp = request.NotifyInApp;
        account.NotifyEmail = request.NotifyEmail;
        _unitOfWork.Repository<Account>().Update(account);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(
            new NotificationPreferencesResponse(account.NotifyInApp, account.NotifyEmail),
            "Cập nhật cài đặt thông báo thành công.");
    }

    private Task<Account?> FindAccountAsync(int accountId, CancellationToken cancellationToken)
        => _unitOfWork.Repository<Account>().Query()
            .FirstOrDefaultAsync(account => account.AccountId == accountId, cancellationToken);

    private static ProfileResponse ToProfileResponse(Account account)
        => new(
            account.AccountId,
            account.Email,
            account.FullName,
            account.AvatarUrl,
            account.DateOfBirth,
            account.Phone,
            account.SocialLinks);

    private static CredentialResponse ToCredentialResponse(PlayerCredential credential)
        => new(
            credential.PlayerCredentialId,
            credential.GameId,
            credential.CredentialType,
            credential.CredentialType == InGameNameType ? "Tên trong game" : "Giấy tờ định danh",
            credential.CredentialType == InGameNameType ? credential.Value : null,
            credential.CreatedAt);

    private static string MatchStatusLabel(string status) => status switch
    {
        "PENDING" => "Chưa bắt đầu",
        "READY" => "Sẵn sàng",
        "LIVE" => "Đang thi đấu",
        "WAITING_REPORT" => "Chờ báo cáo kết quả",
        "DISPUTED" => "Đang tranh chấp",
        "VERIFIED" => "Đã xác minh",
        "WALKOVER" => "Thắng do đối thủ vắng mặt",
        "CANCELLED" => "Đã hủy",
        _ => "Không xác định"
    };

    private static string PlacementLabel(string placement) => placement switch
    {
        "CHAMPION" => "Vô địch",
        "RUNNER_UP" => "Á quân",
        "THIRD" => "Hạng ba",
        "PARTICIPANT" => "Tham gia",
        _ => "Không xác định"
    };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
