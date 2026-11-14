using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ETOMS_API.Services.Impl
{
    /// <summary>Ghi AuditLog cho các thao tác admin. Chỉ Add vào context; người gọi tự SaveChanges (cùng giao dịch).</summary>
    internal static class AdminAudit
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string? ToJson(object? value) => value == null ? null : JsonSerializer.Serialize(value, Json);

        public static void Add(DbContext db, int actorId, string actionType, string objectType, int? objectId,
            object? before, object? after, string? reason)
        {
            db.Set<AuditLog>().Add(new AuditLog
            {
                ActorAccountId = actorId,
                TournamentId = null,
                ActionType = actionType,
                ObjectType = objectType,
                ObjectId = objectId,
                BeforeValue = ToJson(before),
                AfterValue = ToJson(after),
                Reason = Truncate(reason, 1000),
                CreatedAt = DateTime.UtcNow
            });
        }

        public static string? Truncate(string? s, int max) =>
            s == null ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}
