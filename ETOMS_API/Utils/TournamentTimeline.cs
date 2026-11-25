namespace ETOMS_API.Utils;

// Khoảng cách tối thiểu giữa các mốc của giải (nhóm chốt 17/11/2026), dùng chung cho bước 2 trình tạo giải và
// trang Sửa thông tin: đăng ký mở ít nhất 1 ngày để các đội kịp nộp đơn; đóng đăng ký trước khai mạc ít nhất 1 giờ
// để Ban tổ chức duyệt đơn và sinh nhánh; giải kéo dài ít nhất 1 giờ.
public static class TournamentTimeline
{
    public static readonly TimeSpan MinRegistrationWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan MinCloseBeforeStart = TimeSpan.FromHours(1);
    public static readonly TimeSpan MinDuration = TimeSpan.FromHours(1);

    // Trả về (tên trường, câu báo lỗi) nếu sai; null nếu hợp lệ. Thời điểm đều là UTC.
    public static (string Field, string Message)? Check(DateTime open, DateTime close, DateTime start, DateTime? end)
    {
        if (open >= close) return ("RegCloseAt", "Thời gian đóng đăng ký phải sau thời gian mở đăng ký.");
        if (close - open < MinRegistrationWindow)
            return ("RegCloseAt", "Thời gian nhận đăng ký phải kéo dài ít nhất 1 ngày (đóng đăng ký sau mở đăng ký từ 24 giờ trở lên).");
        if (close > start) return ("StartAt", "Ngày khai mạc phải sau thời gian đóng đăng ký.");
        if (start - close < MinCloseBeforeStart)
            return ("StartAt", "Khai mạc phải sau đóng đăng ký ít nhất 1 giờ để Ban tổ chức duyệt đơn và sinh nhánh đấu.");
        if (end is DateTime e)
        {
            if (e <= start) return ("EndAt", "Ngày kết thúc phải sau ngày khai mạc.");
            if (e - start < MinDuration) return ("EndAt", "Giải phải kéo dài ít nhất 1 giờ tính từ khai mạc.");
        }
        return null;
    }
}
