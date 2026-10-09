# Ý CHÍNH · TRỌNG TÂM · KEYWORD – TUẦN 1 → TUẦN 7

> Bản "học nhanh" để ôn trước khi trình bày. Mỗi yêu cầu gồm:
> **Ý chính** (làm gì) · **🎯 Trọng tâm** (câu quan trọng nhất phải nói được) · **🔑 Keyword**.

---

## TUẦN 1 – NỀN TẢNG

### 1. DTO (Data Transfer Object)
**Ý chính**
- Không trả thẳng Entity ra ngoài; dùng `SinhVienDto` chỉ chứa trường cần thiết.
- Trường nội bộ (`IsDeleted`, `PasswordHash`) không bao giờ lộ ra.
- Đặt luật kiểm tra trên DTO: `[Required]`, `[EmailAddress]`, `[Range(18,99)]` → sai tự trả 400.
- Đọc: `.Select` chiếu thẳng sang DTO; ghi: gán từng trường sang Entity.

🎯 **Trọng tâm:** DTO là "hợp đồng" giữa API và client – chống lộ dữ liệu và chống **Over-posting**.

🔑 DTO · Entity · Projection · `.Select` · Over-posting · Data Annotations · `[ApiController]` · Model Validation

### 2. Không hardcode cấu hình
**Ý chính**
- Connection string, khóa JWT đặt trong `appsettings.json`.
- Đọc bằng `IConfiguration`: `GetConnectionString(...)`, `GetSection("JwtSettings")`.
- Đổi môi trường không phải sửa code; Tuần 7 test ghi đè sang DB Docker.

🎯 **Trọng tâm:** Tách **cấu hình theo môi trường** ra khỏi code.

🔑 appsettings.json · IConfiguration · ConnectionStrings · Dependency Injection · User Secrets · Environment Variables

### 3. Mật khẩu an toàn (BCrypt)
**Ý chính**
- Đăng ký: `BCrypt.HashPassword` (tự sinh salt, băm nhiều vòng) → lưu `PasswordHash`.
- Đăng nhập: `BCrypt.Verify` so mật khẩu nhập với hash.
- Sai tên hay sai mật khẩu đều báo **cùng một câu**.
- Tự đăng ký mặc định role `GiangVien`.

🎯 **Trọng tâm:** Hash là **một chiều** + **salt** → không ai đọc được mật khẩu gốc, cùng mật khẩu vẫn ra hash khác nhau.

🔑 Hash · Salt · BCrypt · One-way · Rainbow table · Work factor · User enumeration

### 4. Xác thực JWT
**Ý chính**
- Đăng nhập đúng → tạo token chứa claims (Id, Username, Role, FullName), hạn 2 giờ, ký `HmacSha256`.
- Angular lưu token vào localStorage; `jwtInterceptor` tự gắn `Authorization: Bearer`.
- Backend `AddJwtBearer` kiểm tra chữ ký, Issuer, Audience, Lifetime.
- `UseAuthentication()` trước `UseAuthorization()`.

🎯 **Trọng tâm:** Payload **đọc được nhưng không sửa được** – an toàn nằm ở **chữ ký**; server **stateless**.

🔑 JWT · Header.Payload.Signature · Claims · Bearer token · HmacSha256 · Stateless · TokenValidationParameters · HttpInterceptor · Authentication

### 5. Bất đồng bộ (async/await)
**Ý chính**
- Mọi thao tác DB dùng `...Async` + `await` (`ToListAsync`, `FindAsync`, `SaveChangesAsync`).
- Trong lúc chờ DB, luồng được trả về để phục vụ request khác.

🎯 **Trọng tâm:** Async **không làm 1 request nhanh hơn**, mà giúp server **chịu được nhiều request cùng lúc**.

🔑 async/await · Task · Thread pool · Non-blocking I/O · Scalability

### 6. Xử lý server sập / sai đăng nhập
**Ý chính**
- `layThongBaoLoi()`: `status === 0` → "Không thể kết nối máy chủ"; có `message` → dùng message.
- Đăng nhập sai hiện ngay nhờ `detectChanges()` (vì app zoneless).

🎯 **Trọng tâm:** Người dùng **luôn nhận được thông báo rõ ràng**, không treo, không im lặng.

🔑 HttpErrorResponse · status 0 · catchError · Zoneless · Change Detection · ChangeDetectorRef.detectChanges

---

## TUẦN 2 – TÍNH NĂNG CHÍNH

