namespace ETOMS.Application._Example;

// VÍ DỤ MẪU — implementation đặt ở Services/<MÃ_MODULE>/.
// Thực tế sẽ inject IGenericRepository<Entity> (trong ETOMS.Infrastructure)
// qua constructor, lấy entity rồi MAP sang DTO. Ở đây để dữ liệu giả cho dễ hiểu.
public class SampleService : ISampleService
{
    private static readonly List<SampleDto> _demo = new()
    {
        new SampleDto { Id = 1, Ten = "Mẫu A" },
        new SampleDto { Id = 2, Ten = "Mẫu B" },
    };

    public Task<IReadOnlyList<SampleDto>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<SampleDto>>(_demo);

    public Task<SampleDto?> GetByIdAsync(int id)
        => Task.FromResult(_demo.FirstOrDefault(x => x.Id == id));
}
