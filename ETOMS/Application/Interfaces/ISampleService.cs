using ETOMS.Application.DTOs;

namespace ETOMS.Application.Interfaces;

// VÍ DỤ MẪU interface Service. Controller chỉ phụ thuộc interface này.
public interface ISampleService
{
    Task<IReadOnlyList<SampleItemDto>> GetAllAsync();
    Task<SampleItemDto?> GetByIdAsync(int id);
}
