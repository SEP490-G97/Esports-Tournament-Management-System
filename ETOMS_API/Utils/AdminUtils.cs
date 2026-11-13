using ETOMS_API.DTOs.Admin;

namespace ETOMS_API.Utils
{
    public enum AdminError
    {
        None,
        InvalidReason,
        InvalidFilter,
        NotFound,
        CannotBanSelf,
        CannotBanAdmin,
        AlreadyBanned,
        NotBanned
    }

    public sealed record AdminResult(AdminError Error)
    {
        public bool Ok => Error == AdminError.None;
    }

    public sealed record AdminListResult(AdminError Error, AccountListResponse? Data)
    {
        public bool Ok => Error == AdminError.None;
    }
}