### 7. Phân trang, tìm kiếm, sắp xếp phía server
**Ý chính**
- Nhận `SinhVienQuery` (PageNumber, PageSize, Keyword, SortBy, IsDescending) qua `[FromQuery]`.
- Chặn giá trị sai (page < 1 → 1; size ngoài 1–50 → 5).
- `IQueryable`: `Where` lọc → `switch` sắp xếp → `CountAsync` → `Skip/Take`.
- Trả `PagedResult<T>` (Items, TotalCount, PageNumber, PageSize, TotalPages).

🎯 **Trọng tâm:** Mọi xử lý chạy **trong SQL Server**, chỉ trả về **đúng một trang** – dữ liệu lớn mấy cũng không chậm.

🔑 Server-side pagination · IQueryable · Deferred execution · Skip/Take · OFFSET FETCH · AsNoTracking · Model binding · Generic `PagedResult<T>`

### 8. Phân quyền 3 lớp
**Ý chính**
- Giao diện: `*ngIf="hasRole('Admin')"` ẩn nút.
- Route: `roleGuard(['Admin'])` chặn gõ URL `/lich-su`.
- API: `[Authorize(Roles = "Admin")]` cho Thêm/Sửa/Xóa, AuditLog.
- `getRole()` đọc role từ **JWT** → sửa localStorage không có tác dụng.

🎯 **Trọng tâm:** Hai lớp đầu chỉ là **trải nghiệm**; **bảo mật thật nằm ở API** vì role lấy từ token có chữ ký. 401 = chưa xác thực, 403 = không đủ quyền.

🔑 Authorization · Role-based (RBAC) · `[Authorize(Roles)]` · CanActivateFn · Route Guard · UrlTree · 401 vs 403 · Never trust the client

### 9. Audit Log
**Ý chính**
- Bảng `AuditLogs`: Username, Action, TableName, OldValues, NewValues, Timestamp.
- API `AuditLogController` chỉ Admin; trang `/lich-su` hiển thị mới nhất lên đầu.
- Tuần 3-4 chuyển sang ghi tự động bằng Interceptor.

🎯 **Trọng tâm:** Truy vết được **ai – làm gì – lúc nào – trước/sau ra sao**.

🔑 Audit trail · OldValues/NewValues · Traceability · Admin-only

### 10. Upload ảnh đại diện
**Ý chính**
- Kiểm tra: sinh viên tồn tại, file rỗng, đuôi `.jpg/.jpeg/.png`, ≤ 2MB.
- Tên file mới bằng `Guid`, lưu `wwwroot/avatars`, DB chỉ lưu **đường dẫn**.
- Angular gửi `FormData`, `reportProgress` + `observe: 'events'` để hiện %.

🎯 **Trọng tâm:** **Kiểm tra trước khi lưu** + **đổi tên bằng Guid** để không trùng, không ghi đè, không đoán được.

🔑 IFormFile · multipart/form-data · FormData · wwwroot · UseStaticFiles · Guid · HttpEventType.UploadProgress

### 11. Chuẩn hóa lỗi + Toast
**Ý chính**
- Mọi lỗi dạng `{ statusCode, message, details }`.
- `errorInterceptor` bắt mọi lỗi → toast; 401 → logout về login; 403 → về `/sinh-vien`.
- `ToastService` dùng `Subject`; chưa có ai nghe thì cất tạm `sessionStorage`.

🎯 **Trọng tâm:** **Một chỗ xử lý lỗi** cho toàn bộ app → thông báo thống nhất, không trùng.

🔑 HttpInterceptorFn · catchError · throwError · Toast · RxJS Subject · ErrorResponse

---

## TUẦN 3-4 – CHUYÊN NGHIỆP HÓA

### 12. Dứt điểm Exception Handling
**Ý chính**
- Exception riêng: `NotFoundException` (404), `BadRequestException` (400), `ConflictException` (409).
- `ExceptionMiddleware` đặt **đầu tiên** pipeline, bắt mọi exception → JSON chuẩn.
- `details` chỉ trả khi **Development**; kiểm tra `Response.HasStarted`.
- `InvalidModelStateResponseFactory` (lỗi validation 400) và `UseStatusCodePages` (401/403/404) cũng ra cùng định dạng.
- Code nghiệp vụ chỉ việc `throw`; Angular đọc `message`.

🎯 **Trọng tâm:** **Mọi lỗi đi qua một cửa**, trả **một định dạng JSON duy nhất**, không lộ stack trace ra ngoài.

🔑 Middleware · Pipeline order · Global exception handling · Custom exception · Pattern matching `switch` · ErrorResponse · IHostEnvironment.IsDevelopment · InvalidModelStateResponseFactory · UseStatusCodePages · Cross-cutting concern

