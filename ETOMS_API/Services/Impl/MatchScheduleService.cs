using ETOMS_API.DTOs.Bracket;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module BRK-12 (UC-17 bước 1-4, A1, A3, E1). Ban tổ chức gán giờ thi đấu cho từng trận của nhánh đã khóa.
// Giờ gắn với trận (vị trí trong nhánh), nên trận vòng sau chưa biết đội vẫn xếp được (A1).
// Mỗi lần xếp giờ ghi luôn Match.CheckInOpenAt = giờ thi đấu - 30 phút (BR-MATCH-01): đây là mốc
// để phần check-in (MAT-02) tự mở nút và gửi nhắc. Trận đang mở check-in hoặc đã bắt đầu thì không dời được (A3);
// đã qua giờ mà cả hai đội không check-in thì được xếp lại (BR-MATCH-02).
public class MatchScheduleService : IMatchScheduleService
{
    public static readonly TimeSpan CheckInWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public MatchScheduleService(IUnitOfWork uow) => _uow = uow;

    private sealed class Item
    {
        public Match Match { get; init; } = null!;
        public BracketNode Node { get; init; } = null!;
    }

    private sealed class Context
    {
        public Tournament Tournament { get; init; } = null!;
        public List<Item> Items { get; init; } = new();
        public List<BracketNode> Nodes { get; init; } = new();
        public Dictionary<int, string> TeamNames { get; init; } = new();
        public Dictionary<int, (string Name, bool InApp)> People { get; init; } = new();
        public Dictionary<int, int> FinalRoundByStage { get; init; } = new();
        public int LastKnockoutStage { get; init; }
        public string? BlockedReason { get; init; }
        public DateTime Now { get; init; }
    }

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<MatchScheduleBoardDto?> GetBoardAsync(int tournamentId)
    {
        var ctx = await LoadAsync(tournamentId);
        return ctx is null ? null : ToBoard(ctx);
    }

    // ===== BRK-12: Xếp hoặc dời giờ một trận =====
    public async Task<Result<MatchScheduleBoardDto>> ScheduleMatchAsync(int tournamentId, int matchId, DateTime? scheduledAt)
    {
        var ctx = await LoadAsync(tournamentId);
        if (ctx is null) return Result<MatchScheduleBoardDto>.Failure("Không tìm thấy giải đấu.");
        if (ctx.BlockedReason is not null) return Result<MatchScheduleBoardDto>.Failure(ctx.BlockedReason);

        var item = ctx.Items.FirstOrDefault(i => i.Match.MatchId == matchId);
        if (item is null) return Result<MatchScheduleBoardDto>.Failure("Không tìm thấy trận đấu trong giải này.");

        var locked = LockedReason(ctx, item);
        if (locked is not null) return Result<MatchScheduleBoardDto>.FieldFailure("ScheduledAt", locked);

        var previous = item.Match.ScheduledAt;
        if (scheduledAt is null)
        {
            if (previous is null) return Result<MatchScheduleBoardDto>.Success(ToBoard(ctx), "Trận này chưa có giờ.");
            item.Match.ScheduledAt = null;
            item.Match.CheckInOpenAt = null;
            await NotifyAsync(ctx, item, $"Trận {FullLabel(ctx, item)} của giải {ctx.Tournament.Name} tạm chưa có giờ thi đấu.");
            await _uow.SaveChangesAsync();
            return Result<MatchScheduleBoardDto>.Success(ToBoard(ctx), "Đã bỏ giờ của trận.");
        }

        var at = ToMinute(scheduledAt.Value.ToUniversalTime());
        if (previous == at) return Result<MatchScheduleBoardDto>.Success(ToBoard(ctx), "Trận đã có đúng giờ này.");

        var error = Validate(ctx, item, at);
        if (error is not null) return Result<MatchScheduleBoardDto>.FieldFailure("ScheduledAt", error);

        Apply(item, at);
        await NotifyAsync(ctx, item, ChangeText(ctx, item, previous, at));
        await _uow.SaveChangesAsync();
        return Result<MatchScheduleBoardDto>.Success(ToBoard(ctx),
            previous is null ? "Đã xếp giờ thi đấu. Check-in mở lúc " + Vn(at - CheckInWindow) + "." : "Đã dời giờ thi đấu và báo cho hai đội.");
    }

