namespace ETOMS_API.Utils
{
    public class AuthConstants
    {
    }
    public static class TokenTypes
    {
        public const string Refresh = "REFRESH";
        public const string EmailVerify = "EMAIL_VERIFY";     
        public const string PasswordReset = "PASSWORD_RESET";   
    }

    public static class AccountStatuses
    {
        public const string PendingVerify = "PENDING_VERIFY";
        public const string Active = "ACTIVE";
        public const string Banned = "BANNED";
    }

    public static class AccountRoles
    {
        public const string Player = "PLAYER";
        public const string Organizer = "ORGANIZER";
        public const string Admin = "ADMIN";
    }

    public static class OrganizerRequestStatuses
    {
        public const string None = "NONE";
    }
}
