namespace ETOMS_API.Utils;

public enum CheckInPhase { NotScheduled, NotYet, Open, Closed }

/// <summary>Luật check-in: mở trước giờ thi đấu 30 phút, đóng đúng giờ thi đấu (cột Match.CheckInOpenAt không dùng để quyết định).</summary>
public static class CheckInRules
{
    public static readonly TimeSpan Lead = TimeSpan.FromMinutes(30);

    public static DateTime? OpensAt(DateTime? scheduledAt) => scheduledAt?.Subtract(Lead);

    public static CheckInPhase PhaseOf(DateTime? scheduledAt, DateTime nowUtc)
    {
        if (scheduledAt == null) return CheckInPhase.NotScheduled;
        if (nowUtc < scheduledAt.Value - Lead) return CheckInPhase.NotYet;
        return nowUtc < scheduledAt.Value ? CheckInPhase.Open : CheckInPhase.Closed;   // đóng đúng giờ: now >= ScheduledAt
    }
}