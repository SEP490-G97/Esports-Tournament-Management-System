using ETOMS_API.Utils;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS_API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
        => result.IsSuccess
            ? Ok(new { message = result.Message })
            : StatusCode(result.StatusCode, new { message = result.Message, fieldErrors = result.FieldErrors });

    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess
            ? Ok(new { message = result.Message, data = result.Data })
            : StatusCode(result.StatusCode, new { message = result.Message, fieldErrors = result.FieldErrors });
}
