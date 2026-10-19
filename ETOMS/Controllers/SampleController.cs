using ETOMS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS.Controllers;

// VÍ DỤ MẪU API Controller. Controller mỏng: chỉ gọi Service, trả DTO. Test ngay trên /swagger.
// Controller giao diện (trả View) thì kế thừa Controller thay vì ControllerBase.
[ApiController]
[Route("api/[controller]")]
public class SampleController : ControllerBase
{
    private readonly ISampleService _service;

    public SampleController(ISampleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Application.DTOs.SampleItemDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }
}
