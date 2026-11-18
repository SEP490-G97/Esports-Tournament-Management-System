using MySql.Data.MySqlClient;

namespace ETOMS_API.Repositories;

// Cột RegistrationLineup.GamePositionID (GRL-04) chưa có trong database chung. Nếu EF vẫn ánh xạ cột này
// thì MỌI truy vấn đọc RegistrationLineup (đăng ký giải, đội hình...) đều lỗi "Unknown column".
// Program.cs dò 1 lần lúc khởi động (chỉ đọc information_schema) trước khi EF dựng model:
//   - có cột  -> ánh xạ như bình thường, GRL-04 chạy;
//   - chưa có -> EF bỏ qua cột, các chức năng khác chạy bình thường, GRL-04 báo chưa sẵn sàng.
// Mặc định true để test (in-memory) có đủ cột. Khi nhóm đã thêm cột và scaffold lại: xóa file này.
public static class PendingSchema
{
    public static bool LineupGamePosition { get; set; } = true;

    public static void Detect(string connectionString)
    {
        try
        {
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM information_schema.COLUMNS " +
                "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'RegistrationLineup' AND COLUMN_NAME = 'GamePositionID'",
                conn);
            LineupGamePosition = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        catch (MySqlException)
        {
            // Không dò được thì coi như chưa có cột: an toàn cho các chức năng dùng chung.
            LineupGamePosition = false;
        }
    }
}
