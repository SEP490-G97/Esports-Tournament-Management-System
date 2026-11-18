using Microsoft.AspNetCore.Http;

namespace ETOMS_API.Utils;

/// <summary>
/// Người dùng refresh / đóng tab / chuyển trang → trình duyệt hủy request → RequestAborted bị hủy và các lệnh
/// EF/HTTP đang chờ ném TaskCanceledException. Đó là bình thường, không phải lỗi máy chủ: nuốt riêng trường hợp này
/// (trả 499, không ai đọc) để log/trình gỡ lỗi không bị nhiễu. Lỗi hủy do nguyên nhân khác (timeout nội bộ...) vẫn ném ra.
/// Đăng ký: app.UseMiddleware&lt;RequestAbortedMiddleware&gt;(); ngay sau UseExceptionHandler (nếu có), trước routing/controllers.
/// </summary>
public class RequestAbortedMiddleware
{
    public const int ClientClosedRequest = 499;
    private readonly RequestDelegate _next;

    public RequestAbortedMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted) context.Response.StatusCode = ClientClosedRequest;
        }
    }
}