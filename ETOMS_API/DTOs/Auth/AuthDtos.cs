namespace ETOMS_API.DTOs.Auth
{
    public class AuthDtos
    {
        public record LoginRequest(string Email, string Password, bool Remember = true);
        public record GoogleRequest(string Credential);

        public record AccountDto(int Id, string Email, string Name, string? AvatarUrl, string Role);

        public record GoogleUser(string Subject, string Email, bool EmailVerified, string? Name, string? Picture);

        public record AccountStatusInfo(string Status, string? BanReason);

        public enum AuthError
        {
            None,
            InvalidCredentials,
            InvalidGoogleToken,
            GoogleEmailNotVerified,
            InvalidRefreshToken,
            AccountNotFound,
            AccountBanned,
            EmailNotVerified,
        }

        public record AuthTokens(string AccessToken, string RefreshToken, DateTime RefreshExpiresAt,
                                 bool Persistent, AccountDto User);
        public record AuthResult(AuthTokens? Tokens, AuthError Error = AuthError.None, string? BanReason = null)
        {
            public bool IsSuccess => Error == AuthError.None;
            public static AuthResult Ok(AuthTokens t) => new(t);
            public static AuthResult Fail(AuthError e, string? banReason = null) => new(null, e, banReason);
        }

        public record ConsumeResult(bool Success, int AccountId, bool Persistent)
        {
            public static ConsumeResult Fail() => new(false, 0, false);
        }
    }
}
