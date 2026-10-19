namespace ETOMS.Application._Example;

// VÍ DỤ MẪU — interface đặt ở Interfaces/<MÃ_MODULE>/.
// Controller chỉ phụ thuộc vào interface này (dễ test: mock được bằng Moq).
public interface ISampleService
{
    Task<IReadOnlyList<SampleDto>> GetAllAsync();
    Task<SampleDto?> GetByIdAsync(int id);
}
