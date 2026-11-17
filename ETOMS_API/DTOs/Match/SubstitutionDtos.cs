namespace ETOMS_API.DTOs;

public record SubstitutionRequestBody(int OutAccountId, int InAccountId, string? Reason);

public record SubstitutionDecisionBody(bool Approve, string? Note);

public record SubstitutionPlayer(int AccountId, string Name, string? AvatarUrl);

public record SubstitutionTeamLineup(
    int TeamId,
    string TeamName,
    IReadOnlyList<SubstitutionPlayer> Starters,
    IReadOnlyList<SubstitutionPlayer> Substitutes,
    /// <summary>Số lần thay tối đa cho một trận (theo Rule giải).</summary>
    int MaxSubstitutions,
    /// <summary>Số lần đã dùng = đã duyệt + đang chờ duyệt.</summary>
    int Used);

public record SubstitutionItem(
    int Id,
    int TeamId,
    string TeamName,
    SubstitutionPlayer Out,
    SubstitutionPlayer In,
    string? Reason,
    string Status,
    DateTime RequestedAt,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? DecisionNote);

/// <summary>
/// Bảng thay người của một trận. Lineup là đội hình HIỆU LỰC (đã áp dụng các yêu cầu được duyệt).
/// Decider cho biết ai có quyền duyệt: REFEREE nếu trận có trọng tài, ngược lại ORGANIZER (fallback).
/// </summary>
public record SubstitutionBoard(
    int MatchId,
    string MatchStatus,
    DateTime? ScheduledAt,
    DateTime ServerNow,
    bool RequestWindowOpen,
    string Decider,
    int? MyTeamId,
    bool CanRequest,
    bool CanDecide,
    SubstitutionTeamLineup TeamA,
    SubstitutionTeamLineup TeamB,
    IReadOnlyList<SubstitutionItem> Items);