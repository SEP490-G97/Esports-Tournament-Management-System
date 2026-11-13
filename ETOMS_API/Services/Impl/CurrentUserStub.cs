using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// Người dùng hiện tại, đọc theo thứ tự:
//   1. Đăng nhập thật (AUTH-02): JWT đã xác thực, mã tài khoản ở claim "sub".
//   2. Đăng nhập thử (trang /dev-auth): session do DevAuthApiController đặt, chỉ ở môi trường Development.
// Giữ cả hai để nhóm test song song; khi bỏ đăng nhập thử thì xóa nhánh 2.
public class CurrentUserStub : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IWebHostEnvironment _environment;

    public CurrentUserStub(IHttpContextAccessor accessor, IWebHostEnvironment environment)
    {
        _accessor = accessor;
        _environment = environment;
    }

    private int? JwtUserId
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;
            return int.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : null;
        }
    }

    private ISession? Session => _environment.IsDevelopment() ? _accessor.HttpContext?.Session : null;

    public int? UserId => JwtUserId ?? Session?.GetInt32("DemoUserId");

    public string? DisplayName => JwtUserId.HasValue
        ? _accessor.HttpContext?.User.FindFirst("email")?.Value
        : Session?.GetString("DemoUserName");

    public bool IsAuthenticated => UserId.HasValue;
}
