using ETOMS_API.DTOs.Profile;
using ETOMS_API.Models;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public sealed partial class ProfileService : IProfileService
{
    private const string InGameNameType = "IN_GAME_NAME";
    private const string IdentityDocumentType = "IDENTITY_DOCUMENT";

    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivateFileStorage _fileStorage;

    public ProfileService(IUnitOfWork unitOfWork, PrivateFileStorage fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
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
