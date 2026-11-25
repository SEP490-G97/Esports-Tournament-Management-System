namespace ETOMS_API.Utils;

// Nhãn tiếng Việt và class badge cho các giá trị trạng thái lưu trong DB.
public static class StatusText
{
    public static string Tournament(string status) => status switch
    {
        "DRAFT" => "Nháp",
        "PUBLISHED" => "Đã xuất bản",
        "ONGOING" => "Đang diễn ra",
        "COMPLETED" => "Đã kết thúc",
        "CANCELLED" => "Đã hủy",
        _ => status
    };

    public static string TournamentClass(string status) => status switch
    {
        "PUBLISHED" or "ONGOING" => "badge-active",
        "COMPLETED" => "badge-gold",
        "CANCELLED" => "badge-danger",
        _ => "badge-soft"
    };

    public static string Registration(string status) => status switch
    {
        "PENDING" => "Chờ duyệt",
        "APPROVED" => "Đã duyệt",
        "WAITLISTED" => "Hàng đợi",
        "REJECTED" => "Từ chối",
        "WITHDRAWN" => "Đã rút",
        "DISQUALIFIED" => "Bị tước quyền",
        _ => status
    };

    public static string Match(string status) => status switch
    {
        "PENDING" => "Chờ thi đấu",
        "READY" => "Sẵn sàng",
        "LIVE" => "Đang đấu",
        "WAITING_REPORT" => "Chờ báo cáo",
        "DISPUTED" => "Tranh chấp",
        "VERIFIED" => "Đã xác nhận",
        "WALKOVER" => "Thắng trắng",
        "CANCELLED" => "Đã hủy",
        _ => status
    };

    public static string MatchClass(string status) => status switch
    {
        "LIVE" or "VERIFIED" or "WALKOVER" => "badge-active",
        "DISPUTED" or "CANCELLED" => "badge-danger",
        "WAITING_REPORT" => "badge-gold",
        _ => "badge-soft"
    };

    public static string DisputeReason(string reason) => reason switch
    {
        "CONFLICTING_REPORT" => "Hai báo cáo lệch nhau",
        "REPORT_TIMEOUT" => "Quá hạn báo cáo",
        "FLAGGED" => "Bị cắm cờ",
        _ => reason
    };
}
