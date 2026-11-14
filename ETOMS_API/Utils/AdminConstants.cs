namespace ETOMS_API.Utils
{
    public static class AdminConstants
    {
        public const string AdminRole = "ADMIN";

        // Game.Status (CK_Game_Status)
        public const string GameActive = "ACTIVE";
        public const string GameInactive = "INACTIVE";

        // Game.DefaultFormat (CK_Game_Format)
        public static readonly string[] Formats = { "SINGLE_ELIMINATION", "DOUBLE_ELIMINATION", "ROUND_ROBIN", "HYBRID" };

        // Giá trị cho Game.AllowedEvidenceTypes (lưu dạng chuỗi phân tách bằng dấu phẩy).
        // GIẢ ĐỊNH: schema không có CHECK và file SQL không có dữ liệu mẫu; đối chiếu với module nộp bằng chứng.
        public static readonly string[] EvidenceTypes = { "IMAGE", "VIDEO", "REPLAY", "DOCUMENT" };

        /// <summary>GameRuleset có CK_GameRuleset_File: MaxEvidenceFileSizeMB trong 1..100, nên Game cũng giới hạn như vậy.</summary>
        public const int MaxEvidenceFileSizeMb = 100;
    }

    /// <summary>Mã ActionType hợp lệ theo CK_AuditLog_Action (bản update.sql mới nhất).</summary>
    public static class AdminAuditActions
    {
        public const string GameCreate = "GAME_CREATE";
        public const string GameUpdate = "GAME_UPDATE";
        public const string GameStatus = "GAME_STATUS";
        public const string GameDelete = "GAME_DELETE";
    }
}
