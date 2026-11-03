# ETOMS — Esports Tournament Management System (SEP490_G97)

Hệ thống quản lý giải đấu thể thao điện tử. Capstone FPT University, nhóm SEP490_G97.
Repo GitHub: `SEP490-G97/Esports-Tournament-Management-System`.

> File README này được viết lại từ việc đọc trực tiếp code hiện có trong thư mục này
> (`ETOMS_1` trên máy) ngày 2026-11-03. README cũ trên GitHub chỉ có đúng 1 dòng tên
> repo, chưa có nội dung gì để kế thừa.

## ⚠️ Tình trạng hiện tại — đọc trước khi code tiếp

Thư mục làm việc trên máy này **khác với commit mới nhất trên GitHub** và đang ở trạng
thái dở dang, chưa commit:

| | Trạng thái |
|---|---|
| Trên GitHub (nhánh `main`, commit `83b3d52`/`a692cf3`/`d4ae700`) | Cấu trúc **cũ**: `ETOMS_API/` phẳng 1 cấp, còn nguyên `WeatherForecastController.cs` do `dotnet new webapi` sinh ra mặc định, có kèm `Dockerfile`, `.gitignore`, `README.md` (1 dòng) |
| Trên máy (working tree) | Cấu trúc **mới** (giống mẫu `EbayCloneBuyerService_CoreAPI`): toàn bộ code cũ đã bị **xóa khỏi working tree** (`git status` báo *deleted*, kể cả `.gitignore` và `README.md`), thay bằng project lồng một cấp nữa: `ETOMS_API/ETOMS_API/...` — **project này hoàn toàn chưa add vào git** (untracked) |

**Hệ quả cần biết:**
1. `.gitignore` đang **không tồn tại** trên đĩa → `bin/`, `obj/`, `.vs/` dễ bị add nhầm vào git nếu ai đó `git add -A`. Phải tạo lại `.gitignore` (chuẩn ASP.NET Core: loại `bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.Development.json` nếu sau này có chuỗi kết nối thật) **trước khi** commit đợt cấu trúc mới này.
2. `ETOMS_API/ETOMS_API.csproj` (bản cũ, cấp 1) và `ETOMS_API/ETOMS_API/ETOMS_API.csproj` (bản mới, cấp 2) **không phải cùng 1 file** — đang có 2 project cùng tên lồng nhau trên đĩa. Trước khi commit cần dọn cho chỉ còn 1 cấu trúc, nếu không Visual Studio/`.slnx` sẽ rối.
3. Repo này (`ETOMS_1`) là **bản chia sẻ chung của cả nhóm trên GitHub**, khác với thư mục làm việc cục bộ `E:\Đồ án - SEP490 G97\ETOMS\` (đã code xong Iteration 1–3: Team/Registration/Bracket/Result, cả MVC lẫn Web API, giao diện HTML/CSS/JS thuần). Hai thư mục là **hai codebase tách biệt, chưa hợp nhất** — đừng nhầm lẫn khi quyết định code tiếp ở đâu.

## Cấu trúc thư mục (bản mới, đang dùng để code tiếp)

```
ETOMS_1/
├── ETOMS_API/
│   └── ETOMS_API/              ← project Web API thật (nested — xem cảnh báo mục trên)
│       ├── Controllers/        trống — Controller API (JSON) sẽ nằm ở đây
│       ├── DTOs/                trống — request/response DTO, theo từng module con nếu nhiều
│       ├── Exceptions/          trống — exception nghiệp vụ tự định nghĩa (nếu dùng cách bắt lỗi qua exception thay vì Result)
│       ├── Hubs/                trống — SignalR Hub (tỉ số trực tiếp, thông báo — module LIVE/NOT)
│       ├── Models/               trống — entity (sẽ scaffold database-first từ ETOMS_Database.sql, xem CLAUDE.md mục 6)
│       ├── Repositories/
│       │   ├── Interface/       IGenericRepository.cs — đã có
│       │   └── Impl/            GenericRepository.cs — đã có, TOÀN BỘ method đang `throw new NotImplementedException()`
│       ├── Services/
│       │   ├── Interface/       trống (khai trong .csproj, chưa có file)
│       │   └── Impl/             trống
│       ├── Utils/                trống — helper dùng chung (upload file, format, v.v.)
│       ├── Program.cs            rất tối giản: AddControllers + AddOpenApi, CHƯA có Session/EF/DI module nào
│       ├── appsettings.json / appsettings.Development.json
│       └── Properties/launchSettings.json   (http: 5137, https: 7148)
└── ui/                           HOÀN TOÀN TRỐNG — chưa quyết định làm frontend kiểu gì ở đây
```

### Pattern đang dùng: Generic Repository

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
```

