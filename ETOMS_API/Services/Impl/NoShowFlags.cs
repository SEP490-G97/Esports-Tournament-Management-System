using System.Text.Json;
using ETOMS_API.Models;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

public enum NoShowKind { None, Single, Double }

/// <summary>Cờ No-show còn hiệu lực của một trận (suy ra từ AuditLog do <see cref="NoShowService"/> ghi và trạng thái hiện tại của trận).</summary>
public record NoShowFlag(NoShowKind Kind, IReadOnlyList<int> TeamIds)
{
    public static readonly NoShowFlag None = new(NoShowKind.None, Array.Empty<int>());
}

internal static class NoShowFlags
{
    public static async Task<NoShowFlag> ReadAsync(DbContext db, int matchId, string? matchStatus, CancellationToken ct)
    {
        if (matchStatus != "WALKOVER" && matchStatus != "DISPUTED") return NoShowFlag.None;

        var after = await db.Set<AuditLog>().AsNoTracking()
            .Where(a => a.ObjectType == "MATCH" && a.ObjectId == matchId && a.ActionType == "WALKOVER")
            .OrderByDescending(a => a.AuditLogId).Select(a => a.AfterValue).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(after)) return NoShowFlag.None;

        try
        {
            using var doc = JsonDocument.Parse(after);
            var root = doc.RootElement;
            if (!root.TryGetProperty("reason", out var r)) return NoShowFlag.None;
            root.TryGetProperty("detail", out var d);
            var reason = r.GetString();

            if (reason == "NO_SHOW" && matchStatus == "WALKOVER" && d.ValueKind == JsonValueKind.Object
                && d.TryGetProperty("noShowTeamId", out var one) && one.TryGetInt32(out var id))
                return new NoShowFlag(NoShowKind.Single, new[] { id });

            if (reason == "DOUBLE_NO_SHOW" && matchStatus == "DISPUTED" && d.ValueKind == JsonValueKind.Object
                && d.TryGetProperty("noShowTeamIds", out var many) && many.ValueKind == JsonValueKind.Array)
                return new NoShowFlag(NoShowKind.Double, many.EnumerateArray().Select(x => x.GetInt32()).ToList());
        }
        catch (JsonException) { /* dữ liệu audit lạ → coi như không có cờ */ }
        return NoShowFlag.None;
    }
}