# ETOMS — Esports Tournament Management System (SEP490_G97)

Hệ thống quản lý giải đấu thể thao điện tử. Capstone FPT University, nhóm SEP490_G97.
Repo GitHub: `SEP490-G97/Esports-Tournament-Management-System`.

## Cấu trúc thư mục

```
ETOMS_1/
├── ETOMS_API/                   project Web API (ASP.NET Core, net10.0)
│   ├── Controllers/              Controller API (JSON), mỗi file 1 module/resource
│   ├── DTOs/                     request/response DTO, chia theo thư mục con từng module
│   ├── Exceptions/                exception tự định nghĩa cho lỗi hạ tầng (xem luật bên dưới)
│   ├── Hubs/                      SignalR Hub (tỉ số trực tiếp, thông báo)
│   ├── Models/                    entity ánh xạ database-first từ schema MySQL của nhóm
│   ├── Repositories/
│   │   ├── Interface/             IGenericRepository<T>, IUnitOfWork
│   │   └── Impl/                  GenericRepository<T>, UnitOfWork
│   ├── Services/
│   │   ├── Interface/             IXxxService — 1 interface / module nghiệp vụ
│   │   └── Impl/                  XxxService — implementation
│   ├── Utils/                     Result/Result<T>/PagedResult<T>, upload file, format...
│   ├── Program.cs
│   ├── appsettings.json           KHÔNG chứa chuỗi kết nối thật
│   └── Properties/launchSettings.json
└── ui/                            frontend (chưa code)
```

### Cấu hình database khi chạy API

API dùng provider `MySql.EntityFrameworkCore` cho EF Core 10. Có thể lưu chuỗi kết nối
trong User Secrets (khuyến nghị) hoặc đặt biến môi trường `ETOMS_DB_CONNECTION`.
Ví dụ PowerShell, chạy từ thư mục gốc repository:

```powershell
dotnet user-secrets set "ConnectionStrings:EtomsDatabase" "Server=15.135.76.150;Port=3306;Database=etoms_db;User ID=etoms;Password=YOUR_NEW_PASSWORD;Character Set=utf8mb4" --project ETOMS_API
dotnet run --project ETOMS_API/ETOMS_API.csproj
```

Hãy thay `YOUR_NEW_PASSWORD` bằng mật khẩu mới sau khi đổi mật khẩu đã chia sẻ trong
ảnh; không đưa mật khẩu thật vào `appsettings.json`, README hoặc Git. Máy chủ MySQL
cần cho phép kết nối từ máy bạn và cung cấp đúng cổng/TLS mode cho nhóm.

Entity trong `ETOMS_API/Models/` và `ETOMS_API/Repositories/EtomsDbContext.cs` phải
khớp schema MySQL do nhóm cung cấp (tên bảng/cột, kiểu dữ liệu, khóa ngoại và views).
File local `ETOMS_Database.sql` là cú pháp SQL Server cũ, **không chạy file đó trên
MySQL**. Dự án dùng database-first, không tự tạo hoặc nâng cấp schema khi khởi động.
Trước khi chạy các luồng đăng ký Iteration 2 trên môi trường khác, sao lưu database
và áp dụng `database/mysql/iteration2-registration-upgrade.sql`. Bản nâng cấp thêm
thời hạn xác nhận waitlist và mở rộng các CHECK constraint cho trạng thái hết hạn,
biên lai/hoàn tiền và audit. Trong EF Core, `Tournament.RequireStudentCard` ánh xạ
tới cột MySQL `RequireIdentityDocument`.
Connection của API được đặt rõ charset `utf8mb4`. Nếu dữ liệu seed cũ đã mất dấu, có
thể áp dụng có điều kiện script `database/mysql/fix-vietnamese-seed-data.sql`; script
chỉ sửa đúng các giá trị đã bị mất dấu như nội dung trong ảnh.
Các unique index có điều kiện của SQL Server không được MySQL hỗ trợ trực tiếp; schema
MySQL phải có cách cưỡng chế tương đương (ví dụ generated column/index hoặc constraint
phù hợp). Cần đối chiếu schema MySQL thật trước khi xác nhận mọi API hoạt động.

