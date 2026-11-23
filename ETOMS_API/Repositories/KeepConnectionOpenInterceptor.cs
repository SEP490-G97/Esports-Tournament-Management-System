using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ETOMS_API.Repositories;

// Mặc định EF mở/đóng kết nối quanh MỖI câu SQL. Với MySql.Data, mỗi lần lấy lại kết nối từ pool
// tốn thêm ~2 vòng mạng; database ở xa (~220 ms/vòng) nên 1 API 5 câu SQL mất gấp đôi thời gian.
// Interceptor này bỏ qua lệnh đóng: kết nối mở ở câu SQL đầu tiên và giữ đến hết request
// (EtomsDbContext là scoped, khi dispose sẽ trả kết nối về pool). Request không chạy SQL thì không mở kết nối.
// Worker nền tạo scope mới mỗi lượt nên cũng chỉ giữ kết nối trong một lượt.
public sealed class KeepConnectionOpenInterceptor : DbConnectionInterceptor
{
    // Tự mở kết nối KHÔNG kèm token huỷ của request. Bắt tay SSL tới database ở xa mất 3–4 giây; nếu người dùng
    // tải lại/chuyển trang đúng lúc đó, MySql.Data ném IOException ngay trong callback huỷ → Kestrel ghi
    // "An unhandled exception was thrown by the application ... Authentication to host failed (I/O error)".
    // Mở nốt cho xong thì kết nối vẫn vào nhóm WarmConnectionPool dùng lại được; câu SQL sau đó vẫn bị huỷ đúng như cũ.
    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        connection.Open();
        return InterceptionResult.Suppress();
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        await connection.OpenAsync(CancellationToken.None);
        return InterceptionResult.Suppress();
    }

    public override InterceptionResult ConnectionClosing(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => InterceptionResult.Suppress();

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => ValueTask.FromResult(InterceptionResult.Suppress());
}
