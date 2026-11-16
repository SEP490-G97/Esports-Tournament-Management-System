using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// POR-02 / UC-36: danh sách & tìm kiếm giải công khai (khách xem được, không bao giờ có giải Nháp).
public interface ITournamentDirectoryService
{
    Task<Result<TournamentDirectoryPage>> SearchAsync(TournamentDirectoryQuery query, CancellationToken ct);
    Task<TournamentDirectoryFilters> GetFiltersAsync(CancellationToken ct);
}