Nếu dùng biến môi trường thay cho User Secrets, đặt `ETOMS_DB_CONNECTION` bằng chuỗi
kết nối MySQL cùng định dạng trước khi chạy API. Provider Oracle có giấy phép
GPL-2.0 kèm Universal-FOSS exception; nhóm cần xác nhận giấy phép này phù hợp với
cách phân phối dự án.

Các API Profile, Organization và Registration chưa tích hợp xác thực thật; hiện dùng
đăng nhập demo trong môi trường Development. Tệp riêng tư được lưu dưới
`ETOMS_API/App_Data/private-uploads/`, ngoài thư mục tĩnh. API Profile giới hạn tài
liệu cá nhân cho chủ tài khoản; API Registration giới hạn giấy tờ và biên lai cho
đội trưởng/ban tổ chức tương ứng. Tài liệu tổ chức hiện chỉ có metadata trong hàng
đợi xét duyệt. Không đưa API lên môi trường công khai trước khi module AUTH tích hợp
xong và kiểm thử quyền truy cập các tệp riêng tư.

### API được triển khai — Iteration 1 (Profile, Organization)

- `GET/PUT /api/profiles/{accountId}` — đọc/cập nhật hồ sơ.
- `PUT /api/profiles/{accountId}/games/{gameId}` — lưu tên trong game.
- `GET /api/profiles/{accountId}/public`, `GET /api/profiles/{accountId}/match-history` —
  hồ sơ công khai và lịch sử thi đấu.
- `GET/PUT /api/profiles/{accountId}/notification-preferences` — cài đặt thông báo.
- `POST/GET /api/profiles/{accountId}/identity-documents` — tải lên/liệt kê metadata
  giấy tờ định danh.
- `POST /api/organizations` — đăng ký đơn vị; `POST /api/organizations/{organizationId}/documents`
  — tải giấy tờ.
- `GET /api/organizations/review-queue`, `POST /api/organizations/{organizationId}/review`
  — hàng đợi và kết quả xét duyệt.
- `GET /api/organizations`, `GET /api/organizations/{organizationId}` — danh sách và
  hồ sơ công khai của đơn vị.
- `GET/POST /api/organizations/{organizationId}/members`,
  `POST /api/organizations/{organizationId}/members/{memberId}/accept`,
  `PATCH /api/organizations/{organizationId}/members/{memberId}` — thành viên, lời mời,
  chấp nhận lời mời và vai trò.
- `POST /api/organizations/{organizationId}/chair-transfer`,
  `GET /api/organizations/{organizationId}/reputation`,
  `POST /api/organizations/{organizationId}/suspension` — chuyển chủ nhiệm, uy tín và
  đình chỉ.

Trong giai đoạn chưa có AUTH, các ID tài khoản/người xét duyệt nhận từ request chỉ là
đầu vào phát triển, không chứng minh danh tính hoặc quyền hạn của người gọi.

### Pattern: Generic Repository + Unit of Work

```csharp
public interface IGenericRepository<T> where T : class
{
    IEnumerable<T> GetAll();
    T GetById(object id);
    void Add(T entity);
    void Update(T entity);
    void Delete(T entity);
    void Save();
}

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync();
}
```

`Controller → Service → IUnitOfWork.Repository<TEntity>() → DbContext → DB`. Service
không inject `DbContext` trực tiếp, luôn qua `IUnitOfWork`.

## Luật để code — áp dụng cho TOÀN BỘ dự án ETOMS (rút từ `CLAUDE.md`)

1. **LI-01 — KHÔNG tích hợp API nhà phát hành game.** Tỉ số chỉ vào hệ thống qua
   báo cáo mù hai chiều (`MatchReport`) hoặc phán quyết VAR (`Dispute`). Không viết
   client gọi Riot/Garena/bất kỳ game publisher nào.
