# BẢN CHẤT · CƠ CHẾ · CÁCH THỰC HIỆN · KEYWORD – TUẦN 1 → TUẦN 7

> Dựa trên code hiện tại của dự án Quản lý Sinh viên (`D:\TT\QuanLySinhVien`, `D:\TT\QuanLySinhVienAngular`).

Mỗi yêu cầu gồm 4 mục:
- **Bản chất** – nó *là gì*, giải quyết *vấn đề gì* (kèm ví von).
- **Cơ chế** – bên trong hệ thống nó *chạy thế nào*.
- **Cách thực hiện** – trong đồ án em *đã làm gì, ở file nào*.
- **Keyword** – các từ khóa cần nói được.

---

# TUẦN 1 – NỀN TẢNG

## 1. DTO và kiểm tra dữ liệu đầu vào
**Bản chất:** DTO là lớp **trung gian chỉ để vận chuyển dữ liệu qua API**, tách khỏi Entity (lớp ánh xạ bảng DB). Giải quyết: lộ cột nội bộ, client gửi lên trường không được phép (over-posting), dữ liệu rác vào DB.
*Ví von:* Entity là hồ sơ gốc trong kho; DTO là bản photo chỉ có những trang được phép đưa ra quầy.

**Cơ chế:** Request đến → **Model Binding** đọc JSON đổ vào DTO → **Model Validation** đọc các attribute → nhờ `[ApiController]`, sai thì **tự trả 400**, hàm Controller không chạy. Khi đọc, `Select` chuyển Entity → DTO ngay trong câu SQL.

**Cách thực hiện:**
- `DTOs/SinhVienDto.cs`: `Id, HoTen, Email, Tuoi, AvatarUrl, RowVersion` + `[Required]`, `[EmailAddress]`, `[Range(18, 99)]`, câu lỗi tiếng Việt. Không có `IsDeleted`.
- `DangKyDto`, `DangNhapDto` cho tài khoản – không có `PasswordHash`.
- Service đọc bằng `.Select(s => new SinhVienDto {...})`; ghi bằng cách gán từng trường DTO → Entity.

**Keyword:** DTO · Entity · Over-posting · Model Binding · Model Validation · Data Annotations · `[ApiController]` · Projection

## 2. Không hardcode cấu hình
**Bản chất:** Tách các giá trị **phụ thuộc môi trường** (chuỗi kết nối, khóa bí mật) ra khỏi code.
*Ví von:* đổi đèn chỉ cần gạt công tắc, không phải đục tường đi lại dây.

**Cơ chế:** ASP.NET nạp `appsettings.json`, `appsettings.{Env}.json`, biến môi trường… vào một **kho cấu hình** chung (`IConfiguration`). Code hỏi theo tên khóa. Nguồn nạp sau **đè** nguồn trước → test ghi đè được chuỗi kết nối.

**Cách thực hiện:**
- `appsettings.json`: `ConnectionStrings:DefaultConnection`, `JwtSettings: Secret, Issuer, Audience`.
- `Program.cs`: `GetConnectionString("DefaultConnection")`, `GetSection("JwtSettings")`.
- `XacThucController` nhận `IConfiguration` qua constructor (DI).

**Keyword:** appsettings.json · IConfiguration · ConnectionStrings · Dependency Injection · Environment · User Secrets

## 3. Lưu mật khẩu an toàn (BCrypt)
**Bản chất:** Chỉ lưu **chuỗi băm** của mật khẩu – lộ DB cũng không lộ mật khẩu.
*Ví von:* máy xay sinh tố – ra nước ép thì dễ, biến ngược lại thành trái cây thì không thể.

**Cơ chế:** Hash là **một chiều**. BCrypt tự sinh **salt** ngẫu nhiên (cùng mật khẩu ra hash khác nhau) và **cố tình chậm** (nhiều vòng băm) để chống dò. Chuỗi `$2a$11$...` chứa sẵn salt và số vòng. Đăng nhập: `Verify` lấy salt từ chuỗi đã lưu, băm lại mật khẩu vừa nhập rồi so.

