using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// STUB TẠM — lấy người dùng hiện tại từ session do trang "Đăng nhập demo" đặt vào.
// THAY THẾ khi có AUTH thật (đọc từ Claims/JWT của HttpContext.User).
public class CurrentUserStub : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserStub(IHttpContextAccessor accessor, IWebHostEnvironment environment)
    {
        _accessor = accessor;
        _environment = environment;
    }

    private ISession? Session => _environment.IsDevelopment() ? _accessor.HttpContext?.Session : null;

    public int? UserId => Session?.GetInt32("DemoUserId");

    public string? DisplayName => Session?.GetString("DemoUserName");

    public bool IsAuthenticated => UserId.HasValue;
}
