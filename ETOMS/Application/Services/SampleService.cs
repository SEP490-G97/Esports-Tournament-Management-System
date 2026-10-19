using AutoMapper;
using ETOMS.Application.DTOs;
using ETOMS.Application.Interfaces;
using ETOMS.Domain.Entities;

namespace ETOMS.Application.Services;

// VÍ DỤ MẪU implementation. Thực tế inject IUnitOfWork để lấy entity từ DB; ở đây dùng
// dữ liệu giả cho dễ chạy. Điểm mấu chốt: lấy ENTITY rồi dùng AutoMapper map sang DTO.
public class SampleService : ISampleService
{
    private readonly IMapper _mapper;

    private static readonly List<SampleItem> _demo = new()
    {
        new SampleItem { Id = 1, Ten = "Mẫu A", CreatedBy = "admin" },
        new SampleItem { Id = 2, Ten = "Mẫu B", CreatedBy = "admin" },
    };

    public SampleService(IMapper mapper) => _mapper = mapper;

    public Task<IReadOnlyList<SampleItemDto>> GetAllAsync()
    {
        var dtos = _mapper.Map<IReadOnlyList<SampleItemDto>>(_demo);
        return Task.FromResult(dtos);
    }

    public Task<SampleItemDto?> GetByIdAsync(int id)
    {
        var entity = _demo.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(entity is null ? null : _mapper.Map<SampleItemDto>(entity));
    }
}