**Cách thực hiện (`XacThucController`):**
- Đăng ký: kiểm tra rỗng (400) → trùng tài khoản (409) → `BCrypt.HashPassword` → lưu `PasswordHash`; role mặc định `GiangVien`.
- Đăng nhập: `FirstOrDefaultAsync` → `BCrypt.Verify` → sai tên hay sai mật khẩu đều báo **cùng một câu** (chống dò tài khoản).

**Keyword:** Hash · One-way · Salt · BCrypt · Work factor · Verify · User enumeration

## 4. Xác thực bằng JWT
**Bản chất:** "Vé ra vào" **có chữ ký** do server phát sau khi đăng nhập, để các request sau chứng minh "tôi là ai, quyền gì" (HTTP không nhớ request trước).
*Ví von:* vé xem phim có dấu mộc – ai cũng đọc được thông tin trên vé, nhưng không ai sửa được vì dấu mộc chỉ rạp có.

**Cơ chế:** Token = `Header.Payload.Signature`. Payload (claims: Id, Name, Role, FullName, hạn) chỉ mã hóa Base64 – **đọc được, không sửa được**. Signature = băm(Header+Payload) bằng khóa bí mật. Server tính lại chữ ký để tin nội dung → không cần lưu phiên (**stateless**).

**Cách thực hiện:**
- `GenerateJwtToken`: claims `NameIdentifier, Name, Role, FullName`; `HmacSha256`; Issuer/Audience; hạn 2 giờ.
- `Program.cs`: `AddAuthentication` + `AddJwtBearer` (`ValidateIssuer/Audience/Lifetime/IssuerSigningKey`); `UseAuthentication()` trước `UseAuthorization()`.
- Angular: `AuthService.login` lưu token vào `localStorage`; `jwtInterceptor` gắn `Authorization: Bearer <token>` vào mọi request.

**Keyword:** JWT · Claims · Signature · HmacSha256 · Bearer token · Stateless · TokenValidationParameters · HttpInterceptor · localStorage

## 5. Lập trình bất đồng bộ (async/await)
**Bản chất:** Chờ thao tác chậm (DB, file, mạng) **mà không giữ luồng xử lý**.
*Ví von:* phục vụ đưa phiếu cho bếp rồi đi phục vụ bàn khác, không đứng chờ món chín.

**Cơ chế:** Gặp `await` một thao tác I/O → luồng hiện tại được **trả về thread pool** phục vụ request khác → DB xong thì một luồng tiếp tục phần sau `await`. Không làm 1 request nhanh hơn, nhưng server **chịu được nhiều request cùng lúc**.

**Cách thực hiện:** mọi hàm truy cập DB là `async Task<...>` và dùng `ToListAsync`, `FindAsync`, `AnyAsync`, `CountAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`; hàm tự viết có đuôi `Async`.

**Keyword:** async/await · Task · Thread pool · Non-blocking I/O · Scalability

## 6. Xử lý khi server sập / đăng nhập sai
**Bản chất:** Giao diện **luôn báo lỗi rõ ràng**, không treo, không im lặng.

**Cơ chế:** Không có phản hồi HTTP nào (server tắt, CORS chặn) → `status = 0`. Có phản hồi lỗi → đọc `message` trong JSON. Angular chạy **zoneless** (không có Zone.js tự phát hiện thay đổi) → phải báo vẽ lại bằng `detectChanges()`.

**Cách thực hiện:** `utils/error-message.ts` – hàm dùng chung `layThongBaoLoi()`; `login.ts` gán `errorMessage` rồi `cdr.detectChanges()` để lỗi hiện ngay lần bấm đầu.

**Keyword:** HttpErrorResponse · status 0 · Zoneless · Change Detection · ChangeDetectorRef

---

