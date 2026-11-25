using ETOMS_API.Repositories.Interface;
using ETOMS_API.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace ETOMS_API.Middleware
{
    public class BanCheckMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext ctx, IAccountRepository accounts, IMemoryCache cache)
        {
            if (ctx.User.Identity?.IsAuthenticated == true
                && int.TryParse(ctx.User.FindFirst("sub")?.Value, out var id)
                // Vừa kiểm tra trong 30 giây gần đây là đang hoạt động → khỏi hỏi lại database (AccountStatusCache).
                && !cache.TryGetValue(AccountStatusCache.Key(id), out _))
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
                cache.Set(AccountStatusCache.Key(id), true, AccountStatusCache.ActiveTtl);
            }
            await next(ctx);
        }
    }
}