    // ===== BRK-12: Xếp cả vòng: trận đầu lúc FirstAt, mỗi trận sau cách GapMinutes phút =====
    public async Task<Result<ScheduleRoundResultDto>> ScheduleRoundAsync(int tournamentId, ScheduleRoundRequest request)
    {
        var ctx = await LoadAsync(tournamentId);
        if (ctx is null) return Result<ScheduleRoundResultDto>.Failure("Không tìm thấy giải đấu.");
        if (ctx.BlockedReason is not null) return Result<ScheduleRoundResultDto>.Failure(ctx.BlockedReason);

        // Tranh hạng Ba đá trước chung kết.
        var round = ctx.Items
            .Where(i => i.Node.StageNo == request.StageNo && i.Node.RoundNo == request.RoundNo)
            .OrderBy(i => IsThirdPlace(ctx, i) ? 0 : 1).ThenBy(i => i.Node.GroupLabel).ThenBy(i => i.Node.PositionInRound)
            .ToList();
        if (round.Count == 0) return Result<ScheduleRoundResultDto>.Failure("Vòng này không có trận nào.");

        var result = new ScheduleRoundResultDto();
        var first = ToMinute(request.FirstAt!.Value.ToUniversalTime());
        var slot = 0;
        var changed = new List<(Item Item, DateTime? Previous)>();
        foreach (var item in round)
        {
            var label = FullLabel(ctx, item);
            var locked = LockedReason(ctx, item);
            if (locked is not null) { result.Skipped.Add($"{label}: {locked}"); continue; }

            var at = first.AddMinutes((double)request.GapMinutes * slot);
            slot++;
            if (item.Match.ScheduledAt == at) continue;
            var error = Validate(ctx, item, at);
            if (error is not null) { result.Skipped.Add($"{label}: {error}"); continue; }

            changed.Add((item, item.Match.ScheduledAt));
            Apply(item, at);
        }

        foreach (var (item, previous) in changed)
            await NotifyAsync(ctx, item, ChangeText(ctx, item, previous, item.Match.ScheduledAt!.Value));
        if (changed.Count > 0) await _uow.SaveChangesAsync();

        result.ScheduledCount = changed.Count;
        result.Board = ToBoard(ctx);
        var message = result.Skipped.Count > 0
            ? $"Đã xếp {changed.Count} trận, bỏ qua {result.Skipped.Count} trận."
            : $"Đã xếp {changed.Count} trận.";
        return Result<ScheduleRoundResultDto>.Success(result, message);
    }

    private async Task<Context?> LoadAsync(int tournamentId)
    {
        var t = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (t is null) return null;

        var nodes = await _uow.Repository<BracketNode>().Query().Where(n => n.TournamentId == tournamentId).ToListAsync();
        var matches = await _uow.Repository<Match>().Query().Where(m => m.TournamentId == tournamentId).ToListAsync();
        var nodeById = nodes.ToDictionary(n => n.BracketNodeId);
        var items = matches.Where(m => nodeById.ContainsKey(m.BracketNodeId))
            .Select(m => new Item { Match = m, Node = nodeById[m.BracketNodeId] }).ToList();

        var teamIds = matches.SelectMany(m => new[] { m.TeamAid, m.TeamBid }).Concat(nodes.Select(n => n.SeedTeamId))
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var teams = await _uow.Repository<Team>().Query().Where(x => teamIds.Contains(x.TeamId))
            .Select(x => new { x.TeamId, x.Name, x.CaptainAccountId }).ToListAsync();
        var peopleIds = teams.Select(x => x.CaptainAccountId).Concat(matches.Where(m => m.RefereeAccountId.HasValue).Select(m => m.RefereeAccountId!.Value)).Distinct().ToList();
        var people = await _uow.Repository<Account>().Query().Where(a => peopleIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId, a => (a.FullName, a.NotifyInApp));

        var knockout = nodes.Where(n => n.StageType == "KNOCKOUT").ToList();
        string? blocked = t.Status is "COMPLETED" or "CANCELLED" ? "Giải đã kết thúc hoặc đã hủy nên không xếp lịch được."
            : t.BracketLockedAt is null ? "Nhánh đấu chưa khóa nên chưa xếp lịch được."
            : null;

        return new Context
        {
            Tournament = t,
            Items = items,
            Nodes = nodes,
            TeamNames = teams.ToDictionary(x => x.TeamId, x => x.Name),
            People = people,
            FinalRoundByStage = knockout.GroupBy(n => n.StageNo).ToDictionary(g => g.Key, g => g.Max(n => n.RoundNo ?? 0)),
            LastKnockoutStage = knockout.Count == 0 ? 0 : knockout.Max(n => n.StageNo),
            BlockedReason = blocked,
            Now = DateTime.UtcNow
        };
    }

