# Esports Tournament Management System (ETOMS)

Đồ án SEP490 — Nhóm G97. ASP.NET Core MVC + Web API, EF Core, SignalR, Bootstrap 5, SQL Server.

Cấu trúc theo kiểu **1 project chia tầng bằng folder** (giống đồ án môn PRN/Cinema nhóm từng làm) — Clean Architecture nhẹ: Domain → Application → Infrastructure, cộng lớp giao diện (Controllers + Views).

## Cấu trúc thư mục

```
ETOMS/                          ← repo (nhánh master dùng chung)
├── ETOMS.sln
├── database/                   ← script SQL (ETOMS_Database.sql, ETOMS_Database_Test.sql)
└── ETOMS/                      ← 1 project duy nhất
    ├── Application/            TẦNG NGHIỆP VỤ
    │   ├── Common/             Result, PagedResult, hằng số dùng chung
    │   ├── DTOs/               lớp dữ liệu trả ra API (chỉ field cần)
    │   ├── Interfaces/         interface của Service + Repository
    │   ├── Services/           cài đặt nghiệp vụ
    │   └── Mappings/           AutoMapper Profile (map Entity ↔ DTO)
    ├── Domain/                 TẦNG LÕI
    │   ├── Entities/           entity (sẽ scaffold từ DB)
    │   └── Enums/              enum nghiệp vụ
    ├── Infrastructure/         TẦNG DỮ LIỆU
    │   ├── Data/               EtomsDbContext
    │   └── Repositories/       GenericRepository, UnitOfWork
    ├── Controllers/            nhận request (API [Route("api/...")] + MVC trả View)
    ├── Views/                  giao diện Razor (.cshtml)
    ├── Hubs/                   SignalR (thông báo, tỉ số trực tiếp)
    ├── wwwroot/                css, js, Bootstrap 5
    └── Program.cs
```

## Luồng chuẩn khi code 1 chức năng

```
Controller  →  IService → Service  →  IUnitOfWork/Repository  →  Entity (DB)
    ↑ trả DTO       ↑ AutoMapper map Entity→DTO
```

1. Tạo **DTO** trong `Application/DTOs/` — chỉ field client cần.
2. Tạo **Mapping Profile** trong `Application/Mappings/` (`CreateMap<Entity, Dto>()`).
3. Tạo **interface** `IXxxService` trong `Application/Interfaces/`.
4. Tạo **Service** trong `Application/Services/` (inject `IUnitOfWork`, lấy entity, map sang DTO).
5. Đăng ký service trong `Program.cs`: `builder.Services.AddScoped<IXxxService, XxxService>();`
6. Tạo **Controller** trong `Controllers/` (gọi service, trả DTO).

Xem bộ mẫu `Sample*` (SampleItem, SampleItemDto, SampleMappingProfile, SampleService,
SampleController) để hiểu luồng. Chạy app → `/swagger` test thử. Quen rồi thì xóa các file `Sample*`.

## Quy tắc

- **Không trả thẳng Entity ra API** — luôn map sang DTO. Cần gì lấy đó, không xổ full.
- **Controller mỏng** — chỉ gọi Service, không chứa nghiệp vụ, không đụng DbContext.

## Chạy dự án

```bash
dotnet run --project ETOMS
```

- Giao diện: `https://localhost:<port>/`
- Swagger (test API): `https://localhost:<port>/swagger`

## Cấu hình database

Chuỗi kết nối thật đặt trong `ETOMS/appsettings.Development.json` (**không commit**):

```json
{
  "ConnectionStrings": {
    "EtomsDb": "Server=.\\SQLEXPRESS;Database=ETOMS;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Sinh entity từ database đã có (database-first — không dùng migrations để đổi lược đồ):

```bash
dotnet ef dbcontext scaffold "Name=ConnectionStrings:EtomsDb" Microsoft.EntityFrameworkCore.SqlServer -o Domain/Entities --context EtomsDbContext --context-dir Infrastructure/Data
```

## Nhánh git

- `master` — nhánh code chung, mọi người clone về từ đây.
- Mỗi chức năng tạo nhánh `feature/<ten>`, xong tạo Pull Request vào `master`.
