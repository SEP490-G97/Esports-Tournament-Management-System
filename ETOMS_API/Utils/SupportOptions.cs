namespace ETOMS_API.Utils
{
    public sealed class SupportOptions
    {
        public const string SectionName = "Support";

        /// <summary>Email nhận khiếu nại, ví dụ support@etoms.vn.</summary>
        public string Email { get; set; } = "";

        /// <summary>Tùy chọn: trang/biểu mẫu khiếu nại.</summary>
        public string Url { get; set; } = "";
    }
}