### 13. Audit Logging bằng SaveChangesInterceptor
**Ý chính**
- Interceptor chạy **ngay trước** `SaveChanges`, duyệt `ChangeTracker.Entries()`.
- `EntityState` Added/Modified/Deleted → Thêm/Sửa/Xóa; so `OriginalValue` với `CurrentValue`, chỉ ghi cột `IsModified`.
- Lấy username từ `IHttpContextAccessor` (claim trong JWT).
- Bỏ qua `PasswordHash`, `RowVersion`; không tự ghi log cho chính bảng AuditLog.
- Đăng ký `AddScoped` + `options.AddInterceptors(...)`.

🎯 **Trọng tâm:** Ghi log **tự động ở tầng EF Core** – Controller **không còn một dòng log nào**, không thể quên.

🔑 SaveChangesInterceptor · SavingChangesAsync · ChangeTracker · EntityState · OriginalValue/CurrentValue · IsModified · IHttpContextAccessor · Claims · Sensitive properties · AOP / Cross-cutting

### 14. Xóa ảnh cũ khi đổi ảnh
**Ý chính**
- Lưu `oldAvatarUrl` → ghi file mới → cập nhật DB.
- DB lỗi → **xóa file mới** vừa ghi rồi `throw`.
- DB thành công → **mới xóa file cũ**.

🎯 **Trọng tâm:** **Thứ tự an toàn**: chỉ xóa file cũ **sau khi** lưu DB thành công → không bao giờ mất ảnh đang dùng, không để lại rác.

🔑 File cleanup · Orphan file · Compensating action · `File.Exists` / `File.Delete` · WebRootPath · Path.Combine

---

## TUẦN 5-6 – BẢO VỆ DỮ LIỆU & KIỂM CHỨNG CODE

### 15. Xóa mềm (Soft Delete) + Global Query Filter
**Ý chính**
- Thêm cột `IsDeleted` (`BIT NOT NULL DEFAULT 0`).
- `DeleteAsync` chỉ đặt `IsDeleted = true` → SQL là **UPDATE**, không phải DELETE.
- `HasQueryFilter(s => !s.IsDeleted)` khai báo **một lần** trong `OnModelCreating`.
- Muốn xem dữ liệu đã xóa: `IgnoreQueryFilters()`.
- **Chỉ áp dụng cho Sinh viên**, không cho tài khoản. Xóa lại lần 2 → 404.

🎯 **Trọng tâm:** **Khai báo một lần, áp dụng mọi nơi** – mọi truy vấn tự ẩn dòng đã xóa, dữ liệu vẫn còn trong DB.

🔑 Soft Delete · Hard Delete · IsDeleted flag · Global Query Filter · HasQueryFilter · OnModelCreating · IgnoreQueryFilters · Data retention

### 16. Tách tầng Service + Unit Test
**Ý chính**
- `SinhVienService` chứa nghiệp vụ (GetAll, GetById, Create, Update, Delete); Controller chỉ gọi Service và trả mã HTTP.
- Service **không biết HTTP**, sai thì ném exception → Middleware đổi mã.
- Đăng ký `AddScoped` (cùng vòng đời với DbContext).
- Test: xUnit + EF **InMemory**, mỗi test một DB `Guid` riêng, mẫu **Arrange–Act–Assert**.
- Test cả happy path và mọi đường lỗi; `[Theory]` + 9 `[InlineData]` phủ các nhánh sắp xếp.

🎯 **Trọng tâm:** **Mỗi lớp một trách nhiệm** → nghiệp vụ test được độc lập, không cần HTTP, không cần SQL Server.

🔑 Service Layer · Separation of Concerns · Thin Controller · Dependency Injection · AddScoped · Refactor · Unit Test · xUnit · `[Fact]` / `[Theory]` / `[InlineData]` · Arrange–Act–Assert · EF Core InMemory · `Assert.ThrowsAsync` · Test isolation

---

## TUẦN 7 – CHỨNG MINH TRONG ĐIỀU KIỆN THẬT

### 17. Integration Test với TestContainers
**Ý chính**
- `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime` chạy toàn bộ API trong bộ nhớ.
- `MsSqlContainer` bật **SQL Server 2022 thật trong Docker**, xong tự xóa.
- `UseSetting` ghi đè connection string; chốt an toàn chặn trỏ vào `QLSINHVIEN`.
- `EnsureCreated` tạo bảng; `IClassFixture` dùng chung 1 container cho cả lớp.
- `public partial class Program {}` để test thấy lớp Program (top-level statements).
- 6 kịch bản: tạo + đọc lại, email trùng 409, xóa mềm, 401/403, dữ liệu sai 400, hai người cùng sửa 409.

🎯 **Trọng tâm:** Test đi **đúng đường request thật** (HTTP → Middleware → JWT → Controller → Service → **SQL Server thật**) mà **không đụng DB thật**.

