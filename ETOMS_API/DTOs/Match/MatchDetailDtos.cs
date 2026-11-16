namespace ETOMS_API.DTOs;

public record MatchTeamView(int? TeamId, string? Name, string? Tag, string? LogoUrl, int? Score, bool CheckedIn, bool IsWinner);

public record MatchRoundView(string Label, string? Stage, string? GroupLabel, int? RoundNo, int? Position);

public record MatchRefereeView(int AccountId, string FullName, string? AvatarUrl);

public record MatchGameView(int GameNo, int? ScoreA, int? ScoreB, int? WinnerTeamId);

public record MatchEvidenceView(int AttachmentId, string Source, string FileType, string FileUrl, long? SizeBytes,
    int? TeamId, DateTime UploadedAt);

public record MatchRoomView(string? Code, string? Password);

public record MatchDisputeView(int DisputeId, string Status, string OpenReason);

public record MatchNoShowView(string Kind, IReadOnlyList<int> TeamIds);

public record MatchViewerView(string Role, string Relation);

public record MatchTournamentView(int TournamentId, string Name, string Format, string Status, string? StreamUrl);

/// <summary>
/// Chi tiết trận đấu cho trang công khai. Dữ liệu nhạy cảm (mã phòng, minh chứng khi trận chưa chốt) chỉ có khi
/// người xem là đội tham gia / trọng tài / BTC / admin. <see cref="Actions"/> chỉ là các quyền thao tác người xem
/// ĐƯỢC PHÉP thấy trên giao diện; từng thao tác vẫn phải được kiểm tra lại ở endpoint riêng của nó.
/// </summary>
public record MatchDetailResponse(
    int MatchId,
    MatchTournamentView Tournament,
    MatchRoundView Round,
    string Status,
    int BestOf,
    DateTime? ScheduledAt,
    DateTime? CheckInOpenAt,
    DateTime? ReportDeadlineAt,
    DateTime? VerifiedAt,
    MatchTeamView TeamA,
    MatchTeamView TeamB,
    int? ScoreA,
    int? ScoreB,
    int? WinnerTeamId,
    string? ResultSource,
    MatchRefereeView? Referee,
    IReadOnlyList<MatchGameView> Games,
    bool EvidenceVisible,
    IReadOnlyList<MatchEvidenceView> Evidence,
    MatchRoomView? Room,
    MatchDisputeView? Dispute,
    MatchViewerView Viewer,
    IReadOnlyList<string> Actions,
    MatchNoShowView? NoShow = null);