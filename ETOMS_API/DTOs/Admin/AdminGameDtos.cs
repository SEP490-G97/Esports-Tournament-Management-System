namespace ETOMS_API.DTOs.Admin
{
    public class AdminGameDtos
    {
    }
    public class GameListQuery
    {
        public string? Q { get; set; }
        /// <summary>ACTIVE / INACTIVE / ALL (không phân biệt hoa thường).</summary>
        public string? Status { get; set; }
    }

    public class SaveGameRequest
    {
        public string? Title { get; set; }
        public string? ShortCode { get; set; }
        public int MinStarters { get; set; }
        public int MaxStarters { get; set; }
        public int MaxSubstitutes { get; set; }
        public string? DefaultFormat { get; set; }
        /// <summary>Các loại bằng chứng cho phép: IMAGE, VIDEO, REPLAY, DOCUMENT.</summary>
        public string[]? AllowedEvidenceTypes { get; set; }
        public int MaxEvidenceFileSizeMb { get; set; } = 10;
    }

    public record GameDetail(
    int GameId,
    string Title,
    string ShortCode,
    string? LogoUrl,
    string? BannerUrl,
    string Status,
    int MinStarters,
    int MaxStarters,
    int MaxSubstitutes,
    string DefaultFormat,
    string[] AllowedEvidenceTypes,
    int MaxEvidenceFileSizeMb,
    int TournamentCount);

    public record GameStatusCounts(int All, int Active, int Inactive);

    public record GameListResponse(List<GameDetail> Items, GameStatusCounts Counts);
}