# TUẦN 2 – TÍNH NĂNG CHÍNH

## 7. Phân trang, tìm kiếm, sắp xếp phía server
**Bản chất:** Để **SQL Server** lọc – sắp xếp – cắt trang, chỉ trả về **đúng một trang**, thay vì tải cả bảng về trình duyệt.
*Ví von:* viết phiếu order rồi đưa bếp nấu một lần, chỉ bưng ra 5 món cần ăn.

**Cơ chế:** `IQueryable` + **thực thi trễ**: `Where`, `OrderBy`, `Skip`, `Take` chỉ ghép điều kiện, **chưa chạy SQL**; đến `CountAsync`/`ToListAsync` mới sinh **một câu SQL** với `WHERE ... ORDER BY ... OFFSET x ROWS FETCH NEXT y ROWS ONLY`.

**Cách thực hiện:**
- `SinhVienQuery` (`PageNumber, PageSize, Keyword, SortBy, IsDescending`) nhận qua `[FromQuery]`.
- `SinhVienService.GetAllAsync`: chặn tham số sai (trang < 1 → 1; size ngoài 1–50 → 5) → `AsNoTracking` → lọc họ tên/email không phân biệt hoa thường → `switch` sắp xếp theo danh sách cột cố định (mặc định Id) → `CountAsync` → `Skip/Take` → `Select` DTO.
- Trả `PagedResult<T>` (`Items, TotalCount, PageNumber, PageSize, TotalPages`).

**Keyword:** Server-side pagination · IQueryable · Deferred execution · Skip/Take · OFFSET FETCH · AsNoTracking · PagedResult\<T\> · `[FromQuery]`

## 8. Phân quyền 3 lớp
**Bản chất:** Admin được Thêm/Sửa/Xóa, Giảng viên chỉ xem. Chặn ở 3 lớp, trong đó **chỉ lớp API là bảo mật thật**.
*Ví von:* biển chỉ dẫn và lễ tân giúp tiện, nhưng **cửa quẹt thẻ** mới là an ninh.

**Cơ chế:** Giao diện và route chạy trên trình duyệt (sửa được). `[Authorize(Roles)]` chạy trên server, đọc role từ **JWT có chữ ký** (không giả được). Không token → **401**; sai role → **403**.

**Cách thực hiện:**
- Giao diện: `*ngIf="authService.hasRole('Admin')"`.
- Route: `roleGuard(['Admin'])` cho `/lich-su`, trả `UrlTree`.
- API: `[Authorize]` cả controller; `[Authorize(Roles = "Admin")]` cho Create/Update/Delete và `AuditLogController`.
- `AuthService.getRole()` giải mã Payload JWT lấy role thật → sửa `role` trong localStorage không có tác dụng.

**Keyword:** Authentication vs Authorization · Role-based (RBAC) · `[Authorize(Roles)]` · Route Guard · CanActivateFn · UrlTree · 401 vs 403

## 9. Audit Log
**Bản chất:** **Truy vết** ai – làm gì – lúc nào – giá trị trước/sau.
*Ví von:* sổ giao ca ghi mọi thay đổi.

**Cơ chế:** Mỗi thay đổi dữ liệu sinh một dòng trong `AuditLogs`; giá trị cũ/mới lưu dạng **JSON** nên một bảng log dùng cho mọi bảng. (Tự động hóa ở Tuần 3-4.)

**Cách thực hiện:** `Models/AuditLog.cs`; `AuditLogController.GetAll` (chỉ Admin, mới nhất trước, trả `AuditLogDto`); trang `/lich-su`.

**Keyword:** Audit trail · OldValues / NewValues · Traceability

## 10. Upload ảnh đại diện
**Bản chất:** Nhận file ảnh **an toàn**: kiểm tra trước khi lưu, không trùng, không ghi đè.
*Ví von:* bưu điện cân và kiểm tra hàng, dán mã vận đơn mới, cất kho, chỉ ghi mã kệ vào sổ.