    private static string? LockedReason(Context ctx, Item item)
    {
        var m = item.Match;
        if (m.Status != "PENDING") return "Trận đã bắt đầu hoặc đã có kết quả nên không đổi giờ được.";
        if (m.CheckInOpenAt is not DateTime open || ctx.Now < open) return null;
        if (m.ScheduledAt is DateTime kickOff && ctx.Now < kickOff) return "Trận đã mở check-in nên không dời giờ được.";
        // Đã qua giờ thi đấu: cả hai đội không check-in thì Ban tổ chức được xếp lại lịch (BR-MATCH-02).
        // Có một đội đã check-in thì đó là thắng trắng, để quy trình vắng mặt (MAT-03) xử lý.
        return m.TeamAcheckedInAt is null && m.TeamBcheckedInAt is null
            ? null
            : "Đã qua giờ thi đấu và có đội đã check-in, trận được xử theo quy trình vắng mặt nên không dời giờ được.";
    }

    private string? Validate(Context ctx, Item item, DateTime at)
    {
        if (at < ctx.Now + CheckInWindow)
            return "Giờ thi đấu phải sau hiện tại ít nhất 30 phút để đủ thời gian check-in.";
        var t = ctx.Tournament;
        var start = DateTime.SpecifyKind(t.StartAt, DateTimeKind.Utc);
        var end = t.EndAt is DateTime e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : (DateTime?)null;
        if (at < start || (end is not null && at > end))
            return end is null
                ? $"Giờ thi đấu phải từ lúc giải khởi tranh ({Vn(start)}) trở đi."
                : $"Giờ thi đấu phải nằm trong thời gian thi đấu của giải, từ {Vn(start)} đến {Vn(end.Value)}.";

        foreach (var feeder in Feeders(ctx, item))
            if (feeder.Match.ScheduledAt is DateTime f && f >= at)
                return $"Trận này phải sau {FullLabel(ctx, feeder)} ({Vn(f)}).";
        foreach (var next in Dependents(ctx, item))
            if (next.Match.ScheduledAt is DateTime n && n <= at)
                return $"Trận này phải trước {FullLabel(ctx, next)} ({Vn(n)}).";

        foreach (var other in ctx.Items.Where(o => o != item && o.Match.ScheduledAt is DateTime))
        {
            var when = other.Match.ScheduledAt!.Value;
            if (other.Node.StageNo < item.Node.StageNo && when >= at)
                return $"Trận của giai đoạn {item.Node.StageNo} phải sau mọi trận giai đoạn {other.Node.StageNo} ({FullLabel(ctx, other)} lúc {Vn(when)}).";
            if (other.Node.StageNo > item.Node.StageNo && when <= at)
                return $"Trận của giai đoạn {item.Node.StageNo} phải trước mọi trận giai đoạn {other.Node.StageNo} ({FullLabel(ctx, other)} lúc {Vn(when)}).";
            if (when != at) continue;

            var shared = new[] { item.Match.TeamAid, item.Match.TeamBid }
                .FirstOrDefault(id => id.HasValue && (other.Match.TeamAid == id || other.Match.TeamBid == id));
            if (shared is int team)
                return $"Đội {TeamName(ctx, team)} đã có trận khác lúc {Vn(at)} ({FullLabel(ctx, other)}).";
            if (item.Match.RefereeAccountId is int referee && other.Match.RefereeAccountId == referee)
                return $"Trọng tài của trận đã được gán vào {FullLabel(ctx, other)} cùng giờ.";
        }
        return null;
    }