`GenericRepository<T>` implement interface này nhưng **mọi method đều throw
`NotImplementedException`** — đây là khung sườn (scaffold) do Hải dựng ("[Haivd] Add
base repository, base architecture code"), chưa nối với `DbContext` thật. Muốn dùng
được phải:
1. Thêm package `Microsoft.EntityFrameworkCore.SqlServer` (hiện `.csproj` mới chỉ có
   `Microsoft.EntityFrameworkCore` lõi, chưa có provider SQL Server).
2. Scaffold `DbContext` + entity database-first từ `ETOMS_Database.sql` (script này
   **chưa có trong repo `ETOMS_1`** — đang nằm ở thư mục cha `E:\Đồ án - SEP490 G97\`,
   cần copy vào đúng vị trí theo cấu trúc chuẩn trước, xem CLAUDE.md mục 5/6).
3. Viết lại `GenericRepository<T>` dùng `DbSet<T>` thay vì throw exception (có thể
   tham khảo cách làm ở `E:\Đồ án - SEP490 G97\ETOMS\ETOMS\Repositories\GenericRepository.cs`
   — đã implement đầy đủ, chạy được trên SQL Server thật).

Package khác đã thêm sẵn nhưng **chưa được dùng ở đâu trong code** (chỉ khai trong
`.csproj`, `Program.cs` chưa gọi): `AutoMapper`, `DotNetEnv` (đọc biến môi trường từ
file `.env` — hiện chưa có file `.env`/`.env.example` nào trong repo), `MailKit` +
`MimeKit` (gửi email — phục vụ xác thực/thông báo sau này).

## Luật để code — áp dụng cho TOÀN BỘ dự án ETOMS (rút từ `CLAUDE.md`)

Đây là các ràng buộc nền tảng của dự án, không riêng gì repo này. Phải đọc kỹ trước
khi thêm bất kỳ chức năng nào:

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
5. **Database: SQL Server, database-first.** Không dùng `dotnet ef migrations add` để
   tạo/đổi schema — sửa schema thì sửa file `ETOMS_Database.sql` rồi scaffold lại
   entity. Lược đồ có CHECK constraint + unique index có điều kiện là tầng cưỡng chế
   nghiệp vụ thật sự — đừng xóa khi EF scaffold báo khó, sai là ở code.
6. **Phân công module** (ai đụng module nào hỏi người đó trước khi đổi logic chung):

   | Thành viên | Module |
   |---|---|
   | Cấn Việt Hùng (Leader) | MKT, ORG, PRF, REG, RNK |
   | Nguyễn Tiến Dũng | BRK-01..07, RES, TEAM |
   | Nguyễn Quang Anh | FA, LIVE, MAT-08..13, PEN, SPN, TOUR-01..07 |
   | Nguyễn Duy Khánh | BRK-08..13, GRL, ITG-07..11, POR, TOUR-08..14 |
   | Vũ Đức Hải | ADM, AUTH, ITG-01..06, MAT-01..07, NOT |

7. **Git:** đừng `git add -A` khi chưa có `.gitignore` đúng (xem cảnh báo đầu file) —
   dễ commit nhầm `bin/`, `obj/`, `.vs/`, hoặc chuỗi kết nối thật trong
   `appsettings.Development.json`.

## Luật code theo từng thư mục

Chốt theo đúng pattern **đã chạy thật** ở `E:\Đồ án - SEP490 G97\ETOMS\ETOMS\` (project
cục bộ, đã code xong Iteration 1-3, build 0 lỗi, test qua `curl` đầy đủ) — áp dụng lại
cho `ETOMS_1` để hai bên không lệch kiến trúc. Nguyên tắc chung: **Controller không
chứa logic nghiệp vụ, Service không biết gì về HTTP, Repository không biết gì về
nghiệp vụ.**

### `Controllers/`
- Mỗi Controller ứng với **1 module/resource**, route `[Route("api/<ten-so-nhieu>")]`
  (vd `api/teams`, `api/registrations`, `api/tournaments/{tournamentId}/bracket`).
- Kế thừa 1 base class chung (vd `ApiControllerBase : ControllerBase`, có `[ApiController]`)
  để chuẩn hoá response — xem khung dưới.
- Action **chỉ làm 3 việc**: đọc input → gọi đúng 1 method của Service → chuyển
  `Result`/`Result<T>` thành `IActionResult`. Không query DB, không if/else nghiệp vụ
  trong Controller.
- Thứ tự kiểm tra bắt buộc trong mọi action cần quyền: **tồn tại trước (404) rồi mới
  tới quyền (403)** — kiểm tra ngược sẽ lộ thông tin tồn tại của tài nguyên cho người
  không có quyền. Dùng `bool?`/kiểu nullable cho hàm check quyền để phân biệt
  "không tồn tại" (`null`) và "tồn tại nhưng không có quyền" (`false`).
- Không dùng `[ValidateAntiForgeryToken]` cho Controller API (frontend gọi `fetch`,
  không có token) — bảo vệ CSRF bằng cookie session `SameSite=Strict` ở `Program.cs`.
- Khuôn mẫu base class nên có (copy ý tưởng từ `ETOMS/ETOMS/Controllers/ApiControllerBase.cs`):
  ```csharp
  [ApiController]
  public abstract class ApiControllerBase : ControllerBase
  {
      protected IActionResult FromResult(Result r)
          => r.IsSuccess ? Ok(new { message = r.Message }) : BadRequest(new { message = r.Message, fieldErrors = r.FieldErrors });
      protected IActionResult FromResult<T>(Result<T> r)
          => r.IsSuccess ? Ok(new { message = r.Message, data = r.Data }) : BadRequest(new { message = r.Message, fieldErrors = r.FieldErrors });
  }
  ```

### `DTOs/`
- Tách theo module con, mỗi module 1 thư mục con (`DTOs/Team/`, `DTOs/Registration/`...)
  — không gom hết vào 1 file lớn.
- Request DTO validate bằng DataAnnotations (`[Required]`, `[StringLength]`,
  `[Range]`, `[EmailAddress]`...) để `[ApiController]` tự trả 400 khi sai định dạng
  cơ bản; lỗi nghiệp vụ sâu hơn (trùng tên, sai trạng thái...) trả qua `Result.FieldFailure`.
- Response DTO là object thuần phục vụ hiển thị — được phép có **property tính sẵn**
  (vd `StatusLabel`, `StatusClass`, `RoleLabel` dịch tiếng Việt + gán class CSS) để FE
  không phải tính lại logic hiển thị.
- **Không bao giờ trả thẳng Entity ra API** — luôn qua DTO, kể cả khi DTO và Entity
  giống hệt nhau, để tránh lộ cột nhạy cảm (`PasswordHash`...) khi Entity đổi cột sau này.

### `Exceptions/`
- Dự án này **ưu tiên dùng kiểu `Result`/`Result<T>` thay vì exception** cho lỗi
  nghiệp vụ (xem `Services/` bên dưới) — exception dùng `throw` làm luồng điều khiển
  chính rất chậm và khó test. Thư mục `Exceptions/` chỉ nên chứa exception cho lỗi
  **hạ tầng/hệ thống thật sự** (vd mất kết nối DB, timeout bên ngoài) mà
  `UseExceptionHandler` ở `Program.cs` bắt chung, không dùng cho "sai dữ liệu",
  "trùng tên", "hết hạn"... — những cái đó trả `Result.Failure(...)`.

### `Hubs/`
- Mỗi Hub SignalR ứng với 1 nhóm sự kiện real-time (vd `LiveScoreHub` cho tỉ số
  trực tiếp — module LIVE, `NotificationHub` — module NOT).
- Hub chỉ nhận kết nối + broadcast, **không chứa logic nghiệp vụ**: Service xử lý
  xong rồi gọi `IHubContext<T>.Clients...SendAsync(...)` để đẩy tin, không để Hub tự
  query DB.

### `Models/`
- **Chỉ entity scaffold database-first** từ `ETOMS_Database.sql`
  (`dotnet ef dbcontext scaffold ... -o Models -c EtomsDbContext --no-onconfiguring`,
  xem CLAUDE.md mục 6) — không sửa tay property, không thêm property không có trong
  script SQL, không đặt ViewModel/DTO chung chỗ này.
- Đổi schema thì sửa `ETOMS_Database.sql` → tạo lại DB → scaffold lại, **không** dùng
  `dotnet ef migrations add`.

### `Repositories/Interface/` và `Repositories/Impl/`
- Dùng **1 Generic Repository chung cho mọi entity** (`IGenericRepository<T>`) —
  không tạo riêng `ITeamRepository`, `IGameRepository`... cho từng entity trừ khi
  thật sự cần query phức tạp không generic hoá được.
- Khuyến nghị thêm `IUnitOfWork` (đúng kiến trúc Report 4 đã chốt, xem
  `ETOMS/ETOMS/Repositories/UnitOfWork.cs`): `IUnitOfWork.Repository<TEntity>()` phát
  ra 1 `GenericRepository<TEntity>` dùng chung 1 `DbContext`/request, gom nhiều thay
  đổi rồi `SaveChangesAsync()` đúng 1 lần ở cuối — Service inject `IUnitOfWork`, không
  inject `DbContext` trực tiếp.
- `Interface/` chỉ chứa interface, `Impl/` chỉ chứa class implement — không khai báo
  interface lẫn trong file implementation.
- Repository chỉ CRUD/query (`Query()`, `GetByIdAsync`, `AnyAsync`, `AddAsync`,
  `Update`, `Remove`) — không chứa if/else nghiệp vụ.

### `Services/Interface/` và `Services/Impl/`
- **Toàn bộ business logic nằm ở đây.** Mỗi Service ứng 1 module
  (`ITeamService`/`TeamService`, `IRegistrationService`/`RegistrationService`...),
  method đặt tên theo hành vi nghiệp vụ (`CreateTeamAsync`, `JoinByCodeAsync`,
  `ApproveMemberAsync`...), không đặt tên kiểu CRUD chung chung.
- Method trả `Task<Result>` hoặc `Task<Result<T>>` — **không throw exception cho lỗi
  nghiệp vụ**, dùng `Result.Failure("thông báo tiếng Việt")` hoặc
  `Result.FieldFailure("TenField", "thông báo")` để Controller/FE gắn đúng lỗi vào
  đúng ô trên form.
- Truy DB qua `IUnitOfWork`/`IGenericRepository<T>` injected, **không** inject
  `DbContext` thẳng vào Service.
- Thông báo (`Result.Message`, lỗi field) **luôn tiếng Việt có dấu** — đây là chuỗi
  sẽ hiển thị thẳng cho người dùng.

### `Utils/`
- Chỉ helper thuần, **không phụ thuộc `DbContext`/`IUnitOfWork`**, không chứa quy tắc
  nghiệp vụ: upload file (kiểm định dạng/dung lượng), format chuỗi/ngày tháng, sinh mã
  ngẫu nhiên... Kiểu `Result`/`Result<T>`/`PagedResult<T>` cũng nằm ở đây vì chúng là
  kiểu dữ liệu dùng chung, không phải nghiệp vụ của module nào.

## Việc cần làm trước khi nhóm code tiếp trên repo này (TODO)

- [ ] Quyết định giữ cấu trúc nào: xóa hẳn `ETOMS_API/ETOMS_API.csproj` (cấp 1, cũ) hay
      gộp ngược lại — hiện đang tồn tại song song 2 project lồng nhau trên đĩa.
- [ ] Tạo lại `.gitignore` (và `.dockerignore` nếu còn dùng Docker) trước khi commit.
- [ ] Thêm `Microsoft.EntityFrameworkCore.SqlServer`, copy `ETOMS_Database.sql` vào
      repo, scaffold entity, viết lại `GenericRepository<T>` dùng `DbSet<T>` thật.
- [ ] Quyết định `ui/` làm bằng gì (Razor riêng, SPA riêng, hay đồng bộ theo hướng
      HTML/CSS/JS thuần đã làm ở `ETOMS/ETOMS` cục bộ) — hiện đang trống hoàn toàn.
- [ ] Nối `Program.cs`: Session/DI cho Repository+Service, CORS nếu `ui/` tách origin
      riêng với API.