**Cơ chế:** Angular gửi `multipart/form-data` → ASP.NET nhận thành `IFormFile` → lưu file vào `wwwroot/avatars`, DB chỉ lưu **đường dẫn** → `UseStaticFiles` phục vụ file qua URL. `reportProgress` cho Angular biết số byte đã gửi.

**Cách thực hiện (`UploadAvatar`):** kiểm tra sinh viên tồn tại → file rỗng → đuôi `.jpg/.jpeg/.png` (whitelist) → ≤ 2MB → tên file `Guid` → `FileStream` + `CopyToAsync` → cập nhật `AvatarUrl`. Angular: `FormData`, `observe: 'events'`, `HttpEventType.UploadProgress` để tính %.

**Keyword:** IFormFile · multipart/form-data · FormData · wwwroot · UseStaticFiles · Guid · Whitelist · UploadProgress

## 11. Chuẩn hóa lỗi + Toast
**Bản chất:** **Một chỗ** xử lý lỗi cho toàn bộ giao diện → thông báo thống nhất.

**Cơ chế:** `errorInterceptor` đứng giữa mọi response; `catchError` bắt lỗi → Toast → 401 thì đăng xuất về `/login`, 403 về `/sinh-vien` → `throwError` ném tiếp để component tự xử lý thêm. `ToastService` dùng **RxJS `Subject`**: một nơi phát, `ToastComponent` nghe; chưa có ai nghe thì cất tạm `sessionStorage`.

**Cách thực hiện:** `interceptors/error.ts`, `services/toast.ts`, `toast/`; trang đăng nhập/đăng ký không Toast trùng với banner.

**Keyword:** HttpInterceptorFn · catchError · throwError · RxJS Subject · Toast · sessionStorage

---

# TUẦN 3-4 – CHUYÊN NGHIỆP HÓA

## 12. Exception Handling tập trung (Middleware)
**Bản chất:** **Mọi lỗi đi qua một cửa**, trả về **một định dạng JSON duy nhất** `{statusCode, message, details}`; không lộ stack trace ra ngoài.
*Ví von:* phòng tiếp nhận khiếu nại duy nhất, trả lời khách theo một mẫu thống nhất.

**Cơ chế:** Middleware là "lớp vỏ" bọc các tầng phía sau. `ExceptionMiddleware` đặt **đầu pipeline**, gọi `await _next(context)` trong `try` → exception từ Controller/Service/EF đều **nổi lên** và rơi vào `catch` → **pattern matching** theo kiểu exception để chọn mã HTTP → ghi JSON. Lỗi **không phải exception** (validation 400, 401/403/404 tự sinh) được xử lý bằng `InvalidModelStateResponseFactory` và `UseStatusCodePages`.

**Cách thực hiện:**
- `Exceptions/AppException.cs`: lớp cha `AppException` (có `StatusCode`) + `BadRequest(400)`, `Forbidden(403)`, `NotFound(404)`, `Conflict(409)`.
- `DTOs/ErrorResponse.cs`: `StatusCode, Message, Details, Errors`.
- `Middleware/ExceptionMiddleware.cs`: kiểm tra `HasStarted`; `LogWarning` cho lỗi nghiệp vụ, `LogError` cho lỗi hệ thống; `details` chỉ ở Development; JSON camelCase.
- `Program.cs`: `UseMiddleware<ExceptionMiddleware>()` đầu tiên; `InvalidModelStateResponseFactory`; `SuppressMapClientErrors`; `UseStatusCodePages`.
- Code nghiệp vụ chỉ `throw`; Angular đọc `message`.
- `ExceptionMiddlewareTests` (5 test, Moq).

**Keyword:** Middleware · Pipeline order · Global Exception Handling · Custom Exception · Pattern matching · ErrorResponse · IsDevelopment · InvalidModelStateResponseFactory · UseStatusCodePages · Cross-cutting concern

