using MySql.Data.MySqlClient;

namespace ETOMS_API.Repositories;

// Dò 1 lần lúc khởi động (chỉ đọc information_schema) các cột mới mà code dùng nhưng DB chung có thể CHƯA chạy script:
//   - có cột  -> chức năng chạy đầy đủ;
//   - chưa có -> EF bỏ qua cột / tính năng tạm tắt, các chức năng khác vẫn chạy (không lỗi "Unknown column").
// Mặc định true để test (in-memory) có đủ cột.
//   * RegistrationLineup.GamePositionID — GRL-04 xếp vị trí đội hình.
//   * MatchRequest.DecisionNote        — ghi chú duyệt yêu cầu thay người (gộp MatchSubstitution vào MatchRequest, 18/11).
//   * Attachment.Content               — bản lưu tệp tải lên trong DB chung để máy khác cũng thấy ảnh (18/11).
public static class PendingSchema
{
    public static bool LineupGamePosition { get; set; } = true;
    public static bool MatchRequestDecisionNote { get; set; } = true;
    public static bool AttachmentContent { get; set; } = true;

    public static void Detect(string connectionString)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            bool Has(string table, string column)
            {
                using var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c", conn);
                cmd.Parameters.AddWithValue("@t", table);
                cmd.Parameters.AddWithValue("@c", column);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            LineupGamePosition = Has("RegistrationLineup", "GamePositionID");
            MatchRequestDecisionNote = Has("MatchRequest", "DecisionNote");
            AttachmentContent = Has("Attachment", "Content");
        }
        catch (MySqlException)
        {
            // Không dò được thì coi như chưa có cột: an toàn cho các chức năng dùng chung.
            LineupGamePosition = MatchRequestDecisionNote = AttachmentContent = false;
        }
    }
}
