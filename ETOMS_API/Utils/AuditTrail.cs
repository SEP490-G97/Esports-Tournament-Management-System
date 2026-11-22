using System.Security.Claims;
using ETOMS_API.Models;
using ETOMS_API.Repositories;

namespace ETOMS_API.Utils;

// BR-ADM-01 / UC-43: mọi thay đổi có thẩm quyền để lại một dòng AuditLog. Dùng cho các controller quản trị ghi thẳng
// EtomsDbContext (tựa game, biểu ngữ, thông báo hệ thống). Thêm dòng vào context — lưu cùng SaveChanges của thao tác.
public static class AuditTrail
{
    public static int? ActorId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }

    public static void Add(EtomsDbContext db, ClaimsPrincipal user, string actionType, string objectType, int? objectId,
        string? before = null, string? after = null, string? reason = null, int? tournamentId = null)
        => db.AuditLogs.Add(new AuditLog
        {
            ActorAccountId = ActorId(user),
            TournamentId = tournamentId,
            ActionType = actionType,
            ObjectType = objectType,
            ObjectId = objectId,
            BeforeValue = before,
            AfterValue = after,
            Reason = reason is { Length: > 1000 } ? reason[..1000] : reason,
            CreatedAt = DateTime.UtcNow
        });
}