## 13. Audit Logging bằng SaveChangesInterceptor
**Bản chất:** Ghi lịch sử **tự động ở tầng EF Core** – Controller không viết dòng log nào, không thể quên.
*Ví von:* camera ở cửa kho tự ghi mọi thứ ra vào, vì mọi thứ đều phải qua đúng một cửa.

**Cơ chế:** EF gọi `SavingChangesAsync` của interceptor **ngay trước** khi lưu. Lúc đó **ChangeTracker** biết bản ghi nào `Added / Modified / Deleted`, mỗi cột có `OriginalValue`, `CurrentValue`, `IsModified`. Interceptor tạo các dòng `AuditLog` và thêm vào **cùng lần lưu**. Tên người dùng lấy từ JWT qua `IHttpContextAccessor`.

**Cách thực hiện (`Data/AuditSaveChangesInterceptor.cs`):** bỏ qua `AuditLog`, `Detached`, `Unchanged`; `IsDeleted = true` → hành động "Xóa"; Sửa chỉ ghi cột `IsModified`; bỏ `PasswordHash`, `RowVersion`; đăng ký `AddHttpContextAccessor`, `AddScoped<...>`, `options.AddInterceptors(...)`.

**Keyword:** SaveChangesInterceptor · SavingChangesAsync · ChangeTracker · EntityState · OriginalValue / CurrentValue · IsModified · IHttpContextAccessor · Sensitive properties

## 14. Xóa ảnh cũ khi đổi ảnh
**Bản chất:** Dọn file rác mà **không bao giờ làm mất ảnh đang dùng**.
*Ví von:* chuyển nhà – làm xong giấy tờ ở nhà mới rồi mới trả nhà cũ.

**Cơ chế – thứ tự an toàn:** nhớ ảnh cũ → ghi file mới → lưu DB. DB lỗi → xóa **file mới** + ném lỗi. DB thành công → **mới** xóa **file cũ**.

**Cách thực hiện (`UploadAvatar`):** `oldAvatarUrl` → `try { SaveChangesAsync } catch { File.Delete(filePath); throw; }` → `Path.Combine(WebRootPath, oldAvatarUrl.TrimStart('/'))` → `File.Exists` → `File.Delete`.

**Keyword:** Orphan file · File cleanup · Compensating action · WebRootPath · Path.Combine

---

# TUẦN 5-6 – BẢO VỆ DỮ LIỆU VÀ KIỂM CHỨNG CODE

## 15. Xóa mềm + Global Query Filter
**Bản chất:** Xóa = **đánh dấu**, dữ liệu vẫn còn; mọi truy vấn **tự động ẩn** dòng đã đánh dấu. Chỉ áp dụng cho **Sinh viên**.
*Ví von:* Thùng rác trên máy tính – file vẫn còn, Explorer mặc định không hiện.

**Cơ chế:** Xóa đổi `IsDeleted = true` → EF sinh **UPDATE** (không phải DELETE). `HasQueryFilter` khai báo **một lần** → EF tự gắn `WHERE IsDeleted = 0` vào **mọi** truy vấn trên SinhVien (kể cả `Find`, `Count`, `Any`). Cần thấy dòng đã xóa → `IgnoreQueryFilters()`.

**Cách thực hiện:** SQL `ALTER TABLE SinhVien ADD IsDeleted BIT NOT NULL DEFAULT 0`; Model `bool IsDeleted`; `AppDbContext.OnModelCreating` → `modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted)`; `DeleteAsync` đặt `IsDeleted = true`. Angular không phải sửa (API giữ nguyên).

**Keyword:** Soft Delete · Hard Delete · IsDeleted flag · Global Query Filter · HasQueryFilter · OnModelCreating · IgnoreQueryFilters

## 16. Tách tầng Service + Unit Test
**Bản chất:** **Mỗi lớp một trách nhiệm** – Controller lo HTTP, Service lo nghiệp vụ – để nghiệp vụ **test được độc lập**.
*Ví von:* phục vụ nhận order, bếp nấu; thử món mới chỉ cần vào bếp, không cần mở cửa đón khách.

