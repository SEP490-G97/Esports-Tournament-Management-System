namespace ETOMS_API.Utils
{
    public class EmailOptions
    {
        public const string SectionName = "Email";

        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "ETOMS";

        // Gửi mail thật được khi bật và có đủ thông tin đăng nhập SMTP (mật khẩu ứng dụng thường
        // chỉ có trên máy được cấp qua user-secrets / biến môi trường, không nằm trong repo).
        public bool CanSend => Enabled && !string.IsNullOrWhiteSpace(Host)
            && (string.IsNullOrEmpty(User) || !string.IsNullOrEmpty(Password));
    }

    public class AuthLinkOptions
    {
        public const string SectionName = "AuthLinks";

        public string BaseUrl { get; set; } = "";
        public int ResetMinutes { get; set; } = 30;
        public int VerifyHours { get; set; } = 24;
    }
}
