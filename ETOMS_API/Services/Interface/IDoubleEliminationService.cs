namespace ETOMS_API.Services.Interface;

public enum BracketError
{
    None,
    TournamentNotFound,
    WrongFormat,
    TournamentClosed,
    BracketLocked,
    MatchesStarted,
    TeamCountInvalid,
    DuplicateTeams,
    TeamNotFound,
    InvalidBestOf,
    MatchNotFound,
    NotInBracket,
    MatchNotFinished,
    DownstreamStarted
}

public record DoubleEliminationOptions(bool GrandFinalReset = true, int BestOf = 1, int BestOfGrandFinal = 0);

public record BracketGenerated(int TournamentId, int TeamCount, int MatchCount, int NodeCount, bool GrandFinalReset);

public record BracketPlacementInfo(int MatchId, string Slot, int? TeamId);

public record BracketAdvanced(
    int MatchId,
    IReadOnlyList<BracketPlacementInfo> Placements,
    int? EliminatedTeamId,
    bool ResetActivated,
    bool ResetVoided,
    int? ChampionTeamId);

public record BracketGenerateResult(BracketError Error, BracketGenerated? Data = null);

public record BracketAdvanceResult(BracketError Error, BracketAdvanced? Data = null);

/// <summary>BRK-05 — Nhánh Double Elimination: sinh nhánh Upper/Lower/Grand Final và chuyển đội sau mỗi trận.</summary>
public interface IDoubleEliminationService
{
    /// <summary>
    /// Sinh (hoặc sinh lại) toàn bộ nhánh. <paramref name="seededTeamIds"/> xếp theo hạt giống (phần tử đầu = hạt 1).
    /// Chỉ khi giải DOUBLE_ELIMINATION chưa khóa nhánh và chưa có trận nào quá PENDING/READY. Chạy trong một giao dịch.
    /// </summary>
    Task<BracketGenerateResult> GenerateAsync(int tournamentId, IReadOnlyList<int> seededTeamIds,
        DoubleEliminationOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Sau khi một trận đã VERIFIED hoặc WALKOVER (có WinnerTeamId): đưa đội thắng/thua xuống trận kế tiếp,
    /// xử lý Grand Final và Bracket Reset. Gọi lại nhiều lần cho cùng một trận là an toàn (ghi đè khi trận sau chưa bắt đầu).
    /// </summary>
    Task<BracketAdvanceResult> AdvanceAsync(int matchId, CancellationToken ct = default);
}