**Cơ chế:** Service **không phụ thuộc HTTP**, nhận `AppDbContext` qua **DI** → khi test chỉ cần `new SinhVienService(dbGiả)` với **EF Core InMemory**. Service gặp lỗi thì **ném exception**, Middleware đổi thành mã HTTP.

**Cách thực hiện:**
- `Services/SinhVienService.cs`: `GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync` + `TimHoacBaoLoiAsync`, `ToDto`; đăng ký `AddScoped`.
- Controller chỉ còn gọi Service + `CreatedAtAction` / `NoContent`.
- `SinhVienServiceTests` (21 ca): `TaoDb()` mỗi test một DB `Guid`; Arrange–Act–Assert; `[Theory]` 9 bộ dữ liệu cho sắp xếp; `Assert.ThrowsAsync` cho các nhánh lỗi; `Delete_LaXoaMem` dùng `IgnoreQueryFilters`.

**Keyword:** Service Layer · Separation of Concerns · Thin Controller · Dependency Injection · AddScoped · Unit Test · xUnit · `[Fact]` / `[Theory]` · Arrange–Act–Assert · EF Core InMemory · Moq

---

# TUẦN 7 – CHỨNG MINH TRONG ĐIỀU KIỆN THẬT

## 17. Integration Test với TestContainers
**Bản chất:** Kiểm tra **các tầng ghép lại** chạy đúng, đi **đúng đường của request thật** xuống **SQL Server thật**, mà không đụng DB thật.
*Ví von:* lắp cả chiếc xe chạy thử, trên một đoạn đường dựng riêng cho buổi thử rồi dỡ đi.

**Cơ chế:** `WebApplicationFactory<Program>` chạy **toàn bộ API trong bộ nhớ** và cấp `HttpClient`. **TestContainers** ra lệnh **Docker** bật SQL Server 2022 sạch, dùng xong xóa. Chuỗi kết nối bị **ghi đè** sang container. Request đi qua Middleware → JWT → `[Authorize]` → validation → Controller → Service → EF → SQL.

**Cách thực hiện:**
- `Program.cs`: `public partial class Program { }`.
- `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`: `MsSqlBuilder(...2022-latest)`, `UseSetting(ConnectionStrings:DefaultConnection, ...)`, `StartAsync`, chốt chặn `QLSINHVIEN`, `EnsureCreatedAsync`, `DisposeAsync`.
- `SinhVienApiTests : IClassFixture<ApiFactory>`: helper `TaoClientAsync(role)` (đăng ký → đổi role → đăng nhập → gắn Bearer), `SinhVienMoi()` (email Guid); **6 kịch bản**: tạo + đọc lại, email trùng 409, xóa mềm, 401/403, tuổi sai 400, hai người cùng sửa 409.

**Keyword:** Integration Test · TestContainers · Docker · WebApplicationFactory · `public partial class Program` · IAsyncLifetime · IClassFixture · UseSetting · EnsureCreated · HttpClient

## 18. Code Coverage ≥ 80% (tầng Service)
**Bản chất:** Đo **bao nhiêu phần trăm code đã được test chạy qua** – chỉ ra chỗ chưa được kiểm tra. Đo **độ rộng**, không đo **độ đúng**.
*Ví von:* đội kiểm tra đi qua các phòng – biết phòng nào chưa ai vào, không đảm bảo phòng đã vào là an toàn.

**Cơ chế:** **Coverlet** chèn bộ đếm vào từng dòng/nhánh của DLL (instrumentation) → test chạy thì đếm → xuất `coverage.cobertura.xml` → **ReportGenerator** vẽ HTML (xanh/đỏ/vàng). Integration test cũng được tính vì API chạy cùng tiến trình test.