    private static void Apply(Item item, DateTime at)
    {
        item.Match.ScheduledAt = at;
        item.Match.CheckInOpenAt = at - CheckInWindow;
    }

    // Các trận dẫn vào trận này. Tranh hạng Ba nhận đội thua của hai trận bán kết.
    private static IEnumerable<Item> Feeders(Context ctx, Item item)
    {
        var childIds = ChildNodes(ctx, item).Select(n => n.BracketNodeId).ToHashSet();
        return ctx.Items.Where(i => childIds.Contains(i.Node.BracketNodeId));
    }

    private static IEnumerable<Item> Dependents(Context ctx, Item item)
    {
        var parent = ctx.Items.FirstOrDefault(i => i.Node.BracketNodeId == item.Node.ParentNodeId);
        if (parent is not null) yield return parent;
        foreach (var third in ctx.Items.Where(i => IsThirdPlace(ctx, i)))
            if (Feeders(ctx, third).Contains(item)) yield return third;
    }

    private static List<BracketNode> ChildNodes(Context ctx, Item item)
    {
        var target = item.Node.BracketNodeId;
        if (IsThirdPlace(ctx, item))
        {
            var final = ctx.Nodes.FirstOrDefault(n => n.StageType == "KNOCKOUT" && n.StageNo == item.Node.StageNo
                && n.RoundNo == item.Node.RoundNo && n.PositionInRound == 1 && n.ParentNodeId == null);
            if (final is null) return new List<BracketNode>();
            target = final.BracketNodeId;
        }
        return ctx.Nodes.Where(n => n.ParentNodeId == target).OrderBy(n => n.PositionInRound).ToList();
    }

    private static bool IsThirdPlace(Context ctx, Item item)
        => ctx.FinalRoundByStage.TryGetValue(item.Node.StageNo, out var finalRound)
           && ThirdPlaceMatch.IsNode(item.Node.StageType, item.Node.StageNo, item.Node.RoundNo, item.Node.PositionInRound, item.Node.ParentNodeId, ctx.LastKnockoutStage, finalRound);

    private static string RoundLabel(Context ctx, BracketNode node)
    {
        if (node.StageType == "GROUP") return $"Vòng bảng · Lượt {node.RoundNo}";
        var final = ctx.FinalRoundByStage.TryGetValue(node.StageNo, out var f) ? f : 0;
        return (final - (node.RoundNo ?? 0)) switch
        {
            0 => "Chung kết",
            1 => "Bán kết",
            2 => "Tứ kết",
            _ => $"Vòng {node.RoundNo}"
        };
    }

    private static string MatchLabel(Context ctx, Item item)
    {
        if (item.Node.StageType == "GROUP") return $"Bảng {item.Node.GroupLabel}";
        if (IsThirdPlace(ctx, item)) return ThirdPlaceMatch.Label;
        if (RoundLabel(ctx, item.Node) == "Chung kết") return "Chung kết";
        return $"Trận {item.Node.PositionInRound}";
    }

    private static string FullLabel(Context ctx, Item item)
    {
        if (IsThirdPlace(ctx, item)) return ThirdPlaceMatch.Label;
        var round = RoundLabel(ctx, item.Node);
        if (round == "Chung kết") return round;
        return round + " · " + MatchLabel(ctx, item);
    }

    // Đội chưa biết thì ghi rõ chờ ai: thắng (hoặc thua, với tranh hạng Ba) trận nào.
    private static string SideName(Context ctx, Item item, bool sideA)
    {
        var id = sideA ? item.Match.TeamAid : item.Match.TeamBid;
        if (id is int known) return TeamName(ctx, known);

        var children = ChildNodes(ctx, item);
        var child = children.ElementAtOrDefault(sideA ? 0 : 1);
        if (child is null) return "Chưa xác định";
        if (child.IsBye && child.SeedTeamId is int seed) return TeamName(ctx, seed);
        var childItem = ctx.Items.FirstOrDefault(i => i.Node.BracketNodeId == child.BracketNodeId);
        if (childItem is null) return "Chưa xác định";
        return (IsThirdPlace(ctx, item) ? "Thua " : "Thắng ") + FullLabel(ctx, childItem);
    }

