# ETOMS — Esports Tournament Management System (SEP490_G97)

Hệ thống quản lý giải đấu thể thao điện tử. Capstone FPT University, nhóm SEP490_G97.
Repo GitHub: `SEP490-G97/Esports-Tournament-Management-System`.

## Tình trạng hiện tại

Nhánh `main-dev` đang chứa **code thật, chạy được**, không phải khung rỗng — đồng bộ từ
thư mục làm việc cục bộ `E:\Đồ án - SEP490 G97\ETOMS\ETOMS\` (đã code xong Iteration
1–3: Team, Registration, Bracket, Result — cả API lẫn giao diện HTML/CSS/JS thuần).
Build `dotnet build` tại `ETOMS_API/` ra **0 lỗi** trước khi đẩy lên.

Khác với ban đầu (xem lịch sử commit): đã bỏ code mẫu mặc định
(`WeatherForecastController`), bỏ project lồng 2 cấp, thêm `.gitignore` đúng chuẩn.

## Cấu trúc thư mục

```
ETOMS_1/
├── database/
│   ├── ETOMS_Database.sql         script tạo 27 bảng + 3 view (SQL Server, database-first)
│   └── ETOMS_Database_Test.sql    bộ kiểm ràng buộc — chạy xong phải ra 22/22
├── ETOMS_API/                      project Web API duy nhất (ASP.NET Core, net10.0)
│   ├── Controllers/                Controller API (JSON), mỗi file 1 module/resource
│   ├── DTOs/                       request/response DTO, chia theo thư mục con từng module
│   ├── Exceptions/                 trống — chỉ dùng cho lỗi hạ tầng (xem "Luật code" bên dưới)
│   ├── Hubs/                       trống — SignalR Hub (tỉ số trực tiếp, thông báo) khi cần
│   ├── Models/                     entity scaffold database-first từ database/ETOMS_Database.sql
│   ├── Repositories/               IGenericRepository<T>/GenericRepository<T>,
│   │                               IUnitOfWork/UnitOfWork, EtomsDbContext — đã nối EF thật
│   ├── Services/                   toàn bộ business logic (ITeamService, IRegistrationService,
│   │                               IBracketService, IResultService + implementation)
│   ├── Utils/                      Result/Result<T>/PagedResult<T>, upload file, v.v.
│   ├── wwwroot/                    giao diện — HTML/CSS/JS thuần (không Razor), gọi API qua fetch()
│   ├── Program.cs
│   ├── appsettings.json            KHÔNG có chuỗi kết nối thật (để trống)
│   └── Properties/launchSettings.json
└── ui/                             chưa dùng — hiện giao diện nằm trong ETOMS_API/wwwroot/
                                     (xem mục "Vì sao không tách ui/ riêng" bên dưới)
