using ETOMS_API.DTOs.Tournament;
using ETOMS_API.Utils;

namespace ETOMS_API.Services.Interface;

// TOUR-08 / UC-13: Soạn thể lệ giải theo phiên bản.
public interface IRulebookService
{
    // null = không có giải, false = có giải nhưng không phải Ban tổ chức của giải đó.
    Task<bool?> IsOrganizerAsync(int tournamentId, int accountId);

    // version = null lấy bản hiện hành. Giải còn nháp chỉ Ban tổ chức của giải đó xem được.
    Task<RulebookDto?> GetRulebookAsync(int tournamentId, int? version, int? viewerAccountId);

    Task<IReadOnlyList<RulebookVersionDto>?> GetVersionsAsync(int tournamentId, int? viewerAccountId);

    Task<Result<RulebookSavedDto>> SaveRulebookAsync(int tournamentId, SaveRulebookRequest request, int organizerAccountId);
}
