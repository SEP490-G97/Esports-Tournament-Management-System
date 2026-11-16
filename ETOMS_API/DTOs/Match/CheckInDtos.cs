namespace ETOMS_API.DTOs;

public record CheckInTeamView(int TeamId, string Name, string? Tag, string? LogoUrl, bool CheckedIn, DateTime? CheckedInAt);

/// <summary>
/// Trạng thái check-in của một trận cho đội trưởng. <see cref="ServerNow"/> (UTC) để trình duyệt tính đếm ngược
/// theo đồng hồ máy chủ, không phụ thuộc đồng hồ máy người dùng.
/// </summary>
public record CheckInState(
    int MatchId,
    string TournamentName,
    string MatchStatus,
    DateTime? ScheduledAt,
    DateTime? OpensAt,
    DateTime? ClosesAt,
    DateTime ServerNow,
    string Phase,                 // NOT_SCHEDULED | NOT_YET | OPEN | CLOSED
    CheckInTeamView MyTeam,
    CheckInTeamView? Opponent,    // null nếu đối thủ chưa xác định
    bool CanCheckIn,
    string? Reason);              // lý do không check-in được: ALREADY_CHECKED_IN | NOT_YET | CLOSED | NOT_READY | NOT_SCHEDULED