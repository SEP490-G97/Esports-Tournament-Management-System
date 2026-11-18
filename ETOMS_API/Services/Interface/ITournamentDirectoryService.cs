using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// POR-02 / UC-36: danh sách & tìm kiếm giải công khai (khách xem được, không bao giờ có giải Nháp).
public interface ITournamentDirectoryService
{
    Task<Result<TournamentDirectoryPage>> SearchAsync(TournamentDirectoryQuery query, CancellationToken ct);
    Task<TournamentDirectoryFilters> GetFiltersAsync(CancellationToken ct);

    // POR-03: chi tiết giải. Giải Nháp chỉ Ban tổ chức của giải xem trước được (UC-36 E2).
    Task<Result<TournamentOverviewDto>> GetOverviewAsync(int tournamentId, int? viewerAccountId, CancellationToken ct);

    // POR-04: đội đã duyệt + danh sách chờ.
    Task<Result<TournamentParticipantsDto>> GetParticipantsAsync(int tournamentId, int? viewerAccountId, CancellationToken ct);
}
