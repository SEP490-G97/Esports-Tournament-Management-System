namespace ETOMS_API.Utils;

// RES-03 / UC-29 — hạn nộp báo cáo kết quả của một trận. Hạn được ghi cùng lúc Ban tổ chức xếp giờ trận
// (UC-29 Assumptions: "The reporting deadline was published with the match schedule").
// Giải thực tế tính hạn từ lúc KẾT THÚC trận (thường 10–30 phút). ETOMS chỉ biết giờ BẮT ĐẦU, nên quy đổi:
// mỗi ván ước tính 60 phút (cấm/chọn + thi đấu + nghỉ giữa ván), cộng 60 phút để đội trưởng chụp minh chứng và nộp.
// BO1 = +2 giờ, BO3 = +4 giờ (khớp dữ liệu demo 12:00 → 16:00), BO5 = +6 giờ, BO7 = +8 giờ.
public static class ReportDeadline
{
    public static readonly TimeSpan PerGame = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan SubmitGrace = TimeSpan.FromMinutes(60);

    // MSG-MAT-002 — hành động sau hạn.
    public const string ClosedMessage = "Đã quá hạn. Vui lòng liên hệ Ban tổ chức.";

    public static DateTime For(DateTime scheduledAtUtc, int bestOf)
        => scheduledAtUtc + PerGame * Math.Max(1, bestOf) + SubmitGrace;

    public static bool HasPassed(DateTime? deadlineUtc, DateTime nowUtc)
        => deadlineUtc.HasValue && deadlineUtc.Value <= nowUtc;
}
