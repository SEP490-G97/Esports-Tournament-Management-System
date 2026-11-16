namespace ETOMS_API.DTOs.Ranking;

/// <summary>Một đội trong bảng xếp hạng Tournament Points.</summary>
public sealed record LeaderboardEntryResponse(
    int Rank,
    int TeamId,
    string TeamName,
    string TeamTag,
    string? LogoUrl,
    int GameId,
    string GameName,
    int? Season,
    long TournamentPlayed,
    decimal TitleCount,
    decimal AchievementPoint);

/// <summary>Danh hiệu của đội tại một giải đã hoàn tất.</summary>
public sealed record TeamHonorResponse(
    int TournamentId,
    string TournamentName,
    int Season,
    int Rank,
    string Placement);

/// <summary>Hồ sơ công khai tóm tắt và thành tích xếp hạng của đội.</summary>
public sealed record RankedTeamProfileResponse(
    int TeamId,
    string TeamName,
    string TeamTag,
    string? LogoUrl,
    string GameName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TeamHonorResponse> Honors);

/// <summary>Một đơn đăng ký trong bộ hồ sơ tổng kết giải.</summary>
public sealed record TournamentReportRegistrationResponse(
    int RegistrationId,
    string TeamName,
    string TeamTag,
    string Status,
    string PaymentStatus,
    DateTimeOffset? SubmittedAt,
    string FinalPlacement,
    int? FinalRank);

/// <summary>Một khoản lệ phí hoặc minh chứng hoàn tiền trong báo cáo.</summary>
public sealed record TournamentReportPaymentResponse(
    int RegistrationId,
    string TeamName,
    string RecordKind,
    decimal? DeclaredAmount,
    string? Note,
    bool IsCurrent,
    DateTimeOffset RecordedAt);

/// <summary>Kết quả một trận trong báo cáo giải.</summary>
public sealed record TournamentReportMatchResponse(
    int MatchId,
    string? TeamA,
    string? TeamB,
    string? Winner,
    int? ScoreA,
    int? ScoreB,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? VerifiedAt);

/// <summary>Tình trạng xử lý tranh chấp trong báo cáo giải.</summary>
public sealed record TournamentReportDisputeResponse(
    int MatchId,
    string OpenReason,
    string Status,
    string? RulingReason,
    string AppealStatus,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>Một dòng bảng xếp hạng vòng bảng trong báo cáo giải.</summary>
public sealed record TournamentReportStandingResponse(
    string? GroupLabel,
    int TeamId,
    string TeamName,
    long Played,
    decimal Won,
    decimal Lost,
    decimal ScoreDiff,
    decimal Points);

/// <summary>Dữ liệu tổng kết giải, chỉ trả về cho ban tổ chức của giải.</summary>
public sealed record TournamentReportResponse(
    int TournamentId,
    string TournamentName,
    string GameName,
    string Status,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TournamentReportRegistrationResponse> Registrations,
    IReadOnlyList<TournamentReportPaymentResponse> Payments,
    IReadOnlyList<TournamentReportMatchResponse> Matches,
    IReadOnlyList<TournamentReportDisputeResponse> Disputes,
    IReadOnlyList<TournamentReportStandingResponse> Standings);
