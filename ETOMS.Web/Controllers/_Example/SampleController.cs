using ETOMS.Application._Example;
using Microsoft.AspNetCore.Mvc;

namespace ETOMS.Web.Controllers._Example;

// VÍ DỤ MẪU — Controller đặt ở Controllers/<MÃ_MODULE>/.
// Controller MỎNG: chỉ nhận request, gọi Service, trả DTO. Không chứa nghiệp vụ,
// không đụng thẳng DbContext. Test API này ngay trên /swagger.
[ApiController]
[Route("api/[controller]")]
public class SampleController : ControllerBase
{
    private readonly ISampleService _service;

    public SampleController(ISampleService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SampleDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SampleDto>> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }
}
