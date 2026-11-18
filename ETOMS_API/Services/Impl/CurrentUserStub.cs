using ETOMS_API.Services.Interface;

namespace ETOMS_API.Services.Impl;

// Người dùng hiện tại = JWT đã xác thực (AUTH-02), mã tài khoản ở claim "sub".
public class CurrentUserStub : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserStub(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private System.Security.Claims.ClaimsPrincipal? User =>
        _accessor.HttpContext?.User.Identity?.IsAuthenticated == true ? _accessor.HttpContext.User : null;

    public int? UserId => int.TryParse(User?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? DisplayName => User?.FindFirst("email")?.Value;

    public bool IsAuthenticated => UserId.HasValue;
}