    private static string TeamName(Context ctx, int teamId) => ctx.TeamNames.TryGetValue(teamId, out var n) ? n : "Đội #" + teamId;

    private string ChangeText(Context ctx, Item item, DateTime? previous, DateTime at)
        => previous is null
            ? $"Trận {FullLabel(ctx, item)} của giải {ctx.Tournament.Name} thi đấu lúc {Vn(at)}. Check-in mở lúc {Vn(at - CheckInWindow)}."
            : $"Trận {FullLabel(ctx, item)} của giải {ctx.Tournament.Name} dời từ {Vn(previous.Value)} sang {Vn(at)}. Check-in mở lúc {Vn(at - CheckInWindow)}.";

    // Báo đội trưởng của đội đã biết và trọng tài của trận (nếu bật thông báo trong ứng dụng).
    private async Task NotifyAsync(Context ctx, Item item, string body)
    {
        var captains = await _uow.Repository<Team>().Query()
            .Where(x => x.TeamId == item.Match.TeamAid || x.TeamId == item.Match.TeamBid)
            .Select(x => x.CaptainAccountId)
            .ToListAsync();
        var recipients = captains.ToList();
        if (item.Match.RefereeAccountId is int referee) recipients.Add(referee);

        foreach (var accountId in recipients.Distinct())
        {
            if (ctx.People.TryGetValue(accountId, out var person) && !person.InApp) continue;
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                AccountId = accountId,
                NotificationType = "SYSTEM",
                Title = "Lịch thi đấu",
                Body = body,
                LinkUrl = "/tournament/schedule.html?id=" + ctx.Tournament.TournamentId,
                Channel = "IN_APP",
                IsRead = false,
                SentAt = ctx.Now
            });
        }
    }

    private MatchScheduleBoardDto ToBoard(Context ctx)
    {
        var rounds = ctx.Items
            .GroupBy(i => (i.Node.StageNo, RoundNo: i.Node.RoundNo ?? 0))
            .OrderBy(g => g.Key.StageNo).ThenBy(g => g.Key.RoundNo)
            .Select(g => new ScheduleRoundDto
            {
                StageNo = g.Key.StageNo,
                RoundNo = g.Key.RoundNo,
                Label = (ctx.Items.Select(i => i.Node.StageNo).Distinct().Count() > 1 ? $"Giai đoạn {g.Key.StageNo} · " : "") + RoundLabel(ctx, g.First().Node),
                Matches = g.OrderBy(i => i.Node.GroupLabel).ThenBy(i => i.Node.PositionInRound).Select(i =>
                {
                    var locked = ctx.BlockedReason ?? LockedReason(ctx, i);
                    return new ScheduleMatchDto
                    {
                        MatchId = i.Match.MatchId,
                        Label = MatchLabel(ctx, i),
                        TeamAName = SideName(ctx, i, true),
                        TeamBName = SideName(ctx, i, false),
                        ScheduledAt = i.Match.ScheduledAt,
                        CheckInOpenAt = i.Match.CheckInOpenAt,
                        StatusLabel = StatusText.Match(i.Match.Status),
                        StatusClass = StatusText.MatchClass(i.Match.Status),
                        RefereeName = i.Match.RefereeAccountId is int r && ctx.People.TryGetValue(r, out var p) ? p.Name : null,
                        CanEdit = locked is null,
                        LockedReason = locked
                    };
                }).ToList()
            }).ToList();

        return new MatchScheduleBoardDto
        {
            TournamentId = ctx.Tournament.TournamentId,
            TournamentName = ctx.Tournament.Name,
            StartAt = ctx.Tournament.StartAt,
            EndAt = ctx.Tournament.EndAt,
            CanEdit = ctx.BlockedReason is null,
            BlockedReason = ctx.BlockedReason,
            MatchCount = ctx.Items.Count,
            UnscheduledCount = ctx.Items.Count(i => i.Match.ScheduledAt is null),
            Rounds = rounds
        };
    }

    private static DateTime ToMinute(DateTime utc)
        => new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    // Giờ trong thông báo theo giờ Việt Nam (UTC+7, không đổi giờ theo mùa).
    private static string Vn(DateTime utc) => (utc + VietnamOffset).ToString("HH:mm dd/MM/yyyy");
}
