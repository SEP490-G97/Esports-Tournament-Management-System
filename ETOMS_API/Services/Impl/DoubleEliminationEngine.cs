using ETOMS_API.Services.Impl;


public readonly record struct Placement(int MatchNo, Slot Slot, int TeamId);

public sealed record AdvanceOutcome(
    IReadOnlyList<Placement> Placements,
    /// <summary>Đội vừa bị loại hẳn (thua lần thứ hai), nếu có.</summary>
    int? EliminatedTeamId,
    /// <summary>GF1 do đội nhánh thua thắng → trận reset được kích hoạt.</summary>
    bool ResetActivated,
    /// <summary>GF1 do đội nhánh thắng thắng → trận reset bị hủy.</summary>
    bool ResetVoided,
    int? ChampionTeamId);

/// <summary>
/// Luật chuyển đội sau mỗi trận (thuần tính toán). Đội thắng đi theo WinnerTo, đội thua theo LoserTo
/// (không có LoserTo = bị loại). Grand Final: đội từ Upper (slot A) có 0 thua, đội từ Lower (slot B) có 1 thua;
/// nếu đội Lower thắng GF1 thì cả hai đều đã thua 1 lần → đấu thêm trận Bracket Reset (GF2), người thắng GF2 vô địch.
/// </summary>
public static class DoubleEliminationEngine
{
    public static AdvanceOutcome Advance(BracketPlan plan, int matchNo, int teamA, int teamB, Slot winnerSlot)
    {
        if (matchNo < 1 || matchNo > plan.Matches.Count) throw new ArgumentOutOfRangeException(nameof(matchNo));
        if (teamA == teamB) throw new ArgumentException("Hai đội phải khác nhau.");
        var m = plan.Get(matchNo);
        int w = winnerSlot == Slot.A ? teamA : teamB;
        int l = winnerSlot == Slot.A ? teamB : teamA;

        if (m.Side == BracketSide.Grand)
        {
            if (m.IsReset) return new AdvanceOutcome(Array.Empty<Placement>(), l, false, false, w);

            if (plan.ResetNo is int reset && winnerSlot == Slot.B)
                return new AdvanceOutcome(
                    new[] { new Placement(reset, Slot.A, teamA), new Placement(reset, Slot.B, teamB) },
                    null, true, false, null);

            return new AdvanceOutcome(Array.Empty<Placement>(), l, false, plan.ResetNo != null, w);
        }

        var pl = new List<Placement>();
        if (m.WinnerTo is ETOMS_API.Services.Impl.Route wr) pl.Add(new Placement(wr.MatchNo, wr.Slot, w));
        if (m.LoserTo is ETOMS_API.Services.Impl.Route lr) pl.Add(new Placement(lr.MatchNo, lr.Slot, l));
        return new AdvanceOutcome(pl, m.LoserTo == null ? l : null, false, false, null);
    }
}