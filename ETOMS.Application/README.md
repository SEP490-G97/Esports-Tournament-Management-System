# ETOMS.Application — tầng nghiệp vụ

Chứa logic nghiệp vụ, tách khỏi `ETOMS.Web` (giao diện/API) và `ETOMS.Infrastructure` (dữ liệu).

## Ba thư mục, chia theo mã module (`ADM`, `AUTH`, `TEAM`, ...)

| Thư mục | Chứa gì |
|---|---|
| `DTOs/<MÃ>/` | Lớp dữ liệu trả về / nhận vào qua API. **Chỉ field cần dùng.** |
| `Interfaces/<MÃ>/` | Interface của Service (vd `ITeamService`). Controller phụ thuộc interface này. |
| `Services/<MÃ>/` | Cài đặt Service: inject `IGenericRepository<Entity>`, lấy entity → map sang DTO. |

## Luồng chuẩn

```
Controller (Web)  →  IService (Application)  →  Repository (Infrastructure)  →  Entity (DB)
       ↑ trả DTO          ↑ map entity→DTO            ↑ CRUD
```

## Quy tắc quan trọng

- **KHÔNG trả thẳng Entity ra API** — luôn map sang DTO. Entity có nhiều cột nội bộ
  (khóa ngoại, cột audit, cột ẩn), xổ hết ra vừa bẩn vừa lộ cấu trúc DB. Cần gì lấy đó.
- **Controller mỏng** — chỉ gọi Service, không chứa nghiệp vụ, không đụng `DbContext`.
- **Đăng ký Service** trong `DependencyInjection.cs` (`AddApplication()`): mỗi service mới
  thêm 1 dòng `services.AddScoped<IXxxService, XxxService>();`.

## Ví dụ mẫu

Xem thư mục `_Example/` (`SampleDto`, `ISampleService`, `SampleService`) và
`ETOMS.Web/Controllers/_Example/SampleController.cs`. Chạy app → vào `/swagger` test thử.
Khi quen rồi thì **xóa `_Example/`** đi.