```

### Vì sao không tách `ui/` riêng

Bản đang đẩy lên gộp chung backend + frontend trong **1 project ASP.NET Core**
(`ETOMS_API/wwwroot/` chứa toàn bộ HTML/CSS/JS, gọi các Controller cùng project qua
`fetch()`, dùng chung session cookie nên không cần CORS). Đây là kiến trúc **đã chạy
thật và test qua** ở bản cục bộ, nên giữ nguyên khi đồng bộ lên đây thay vì tách `ui/`
ra một project riêng (sẽ cần thêm CORS, thêm cách xác thực qua origin khác, chưa có lý
do bắt buộc để làm việc đó bây giờ). `ui/` vẫn để trống, dành cho sau này nếu nhóm
quyết định tách thật.

### Pattern: Generic Repository + Unit of Work

```csharp
public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync();
}
```

`Controller → Service → IUnitOfWork.Repository<TEntity>() → EtomsDbContext → DB`.
Service **không** inject `DbContext` trực tiếp, luôn qua `IUnitOfWork`. Đã nối
`Microsoft.EntityFrameworkCore.SqlServer` thật — không còn `NotImplementedException`.

**Để chạy được trên máy bạn:** tạo file `ETOMS_API/appsettings.Development.json`
(đã nằm trong `.gitignore`, không commit) với nội dung:
```json
{
  "ConnectionStrings": {
    "EtomsDb": "Server=.\\SQLEXPRESS;Database=ETOMS;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```
rồi chạy `database/ETOMS_Database.sql` để tạo CSDL (xem CLAUDE.md mục 6 để biết thứ tự
và cờ `-f 65001` bắt buộc khi chạy bằng `sqlcmd`).

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
   tạo/đổi schema — sửa schema thì sửa `database/ETOMS_Database.sql` rồi scaffold lại
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

7. **Git:** không commit `appsettings.Development.json` hay bất kỳ chuỗi kết nối thật
   nào — đã nằm trong `.gitignore`, đừng `git add -f` đè lên.

## Luật code theo từng thư mục

Nguyên tắc chung: **Controller không chứa logic nghiệp vụ, Service không biết gì về
HTTP, Repository không biết gì về nghiệp vụ.**

### `Controllers/`
- Mỗi Controller ứng với **1 module/resource**, route `[Route("api/<ten-so-nhieu>")]`
  (vd `api/teams`, `api/registrations`, `api/tournaments/{tournamentId}/bracket`).
- Kế thừa `ApiControllerBase : ControllerBase` (`[ApiController]`) để chuẩn hoá response:
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
  bảo vệ CSRF bằng cookie session `SameSite=Strict` đã cấu hình ở `Program.cs`.

### `DTOs/`
- Tách theo module con, mỗi module 1 thư mục con (`DTOs/Team/`, `DTOs/Registration/`...).
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
- **Chỉ entity scaffold database-first** từ `database/ETOMS_Database.sql`:
  ```bash
  dotnet ef dbcontext scaffold "Server=.\SQLEXPRESS;Database=ETOMS;Trusted_Connection=True;TrustServerCertificate=True" \
    Microsoft.EntityFrameworkCore.SqlServer -o Models -c EtomsDbContext --no-onconfiguring
  ```
  Không sửa tay property, không thêm property không có trong script SQL.
- Đổi schema thì sửa `database/ETOMS_Database.sql` → tạo lại DB → scaffold lại, **không**
  dùng `dotnet ef migrations add`.

### `Repositories/`
- **1 Generic Repository chung cho mọi entity** (`IGenericRepository<T>`) — không tạo
  riêng `ITeamRepository`, `IGameRepository`... trừ khi thật sự cần query phức tạp.
- `IUnitOfWork.Repository<TEntity>()` phát ra 1 `GenericRepository<TEntity>` dùng chung
  1 `EtomsDbContext`/request, gom nhiều thay đổi rồi `SaveChangesAsync()` đúng 1 lần ở
  cuối — Service inject `IUnitOfWork`, không inject `DbContext` trực tiếp.
- Repository chỉ CRUD/query (`Query()`, `GetByIdAsync`, `AnyAsync`, `AddAsync`,
  `Update`, `Remove`) — không chứa if/else nghiệp vụ.

### `Services/`
- **Toàn bộ business logic nằm ở đây.** Mỗi Service ứng 1 module (`ITeamService`/
  `TeamService`...), method đặt tên theo hành vi nghiệp vụ (`CreateTeamAsync`,
  `JoinByCodeAsync`, `ApproveMemberAsync`...), không đặt tên kiểu CRUD chung chung.
- Method trả `Task<Result>` hoặc `Task<Result<T>>` — **không throw exception cho lỗi
  nghiệp vụ**, dùng `Result.Failure("thông báo tiếng Việt")` hoặc
  `Result.FieldFailure("TenField", "thông báo")`.
- Truy DB qua `IUnitOfWork`/`IGenericRepository<T>` injected.
- Thông báo (`Result.Message`, lỗi field) **luôn tiếng Việt có dấu**.

### `Utils/`
- Chỉ helper thuần, **không phụ thuộc `DbContext`/`IUnitOfWork`**: upload file (kiểm
  định dạng/dung lượng), format chuỗi/ngày tháng... `Result`/`Result<T>`/`PagedResult<T>`
  cũng nằm ở đây vì là kiểu dữ liệu dùng chung, không phải nghiệp vụ của module nào.

### `wwwroot/`
- HTML/CSS/JS thuần, không Razor. Mỗi màn hình cũ (`.cshtml`) tương ứng 1 file `.html`
  tĩnh + 1 file `js/pages/<ten>.js` gọi `fetch()` tới Controller cùng project rồi tự
  dựng DOM. `js/api.js` bọc sẵn envelope response (`{message,data}` /
  `{message,fieldErrors}`); `js/ui.js` có `escapeHtml`, `requireAuth`, gán lỗi field...
  — xem code trong `js/` để tái dùng thay vì viết lại.

## Việc còn thiếu

- [ ] Module AUTH thật (hiện đăng nhập là stub demo qua session, đọc `Controllers/DevAuthApiController.cs`).
- [ ] Các module chưa code: MKT, ORG, PRF, RNK, FA, LIVE, MAT-08..13, PEN, SPN, TOUR,
      BRK-08..13, GRL, ITG, ADM, MAT-01..07, NOT — xem bảng phân công ở trên, mỗi
      người code phần mình vào đúng `Controllers/DTOs/Services` theo luật ở trên.
- [ ] `Exceptions/` và `Hubs/` còn trống — dùng khi module LIVE/NOT cần SignalR.
- [ ] `AutoMapper`, `DotNetEnv`, `MailKit`, `MimeKit` đã có package nhưng **chưa được
      gọi ở đâu trong code** — dành cho ai làm module cần gửi email (AUTH, NOT).
