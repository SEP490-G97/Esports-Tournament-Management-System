using System.Net.Mail;

namespace ETOMS_API.Utils
{
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 72; // giới hạn của BCrypt (72 byte)

        public const string Message = "Mật khẩu phải từ 8 đến 72 ký tự, gồm chữ hoa, chữ thường và chữ số.";

        public static bool IsStrong(string? password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (password.Length < MinLength || password.Length > MaxLength) return false;
            return password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
        }

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 256) return false;
            try
            {
                var addr = new MailAddress(email.Trim());
                return addr.Address.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)
                       && addr.Host.Contains('.');
            }
            catch { return false; }
        }
    }
}