🔑 Integration Test · TestContainers · Docker · WebApplicationFactory · IAsyncLifetime · IClassFixture · UseSetting · EnsureCreated · top-level statements · partial class · HttpClient · Bearer token

### 18. Code Coverage ≥ 80% (tầng Service)
**Ý chính**
- Chạy `dotnet test --collect:"XPlat Code Coverage"` (Coverlet có sẵn).
- `reportgenerator ... -classfilters:"+QuanLySinhVien.Services.*"` → báo cáo HTML.
- Xanh = đã chạy, đỏ = chưa test, vàng = nhánh mới chạy một phía.
- Đọc chỗ đỏ để bổ sung test; `SinhVienService` đạt ≥ 80%.

🎯 **Trọng tâm:** Coverage cho biết **dòng nào đã được test chạy qua**, **không** chứng minh code đúng – chất lượng nằm ở **Assert**.

🔑 Code Coverage · Line coverage · Branch coverage · Coverlet · XPlat Code Coverage · Cobertura · ReportGenerator · classfilters

### 19. Optimistic Concurrency với RowVersion
**Ý chính**
- Vấn đề **Lost Update**: hai người cùng sửa, người sau ghi đè người trước.
- Cột `rowversion` – **SQL Server tự tăng** mỗi lần UPDATE; Model `[Timestamp] byte[] RowVersion`.
- DTO mang RowVersion (JSON Base64); Angular `sua()` giữ `rowVersion`, gửi lại khi lưu.
- Service: thiếu RowVersion → 400; gán `Entry(sv).Property(...).OriginalValue = dto.RowVersion`.
- `UPDATE ... WHERE RowVersion = cũ` không trúng dòng → `DbUpdateConcurrencyException` → `ConflictException` 409.
- Angular: 409 đụng độ → đóng form + tải lại; 409 email trùng → giữ form.
- Audit bỏ qua RowVersion; integration test `HaiNguoiCungSua_NguoiSauBi409` chứng minh.

🎯 **Trọng tâm:** **Không khóa, chỉ kiểm tra lúc lưu**. Dòng mấu chốt là **`OriginalValue = dto.RowVersion`** – so với phiên bản **lúc mở form**, không phải phiên bản vừa đọc.

🔑 Lost Update · Optimistic Concurrency · Pessimistic Locking · rowversion · `[Timestamp]` · Concurrency token · OriginalValue · DbUpdateConcurrencyException · 409 Conflict · Atomic update · Base64

---

## BẢNG TÓM TẮT 1 DÒNG

| # | Yêu cầu | Trọng tâm 1 câu |
|---|---|---|
| 1 | DTO | Chỉ trao đổi trường cần thiết, chống lộ dữ liệu & over-posting |
| 2 | Cấu hình | Tách cấu hình môi trường khỏi code |
| 3 | BCrypt | Hash một chiều + salt |
| 4 | JWT | Đọc được nhưng không sửa được nhờ chữ ký |
| 5 | async/await | Server chịu nhiều request hơn |
| 6 | Server sập | Luôn có thông báo rõ ràng |
| 7 | Phân trang server | SQL chỉ trả đúng một trang |
| 8 | Phân quyền 3 lớp | Bảo mật thật nằm ở API |
| 9 | Audit Log | Ai – làm gì – lúc nào – trước/sau |
| 10 | Upload ảnh | Kiểm tra trước khi lưu, tên Guid |
| 11 | Lỗi + Toast | Một chỗ xử lý lỗi cho cả app |
| 12 | Exception Middleware | Mọi lỗi qua một cửa, một định dạng JSON |
| 13 | Audit Interceptor | Ghi log tự động ở tầng EF Core |
| 14 | Xóa ảnh cũ | Chỉ xóa cũ sau khi lưu DB thành công |
| 15 | Soft Delete | Khai báo filter một lần, áp dụng mọi nơi |
| 16 | Service + Unit Test | Mỗi lớp một trách nhiệm → test được |
| 17 | Integration Test | Đi đúng đường thật trên SQL Server thật |
| 18 | Coverage | Đo dòng đã chạy, không đo đúng/sai |
| 19 | Concurrency | Không khóa, kiểm tra lúc lưu bằng RowVersion |

## 3 NGUYÊN LÝ XUYÊN SUỐT
1. **Không tin client** – bảo mật thật luôn ở server (JWT có chữ ký, `[Authorize]`, validation DTO).
2. **Viết một lần, áp dụng mọi nơi** – interceptor, middleware, query filter.
3. **Đo được và kiểm chứng được** – unit test, integration test, coverage.
