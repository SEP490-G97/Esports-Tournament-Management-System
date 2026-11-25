using System.Collections.Concurrent;

namespace ETOMS_API.Utils;

// Khóa thao tác nhánh đấu theo từng giải (trong tiến trình): bấm "Sinh nhánh" nhiều lần liên tiếp hoặc mở 2 tab
// thì yêu cầu sau bị từ chối ngay thay vì chạy song song với yêu cầu trước (hai lượt cùng xóa cây cũ rồi cùng dựng
// cây mới -> lỗi giữa chừng hoặc ra 2 cây). Server ETOMS chạy một tiến trình nên khóa trong bộ nhớ là đủ.
public static class TournamentOpLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    public const string BusyMessage = "Thao tác trước trên nhánh đấu của giải này vẫn đang xử lý, vui lòng đợi vài giây rồi thử lại.";

    // Trả về đối tượng phải Dispose để nhả khóa; null nếu đang có thao tác khác trên cùng giải.
    public static IDisposable? TryEnter(int tournamentId)
    {
        var sem = Locks.GetOrAdd(tournamentId, _ => new SemaphoreSlim(1, 1));
        return sem.Wait(0) ? new Releaser(sem) : null;
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        private int _released;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) sem.Release(); }
    }
}
