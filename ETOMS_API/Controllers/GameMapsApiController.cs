using ETOMS_API.DTOs.GameRule;
using ETOMS_API.Services;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// API JSON cho GRL-02: kho bản đồ của tựa game. Chỉ System Admin.
[Route("api/game-rules/{gameId:int}/maps")]
public class GameMapsApiController : ApiControllerBase
{
    private readonly IGameMapService _mapService;
    private readonly IGameRuleService _gameRuleService;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _env;

    public GameMapsApiController(IGameMapService mapService, IGameRuleService gameRuleService, ICurrentUser currentUser, IWebHostEnvironment env)
    {
        _mapService = mapService;
        _gameRuleService = gameRuleService;
        _currentUser = currentUser;
        _env = env;
    }

    // GET /api/game-rules/{gameId}/maps
    [HttpGet]
    public async Task<IActionResult> GetAll(int gameId)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;
        return FromResult(await _mapService.GetMapsAsync(gameId));
    }

    // POST /api/game-rules/{gameId}/maps — multipart: Name, Image (tùy chọn).
    [HttpPost]
    public async Task<IActionResult> Add(int gameId, [FromForm] SaveGameMapRequest request, IFormFile? image)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;

        var upload = ImageUploader.Save(image, _env, "maps");
        if (!upload.Ok) return FromResult(Result.FieldFailure("Image", upload.Error!));

        var result = await _mapService.AddMapAsync(gameId, request, upload.Url);
        if (!result.IsSuccess) DeleteUpload(upload.Url);
        return FromResult(result);
    }

    // POST /api/game-rules/{gameId}/maps/{mapId} — multipart: Name, Status, Image (tùy chọn).
    // Dùng POST vì api.js dùng chung chỉ có postForm cho multipart.
    [HttpPost("{mapId:int}")]
    public async Task<IActionResult> Update(int gameId, int mapId, [FromForm] SaveGameMapRequest request, IFormFile? image)
    {
        var denied = await CheckAdminAsync();
        if (denied is not null) return denied;

        var upload = ImageUploader.Save(image, _env, "maps");
        if (!upload.Ok) return FromResult(Result.FieldFailure("Image", upload.Error!));

        var result = await _mapService.UpdateMapAsync(gameId, mapId, request, upload.Url);
        if (!result.IsSuccess) DeleteUpload(upload.Url);
        return FromResult(result);
    }

    private async Task<IActionResult?> CheckAdminAsync()
    {
        if (!_currentUser.IsAuthenticated) return Unauthorized();
        return await _gameRuleService.IsAdminAsync(_currentUser.UserId!.Value) ? null : StatusCode(403);
    }

    // Lưu thất bại thì bỏ ảnh vừa tải lên để không để lại file mồ côi.
    private void DeleteUpload(string? url)
    {
        if (url is null) return;
        var path = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}