2. **LI-02 — KHÔNG tích hợp cổng thanh toán.** Không bảng giao dịch, không cột số dư.
   Chỉ lưu minh chứng thanh toán (`PaymentRecord`). Không gọi VNPay/Momo/Stripe...
3. **Không tự nghĩ ra chức năng.** Mọi API/Controller/Service phải truy được về catalogue
   144 chức năng (`ChucNangcuaGiaDau.xlsx`) hoặc Report 3. Thiếu thì hỏi người phụ
   trách, đừng tự chế thêm endpoint.
4. **Ngôn ngữ:** dữ liệu/chuỗi trả về cho người dùng (message lỗi, label...) phải
   **100% tiếng Việt có dấu**. Tên class/property/route/biến trong code: tiếng Anh.
5. **Database: MySQL, database-first.** Không dùng `dotnet ef migrations add` để tự
   tạo/đổi schema. Schema MySQL do nhóm quản lý; cập nhật entity/context theo schema
   MySQL đã thống nhất. Không chạy `ETOMS_Database.sql` (script SQL Server cũ) trên
   MySQL. Bảo toàn các ràng buộc nghiệp vụ bằng các constraint/index MySQL tương đương.
   Luồng waitlist Iteration 2 cần chạy `database/mysql/iteration2-registration-upgrade.sql`
   trên schema MySQL trước khi triển khai API phiên bản này.
6. **Phân công module** (ai đụng module nào hỏi người đó trước khi đổi logic chung):

   | Thành viên | Module |
   |---|---|
   | Cấn Việt Hùng (Leader) | MKT, ORG, PRF, REG, RNK |
   | Nguyễn Tiến Dũng | BRK-01..07, RES, TEAM |
   | Nguyễn Quang Anh | FA, LIVE, MAT-08..13, PEN, SPN, TOUR-01..07 |
   | Nguyễn Duy Khánh | BRK-08..13, GRL, ITG-07..11, POR, TOUR-08..14 |
   | Vũ Đức Hải | ADM, AUTH, ITG-01..06, MAT-01..07, NOT |

7. **Git:** không commit `appsettings.Development.json` hay bất kỳ chuỗi kết nối thật
   nào — đã nằm trong `.gitignore`.

## Luật code theo từng thư mục

Nguyên tắc chung: **Controller không chứa logic nghiệp vụ, Service không biết gì về
HTTP, Repository không biết gì về nghiệp vụ.**

### `Controllers/`
- Mỗi Controller ứng với **1 module/resource**, route `[Route("api/<ten-so-nhieu>")]`
  (vd `api/teams`, `api/registrations`, `api/tournaments/{tournamentId}/bracket`).
- Kế thừa 1 base class chung (`ApiControllerBase : ControllerBase`, có `[ApiController]`)
  để chuẩn hoá response:
  ```csharp
  protected IActionResult FromResult(Result r)
      => r.IsSuccess ? Ok(new { message = r.Message }) : BadRequest(new { message = r.Message, fieldErrors = r.FieldErrors });
  protected IActionResult FromResult<T>(Result<T> r)
      => r.IsSuccess ? Ok(new { message = r.Message, data = r.Data }) : BadRequest(new { message = r.Message, fieldErrors = r.FieldErrors });
  ```
- Action **chỉ làm 3 việc**: đọc input → gọi đúng 1 method của Service → chuyển
  `Result`/`Result<T>` thành `IActionResult`. Không query DB, không if/else nghiệp vụ
  trong Controller.
- Thứ tự kiểm tra bắt buộc trong mọi action cần quyền: **tồn tại trước (404) rồi mới
  tới quyền (403)**. Dùng `bool?` cho hàm check quyền để phân biệt "không tồn tại"
  (`null`) và "tồn tại nhưng không có quyền" (`false`).
- Không dùng `[ValidateAntiForgeryToken]` (frontend gọi `fetch`, không có token) —
  bảo vệ CSRF bằng cookie session `SameSite=Strict` ở `Program.cs`.

