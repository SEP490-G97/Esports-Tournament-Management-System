using ETOMS_API.DTOs.Admin;

namespace ETOMS_API.Services.Interface
{
    public enum GameError
    {
        None,
        InvalidTitle,
        InvalidShortCode,
        InvalidRoster,
        InvalidFormat,
        InvalidEvidenceTypes,
        InvalidFileSize,
        InvalidFilter,
        NotFound,
        TitleTaken,
        CodeTaken,
        HasTournaments,
        InUse,
        AlreadyInState,
        SaveFailed
    }

    public enum GameImageKind { Logo, Banner }

    public record GameResult(GameError Error, GameDetail? Data = null);

    public record GameListResult(GameError Error, GameListResponse? Data = null);

    /// <summary>Kết quả đổi ảnh; OldUrl là ảnh cũ để controller xóa file sau khi DB đã cập nhật.</summary>
    public record GameImageResult(GameError Error, string? OldUrl = null, GameDetail? Data = null);

    /// <summary>Kết quả xóa; chứa URL ảnh của game đã xóa để dọn file.</summary>
    public record GameDeleteResult(GameError Error, string? LogoUrl = null, string? BannerUrl = null);

    public interface IAdminGameService
    {
        Task<GameListResult> ListAsync(GameListQuery query, CancellationToken ct = default);
        Task<GameResult> GetAsync(int gameId, CancellationToken ct = default);
        Task<GameResult> CreateAsync(int actorId, SaveGameRequest request, CancellationToken ct = default);
        Task<GameResult> UpdateAsync(int actorId, int gameId, SaveGameRequest request, CancellationToken ct = default);
        Task<GameResult> SetActiveAsync(int actorId, int gameId, bool active, CancellationToken ct = default);
        Task<GameImageResult> SetImageAsync(int actorId, int gameId, GameImageKind kind, string? url, CancellationToken ct = default);
        Task<GameDeleteResult> DeleteAsync(int actorId, int gameId, CancellationToken ct = default);
    }

}
