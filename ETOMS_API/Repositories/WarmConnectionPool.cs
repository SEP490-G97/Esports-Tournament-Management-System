using System.Collections.Concurrent;
using System.Data;
using MySql.Data.MySqlClient;

namespace ETOMS_API.Repositories;

// Database dùng chung ở xa (~160–240 ms mỗi lượt đi về). Đo thực tế: mỗi lần MySql.Data lấy kết nối từ pool của nó
// tốn thêm ~2 lượt (kiểm tra/khởi tạo lại kết nối), nên một API chỉ 1 câu SQL vẫn mất ~3 lượt.
// Nhóm này giữ sẵn tối đa MaxIdle kết nối ĐANG MỞ: mỗi request mượn một kết nối, chạy SQL ngay (1 lượt/câu), xong thì
// trả lại. Kết nối nhàn rỗi quá MaxIdleAge thì bỏ và mở mới, để không dùng phải kết nối đã bị mạng/máy chủ cắt
// (MySQL tự cắt sau wait_timeout = 8 giờ). Kết nối hỏng hoặc còn giao dịch dở thì không trả lại nhóm.
public sealed class WarmConnectionPool : IDisposable
{
    private const int MaxIdle = 16;
    private static readonly TimeSpan MaxIdleAge = TimeSpan.FromMinutes(5);
    private readonly string _connectionString;
    private readonly ConcurrentQueue<(MySqlConnection Conn, DateTime ReturnedAt)> _idle = new();

    public WarmConnectionPool(string connectionString) => _connectionString = connectionString;

    public MySqlConnection Rent()
    {
        while (_idle.TryDequeue(out var item))
        {
            if (item.Conn.State == ConnectionState.Open && DateTime.UtcNow - item.ReturnedAt < MaxIdleAge)
                return item.Conn;
            item.Conn.Dispose(); // quá cũ hoặc đã đóng → bỏ
        }
        return new MySqlConnection(_connectionString); // EF tự mở ở câu SQL đầu tiên
    }

    public void Return(MySqlConnection conn)
    {
        if (conn.State == ConnectionState.Open && _idle.Count < MaxIdle)
            _idle.Enqueue((conn, DateTime.UtcNow));
        else
            conn.Dispose();
    }

    public void Dispose()
    {
        while (_idle.TryDequeue(out var item)) item.Conn.Dispose();
    }
}

// Một lần mượn cho một request (scoped): DbContext của request dùng kết nối này, hết request thì trả lại nhóm.
public sealed class WarmConnectionLease : IDisposable
{
    private readonly WarmConnectionPool _pool;
    public MySqlConnection Connection { get; }

    public WarmConnectionLease(WarmConnectionPool pool)
    {
        _pool = pool;
        Connection = pool.Rent();
    }

    public void Dispose() => _pool.Return(Connection);
}