### `DTOs/`
- Tách theo module con, mỗi module 1 thư mục con (`DTOs/Team/`, `DTOs/Registration/`...)
  — không gom hết vào 1 file lớn.
- Request DTO validate bằng DataAnnotations (`[Required]`, `[StringLength]`,
  `[Range]`, `[EmailAddress]`...); lỗi nghiệp vụ sâu hơn (trùng tên, sai trạng thái...)
  trả qua `Result.FieldFailure`.
- Response DTO được phép có **property tính sẵn** (vd `StatusLabel`, `StatusClass`,
  `RoleLabel` dịch tiếng Việt + gán class CSS) để FE không phải tính lại logic hiển thị.
- **Không bao giờ trả thẳng Entity ra API** — luôn qua DTO.

### `Exceptions/`
- Dự án **ưu tiên `Result`/`Result<T>` thay vì exception** cho lỗi nghiệp vụ (xem
  `Services/` bên dưới). Thư mục này chỉ dùng cho lỗi **hạ tầng/hệ thống thật sự** (mất
  kết nối DB, timeout bên ngoài) mà `UseExceptionHandler` ở `Program.cs` bắt chung —
  không dùng cho "sai dữ liệu", "trùng tên", "hết hạn"...

### `Hubs/`
- Mỗi Hub SignalR ứng 1 nhóm sự kiện real-time (vd `LiveScoreHub` — module LIVE,
  `NotificationHub` — module NOT). Hub chỉ nhận kết nối + broadcast, **không chứa logic
  nghiệp vụ** — Service xử lý xong rồi gọi `IHubContext<T>...SendAsync(...)`.

### `Models/`
- **Entity database-first** phải khớp schema MySQL đã thống nhất với nhóm. Không lấy
  `ETOMS_Database.sql` làm nguồn schema hiện tại vì đó là script SQL Server cũ.
- Đổi schema thì cập nhật schema MySQL và scaffold/đối chiếu entity, **không** dùng
  `dotnet ef migrations add`.

### `Repositories/Interface/` và `Repositories/Impl/`
- **1 Generic Repository chung cho mọi entity** (`IGenericRepository<T>`) — không tạo
  riêng `ITeamRepository`, `IGameRepository`... trừ khi thật sự cần query phức tạp.
- `IUnitOfWork.Repository<TEntity>()` phát ra 1 `GenericRepository<TEntity>` dùng chung
  1 `DbContext`/request, gom nhiều thay đổi rồi `SaveChangesAsync()` đúng 1 lần ở cuối.
- `Interface/` chỉ chứa interface, `Impl/` chỉ chứa class implement.
- Repository chỉ CRUD/query (`Query()`, `GetById`, `Any`, `Add`, `Update`, `Delete`) —
  không chứa if/else nghiệp vụ.

### `Services/Interface/` và `Services/Impl/`
- **Toàn bộ business logic nằm ở đây.** Mỗi Service ứng 1 module (`ITeamService`/
  `TeamService`...), method đặt tên theo hành vi nghiệp vụ (`CreateTeamAsync`,
  `JoinByCodeAsync`, `ApproveMemberAsync`...), không đặt tên kiểu CRUD chung chung.
- Method trả `Task<Result>` hoặc `Task<Result<T>>` — **không throw exception cho lỗi
  nghiệp vụ**, dùng `Result.Failure("thông báo tiếng Việt")` hoặc
  `Result.FieldFailure("TenField", "thông báo")`.
- Truy DB qua `IUnitOfWork`/`IGenericRepository<T>` injected, **không** inject
  `DbContext` thẳng vào Service.
- Thông báo (`Result.Message`, lỗi field) **luôn tiếng Việt có dấu**.

### `Utils/`
- Chỉ helper thuần, **không phụ thuộc `DbContext`/`IUnitOfWork`**: upload file (kiểm
  định dạng/dung lượng), format chuỗi/ngày tháng... `Result`/`Result<T>`/`PagedResult<T>`
  cũng nằm ở đây vì là kiểu dữ liệu dùng chung, không phải nghiệp vụ của module nào.
