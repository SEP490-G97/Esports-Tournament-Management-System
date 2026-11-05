using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

// Base dùng chung cho các API controller JSON (song song với MVC, không thay thế).
// Quy ước response lỗi: { message, fieldErrors } để client (SPA/mobile sau này) gắn
// lỗi vào đúng field, giống cách MVC đang dùng Result.FieldErrors cho ModelState.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result r)
        => r.IsSuccess ? Ok(new { message = r.Message }) : BadRequest(ToError(r));

    protected IActionResult FromResult<T>(Result<T> r)
        => r.IsSuccess ? Ok(new { message = r.Message, data = r.Data }) : BadRequest(ToError(r));

    private static object ToError(Result r) => new { message = r.Message, fieldErrors = r.FieldErrors };
}
