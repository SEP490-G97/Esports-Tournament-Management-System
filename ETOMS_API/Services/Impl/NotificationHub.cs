using ETOMS_API.DTOs;
using ETOMS_API.Services.Interface;
using System.Threading.Channels;

namespace ETOMS_API.Services.Impl
{
    public sealed class NotificationHub : INotificationHub
    {
        public const int MaxConnectionsPerAccount = 8;
        private const int ChannelCapacity = 100;

        private sealed record Conn(Channel<UserNotificationItem> Channel);

        private readonly object _gate = new();
        private readonly Dictionary<int, List<Conn>> _map = new();

        public int ConnectionCount { get { lock (_gate) return _map.Values.Sum(l => l.Count); } }

        public NotificationSubscription Subscribe(int accountId)
        {
            var conn = new Conn(Channel.CreateBounded<UserNotificationItem>(
                new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true }));

            lock (_gate)
            {
                if (!_map.TryGetValue(accountId, out var list)) _map[accountId] = list = new List<Conn>();
                while (list.Count >= MaxConnectionsPerAccount)
                {
                    list[0].Channel.Writer.TryComplete(); // đóng kết nối cũ nhất (mở quá nhiều tab)
                    list.RemoveAt(0);
                }
                list.Add(conn);
            }

            return new NotificationSubscription(conn.Channel.Reader, () =>
            {
                lock (_gate)
                {
                    if (_map.TryGetValue(accountId, out var list))
                    {
                        list.Remove(conn);
                        if (list.Count == 0) _map.Remove(accountId);
                    }
                }
                conn.Channel.Writer.TryComplete();
            });
        }

        public void Publish(int accountId, UserNotificationItem item)
        {
            Conn[] targets;
            lock (_gate)
            {
                if (!_map.TryGetValue(accountId, out var list) || list.Count == 0) return;
                targets = list.ToArray();
            }
            foreach (var c in targets) c.Channel.Writer.TryWrite(item);
        }
    }
}
