using ETOMS_API.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers
{
    [Route("api/admin/stats")]
    public class AdminStatsController : AdminControllerBase
    {
        private readonly IAdminStatsService _stats;

        public AdminStatsController(IAdminStatsService stats) => _stats = stats;

        /// <summary>GET /api/admin/stats/overview?days=30&amp;tzOffset=420</summary>
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] int days = 30, [FromQuery] int tzOffset = 0, CancellationToken ct = default)
        {
            var r = await _stats.GetOverviewAsync(days, tzOffset, ct);
            if (r.Error == StatsError.InvalidRange)
                return Err(400, "INVALID_RANGE", "Khoảng thời gian phải từ 7 đến 365 ngày.");

            Response.Headers.CacheControl = "no-store";
            return Ok(r.Data);
        }
    }
}
