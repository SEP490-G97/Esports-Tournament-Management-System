using ETOMS_API.Repositories.Interface;
using ETOMS_API.Utils;

namespace ETOMS_API.Middleware
{
    public class BanCheckMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext ctx, IAccountRepository accounts)
        {
            if (ctx.User.Identity?.IsAuthenticated == true
                && int.TryParse(ctx.User.FindFirst("sub")?.Value, out var id))
            {
                var info = await accounts.GetStatusAsync(id, ctx.RequestAborted);

                if (info is null)
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
                if (info.Status == AccountStatuses.Banned)
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        code = "ACCOUNT_BANNED",
                        message = "Tài khoản của bạn đã bị khóa",
                        reason = string.IsNullOrWhiteSpace(info.BanReason)
                            ? "Không có lý do được ghi nhận" : info.BanReason,
                    });
                    return;
                }
            }
            await next(ctx);
        }
    }
}
