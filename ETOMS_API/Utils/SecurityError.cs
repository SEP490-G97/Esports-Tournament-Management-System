namespace ETOMS_API.Utils
{
    public enum SecurityError
    {
        None,
        InvalidEmail,
        InvalidName,
        WeakPassword,
        EmailTaken,
        InvalidToken,
        WrongPassword,
        SamePassword,
        NoPassword,
        AccountNotFound,
        AccountBanned,
        UnderAge
    }

    public record SecurityResult(SecurityError Error)
    {
        public bool Ok => Error == SecurityError.None;
        public static SecurityResult Success() => new(SecurityError.None);
        public static SecurityResult Fail(SecurityError e) => new(e);
    }
}
