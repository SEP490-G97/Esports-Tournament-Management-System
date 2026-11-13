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
    public override InterceptionResult ConnectionClosing(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => InterceptionResult.Suppress();

    public override ValueTask<InterceptionResult> ConnectionClosingAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        => ValueTask.FromResult(InterceptionResult.Suppress());
}
