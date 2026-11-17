
using ETOMS_API.DTOs;

namespace ETOMS_API.Services.Interface;

public enum MatchDetailError { None, NotFound }

public record MatchDetailResult(MatchDetailError Error, MatchDetailResponse? Data = null);

/// <summary>Người xem: AccountId null = khách; Role lấy từ JWT (PLAYER / ORGANIZER / ADMIN).</summary>
public record MatchViewer(int? AccountId, string? Role)
{
    public static readonly MatchViewer Guest = new(null, null);
}

public static class MatchActions
{
    public const string CheckIn = "CHECK_IN";
    public const string SubmitReport = "SUBMIT_REPORT";
    public const string OpenDispute = "OPEN_DISPUTE";
    public const string RefereeStart = "REFEREE_START";
    public const string RefereeScore = "REFEREE_SCORE";
    public const string Manage = "MANAGE";
    public const string ManualCheckIn = "MANUAL_CHECKIN";
    public const string Substitution = "SUBSTITUTION";
    public const string ReviewSubstitution = "REVIEW_SUBSTITUTION";
}

public interface IMatchDetailService
{
    Task<MatchDetailResult> GetAsync(int matchId, MatchViewer viewer, CancellationToken ct = default);
}