**Cách thực hiện:**
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```
Lần đo gần nhất: `SinhVienService` Line **100%**, Branch **96,8%** (cần đo lại sau lần sửa cuối của Service).

**Keyword:** Code Coverage · Line coverage · Branch coverage · Coverlet · ReportGenerator · XPlat Code Coverage · classfilters

## 19. Optimistic Concurrency với RowVersion
**Bản chất:** Chống **Lost Update** – hai người cùng sửa, người lưu sau **không được âm thầm ghi đè** người trước. Cách lạc quan: **không khóa, chỉ kiểm tra lúc lưu**.
*Ví von:* nộp tài liệu có số phiên bản – bản gốc của bạn là v7 mà máy chủ đã lên v8 thì bị từ chối.

**Cơ chế:** Cột `rowversion` do **SQL Server tự tăng** mỗi lần UPDATE. Client giữ phiên bản lúc mở form và gửi lại. EF sinh `UPDATE ... WHERE Id = @id AND RowVersion = @cũ` – so sánh và ghi trong **một câu SQL nguyên tử**. Trúng 0 dòng → `DbUpdateConcurrencyException` → 409.

**Cách thực hiện:**
- SQL `ADD RowVersion ROWVERSION`; Model `[Timestamp] byte[]? RowVersion`; DTO mang RowVersion (Base64 trong JSON).
- `UpdateAsync`: Id lệch 400 → thiếu RowVersion 400 → không thấy 404 → email trùng 409 → **`Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion`** → `SaveChangesAsync` trong `try` → `catch (DbUpdateConcurrencyException)` ném `ConflictException`.
- Audit bỏ qua RowVersion.
- Angular: `sua()` giữ `rowVersion`; 409 đụng độ → `lamMoiForm()` + `taiDanhSach()`; 409 email trùng → giữ form.
- Test: `Update_ThieuRowVersion_NemBadRequest`, integration `HaiNguoiCungSua_NguoiSauBi409`.

**Keyword:** Lost Update · Optimistic Concurrency · Pessimistic Locking · rowversion · `[Timestamp]` · Concurrency token · OriginalValue · DbUpdateConcurrencyException · 409 Conflict · Atomic

---

# BẢNG ÔN NHANH

| # | Yêu cầu | Bản chất trong 1 câu |
|---|---|---|
| 1 | DTO | Chỉ trao đổi trường được phép, kiểm tra ngay từ cửa |
| 2 | Cấu hình | Tách giá trị theo môi trường khỏi code |
| 3 | BCrypt | Băm một chiều + salt |
| 4 | JWT | Vé có chữ ký: đọc được, không sửa được |
| 5 | async/await | Chờ I/O không giữ luồng |
| 6 | Server sập | Luôn có thông báo rõ ràng |
| 7 | Phân trang server | SQL chỉ trả đúng một trang |
| 8 | Phân quyền 3 lớp | Bảo mật thật nằm ở API |
| 9 | Audit Log | Ai – gì – lúc nào – trước/sau |
| 10 | Upload ảnh | Kiểm tra trước khi lưu, tên Guid |
| 11 | Lỗi + Toast | Một chỗ xử lý lỗi giao diện |
| 12 | Exception Middleware | Mọi lỗi qua một cửa, một định dạng |
| 13 | Audit Interceptor | Log tự động ở tầng EF |
| 14 | Xóa ảnh cũ | Lưu DB xong mới xóa cũ |
| 15 | Soft Delete | Khai báo filter một lần, áp dụng mọi nơi |
| 16 | Service + Unit Test | Mỗi lớp một trách nhiệm → test được |
| 17 | Integration Test | Đường thật, DB thật, dùng một lần |
| 18 | Coverage | Đo độ rộng của test, không đo độ đúng |
| 19 | Concurrency | Không khóa, so phiên bản lúc lưu |

**3 nguyên lý xuyên suốt:** Không tin client · Viết một lần, áp dụng mọi nơi · Mọi tính năng đều kiểm chứng được.
