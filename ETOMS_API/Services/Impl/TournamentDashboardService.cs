using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.EntityFrameworkCore;

namespace ETOMS_API.Services.Impl;

// Module TOUR-12. Chỉ đọc số liệu; duyệt đơn, xếp lịch, xử tranh chấp nằm ở các module khác.
public class TournamentDashboardService : ITournamentDashboardService
{
    private const int ListLimit = 10;
    private const string UnknownTeam = "Chờ xác định";

    // "Hôm nay" tính theo giờ Việt Nam vì DB lưu giờ UTC.
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly IUnitOfWork _uow;

    public TournamentDashboardService(IUnitOfWork uow) => _uow = uow;

    public async Task<int?> GetOrganizerAccountIdAsync(int tournamentId)
        => (await _uow.Repository<Tournament>().GetByIdAsync(tournamentId))?.OrganizerAccountId;

    public async Task<TournamentDashboardDto?> GetDashboardAsync(int tournamentId)
    {
        var tournament = await _uow.Repository<Tournament>().GetByIdAsync(tournamentId);
        if (tournament is null) return null;

        var dto = new TournamentDashboardDto
        {
            TournamentId = tournament.TournamentId,
            TournamentName = tournament.Name,
            StatusLabel = StatusText.Tournament(tournament.Status),
            StatusClass = StatusText.TournamentClass(tournament.Status),
            MaxTeams = tournament.MaxTeams,
            HasEntryFee = tournament.EntryFee > 0
        };

        await FillRegistrationsAsync(dto, tournamentId);
        await FillMatchesAsync(dto, tournamentId);
        await FillDisputesAsync(dto, tournamentId);
        await FillPaymentsAsync(dto, tournamentId);
        return dto;
    }

    private async Task FillRegistrationsAsync(TournamentDashboardDto dto, int tournamentId)
    {
        // Đơn nháp chỉ đội trưởng thấy nên không tính.
        var counts = await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId && r.Status != "DRAFT")
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountOf(string status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        dto.PendingCount = CountOf("PENDING");
        dto.ApprovedCount = CountOf("APPROVED");
        dto.WaitlistedCount = CountOf("WAITLISTED");
        dto.RemainingSlots = Math.Max(dto.MaxTeams - dto.ApprovedCount, 0);
        dto.RegistrationsByStatus = counts
            .OrderByDescending(c => c.Count)
            .Select(c => new DashboardCountDto { Label = StatusText.Registration(c.Status), Count = c.Count })
            .ToList();

        dto.PendingRegistrations = await _uow.Repository<Registration>().Query()
            .Where(r => r.TournamentId == tournamentId && r.Status == "PENDING")
            .OrderBy(r => r.SubmittedAt)
            .Take(ListLimit)
            .Select(r => new DashboardRegistrationDto
            {
                RegistrationId = r.RegistrationId,
                TeamName = r.Team.Name,
                SubmittedAt = r.SubmittedAt
            })
            .ToListAsync();
    }

    private async Task FillMatchesAsync(TournamentDashboardDto dto, int tournamentId)
    {
        var counts = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        dto.TotalMatches = counts.Sum(c => c.Count);
        dto.MatchesByStatus = counts
            .OrderByDescending(c => c.Count)
            .Select(c => new DashboardCountDto { Label = StatusText.Match(c.Status), Count = c.Count })
            .ToList();

        var now = DateTime.UtcNow;
        var checkIns = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId
                && m.CheckInOpenAt != null && m.CheckInOpenAt <= now && m.ScheduledAt > now)
            .Select(m => new { m.TeamAid, m.TeamBid, m.TeamAcheckedInAt, m.TeamBcheckedInAt })
            .ToListAsync();

        dto.CheckInMatchCount = checkIns.Count;
        dto.ExpectedTeamCount = checkIns.Sum(m => (m.TeamAid is null ? 0 : 1) + (m.TeamBid is null ? 0 : 1));
        dto.CheckedInTeamCount = checkIns.Sum(m => (m.TeamAcheckedInAt is null ? 0 : 1) + (m.TeamBcheckedInAt is null ? 0 : 1));

        var dayStart = (now + VietnamOffset).Date - VietnamOffset;
        var dayEnd = dayStart.AddDays(1);
        var today = await _uow.Repository<Match>().Query()
            .Where(m => m.TournamentId == tournamentId && m.ScheduledAt >= dayStart && m.ScheduledAt < dayEnd)
            .OrderBy(m => m.ScheduledAt)
            .Take(ListLimit)
            .Select(m => new
            {
                m.MatchId,
                TeamA = m.TeamA != null ? m.TeamA.Name : null,
                TeamB = m.TeamB != null ? m.TeamB.Name : null,
                m.ScheduledAt,
                m.Status
            })
            .ToListAsync();

        dto.TodayMatches = today.Select(m => new DashboardMatchDto
        {
            MatchId = m.MatchId,
            TeamAName = m.TeamA ?? UnknownTeam,
            TeamBName = m.TeamB ?? UnknownTeam,
            ScheduledAt = m.ScheduledAt,
            StatusLabel = StatusText.Match(m.Status),
            StatusClass = StatusText.MatchClass(m.Status)
        }).ToList();
    }

    private async Task FillDisputesAsync(TournamentDashboardDto dto, int tournamentId)
    {
        var disputes = await _uow.Repository<Dispute>().Query()
            .Where(d => d.Match.TournamentId == tournamentId
                && (d.Status == "OPEN" || d.Status == "UNDER_REVIEW" || d.AppealStatus == "PENDING"))
            .OrderBy(d => d.OpenedAt)
            .Select(d => new
            {
                d.DisputeId,
                d.MatchId,
                TeamA = d.Match.TeamA != null ? d.Match.TeamA.Name : null,
                TeamB = d.Match.TeamB != null ? d.Match.TeamB.Name : null,
                d.OpenReason,
                d.AppealStatus,
                d.OpenedAt
            })
            .ToListAsync();

        dto.OpenDisputeCount = disputes.Count;
        dto.PendingAppealCount = disputes.Count(d => d.AppealStatus == "PENDING");
        dto.OpenDisputes = disputes.Take(ListLimit).Select(d => new DashboardDisputeDto
        {
            DisputeId = d.DisputeId,
            MatchId = d.MatchId,
            TeamAName = d.TeamA ?? UnknownTeam,
            TeamBName = d.TeamB ?? UnknownTeam,
            ReasonLabel = StatusText.DisputeReason(d.OpenReason),
            HasPendingAppeal = d.AppealStatus == "PENDING",
            OpenedAt = d.OpenedAt
        }).ToList();
    }

    private async Task FillPaymentsAsync(TournamentDashboardDto dto, int tournamentId)
    {
        var confirmed = await _uow.Repository<PaymentRecord>().Query()
            .Where(p => p.RecordKind == "PROOF" && p.IsCurrent
                && p.Registration.TournamentId == tournamentId && p.Registration.PaymentStatus == "CONFIRMED")
            .Select(p => p.DeclaredAmount)
            .ToListAsync();

        dto.ConfirmedPaymentCount = confirmed.Count;
        dto.MissingAmountCount = confirmed.Count(a => a is null);
        dto.ConfirmedFeeTotal = confirmed.Sum(a => a ?? 0);
    }
}
