namespace ETOMS_API.DTOs.Live;

// UC-45 (LIVE-01/02/04) tỉ số trực tiếp và UC-46 livestream.

public sealed record LiveGameDto(int GameNo, int? ScoreA, int? ScoreB, int? WinnerTeamId, DateTime? UpdatedAt);

public sealed record LiveStreamDto(
    int LivestreamId,
    int? MatchId,
    string? MatchLabel,
    string Platform,
    string PlatformLabel,
    string StreamUrl,
    string? EmbedUrl,
    string? Title,
    string Status,
    string StatusLabel,
    DateTime CreatedAt,
    // PLAYER = trình phát chính thức, PAGE = nhúng thẳng trang của nền tảng khác, null = chỉ mở link.
    string? EmbedKind = null);

public sealed record MatchLiveDto(
    int MatchId,
    int TournamentId,
    string TournamentName,
    int? TeamAId,
    string TeamAName,
    int? TeamBId,
    string TeamBName,
    int BestOf,
    string Status,
    string StatusLabel,
    DateTime? ScheduledAt,
    int WinsA,
    int WinsB,
    // Đội đã thắng đủ số ván của loạt BO (chưa phải kết quả chính thức — kết quả chốt qua báo cáo mù RES-01/02).
    int? SeriesLeaderTeamId,
    bool SeriesDecided,
    bool IsScoringOpen,
    string? ScoringClosedReason,
    bool CanEdit,
    int? NextGameNo,
    IReadOnlyList<LiveGameDto> Games,
    IReadOnlyList<LiveStreamDto> Streams,
    // Đổi khi tỉ số đổi — trang chỉ vẽ lại khi Version khác (LIVE-04).
    string Version);

public sealed record LiveMatchOption(int MatchId, string Label, string Status);

public sealed record TournamentLiveDto(
    int TournamentId,
    string TournamentName,
    string TournamentStatus,
    bool CanManage,
    IReadOnlyList<LiveStreamDto> Streams,
    // Chỉ trả cho Ban tổ chức (để gán livestream vào trận).
    IReadOnlyList<LiveMatchOption> Matches);

public sealed class SaveLiveGameRequest
{
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
}

public sealed class SaveLivestreamRequest
{
    public string? StreamUrl { get; set; }
    public string? Title { get; set; }
    public int? MatchId { get; set; }
    public string? Status { get; set; }
}
