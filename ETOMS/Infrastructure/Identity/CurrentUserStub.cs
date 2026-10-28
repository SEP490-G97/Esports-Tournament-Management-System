using ETOMS.Application.Interfaces;

namespace ETOMS.Infrastructure.Identity;

// STUB TẠM — lấy người dùng hiện tại từ session do trang "Đăng nhập demo" đặt vào.
// THAY THẾ khi có AUTH thật (đọc từ Claims/JWT của HttpContext.User).
public class CurrentUserStub : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserStub(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession? Session => _accessor.HttpContext?.Session;

    public int? UserId => Session?.GetInt32("DemoUserId");

    public string? DisplayName => Session?.GetString("DemoUserName");

    public bool IsAuthenticated => UserId.HasValue;
}
