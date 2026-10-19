using AutoMapper;
using ETOMS.Application.DTOs;
using ETOMS.Domain.Entities;

namespace ETOMS.Application.Mappings;

// AutoMapper tự map Entity -> DTO. Khai báo 1 lần ở đây, khỏi gán tay từng field.
// Mỗi module tạo 1 Profile riêng (vd: TeamMappingProfile) trong thư mục này.
public class SampleMappingProfile : Profile
{
    public SampleMappingProfile()
    {
        CreateMap<SampleItem, SampleItemDto>();
    }
}
