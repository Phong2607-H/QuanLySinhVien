# QUY TRÌNH THỰC HIỆN CÁC YÊU CẦU – DỰ ÁN QUẢN LÝ SINH VIÊN (TUẦN 1 → TUẦN 7)

> Người thực hiện: Nguyễn Thanh Phong
> Công nghệ: ASP.NET Core (.NET 10) Web API · EF Core · SQL Server · Angular 22 · xUnit · Moq · TestContainers · Docker
> Mỗi yêu cầu được trình bày theo 4 ý: **Mục tiêu → Quy trình (luồng chạy) → Code nằm ở đâu → Từ khóa**.

---

## Mục lục

0. [Tổng quan kiến trúc và đường đi của một request](#0-tổng-quan-kiến-trúc-và-đường-đi-của-một-request)
1. [Tuần 1 – Nền tảng](#1-tuần-1--nền-tảng)
2. [Tuần 2 – Tính năng chính](#2-tuần-2--tính-năng-chính)
3. [Tuần 3-4 – Exception, Audit Interceptor, quản lý file](#3-tuần-3-4--exception-audit-interceptor-quản-lý-file)
4. [Tuần 5-6 – Soft-Delete, tầng Service, Unit Test](#4-tuần-5-6--soft-delete-tầng-service-unit-test)
5. [Tuần 7 – Integration Test, Code Coverage, Optimistic Concurrency](#5-tuần-7--integration-test-code-coverage-optimistic-concurrency)
6. [Bảng tổng hợp](#6-bảng-tổng-hợp)

---

## 0. Tổng quan kiến trúc và đường đi của một request

```
Angular (localhost:4200)
   │  component gọi service  →  jwtInterceptor gắn token  →  HTTP request
   ▼
ASP.NET Core API (localhost:7280) – pipeline middleware theo thứ tự:
   ① ExceptionMiddleware      bọc try/catch toàn bộ phía sau
   ② UseStatusCodePages       401/403/404 rỗng → JSON chuẩn
   ③ UseHttpsRedirection
   ④ UseCors("AllowAngular")  cho phép localhost:4200 gọi
   ⑤ UseStaticFiles           phục vụ ảnh trong wwwroot/avatars
   ⑥ UseAuthentication        đọc + kiểm tra JWT
   ⑦ UseAuthorization         kiểm tra [Authorize(Roles=...)]
   ⑧ MapControllers           → Controller → Service → AppDbContext
   ▼
EF Core (Global Query Filter, AuditSaveChangesInterceptor) → SQL Server (QLSINHVIEN)
   ▼
Response JSON  →  errorInterceptor (nếu lỗi: hiện Toast)  →  component cập nhật giao diện
```

| Tầng | Nhiệm vụ |
|---|---|
| **Controller** | Nhận request, gọi Service, trả mã HTTP (200/201/204) |
| **Service** | Logic nghiệp vụ: kiểm tra trùng email, xóa mềm, phân trang, kiểm tra đụng độ |
| **AppDbContext** | Ánh xạ bảng, Global Query Filter |
| **Middleware / Interceptor** | Việc chung cho mọi request: bắt lỗi, ghi Audit |

---

## 1. Tuần 1 – Nền tảng

### 1.1. DTO (Data Transfer Object)

**Mục tiêu:** không trả thẳng Entity ra ngoài, chỉ gửi đúng các trường client cần.

**Quy trình:**
1. Tạo `SinhVienDto` gồm `Id, HoTen, Email, Tuoi, AvatarUrl, RowVersion` + các attribute validation (`[Required]`, `[EmailAddress]`, `[Range(18,99)]`).
2. Khi đọc dữ liệu: dùng `.Select(s => new SinhVienDto {...})` để EF chỉ SELECT các cột cần.
3. Khi ghi: nhận `SinhVienDto` từ request, tự gán từng trường sang Entity `SinhVien`.
4. Trường nội bộ như `IsDeleted`, `PasswordHash` không bao giờ đi ra ngoài.

**Code:** `DTOs/SinhVienDto.cs`, `Services/SinhVienService.cs` (`GetAllAsync`, `ToDto`).
**Từ khóa:** DTO, Entity, Projection, `.Select`, Over-posting.

### 1.2. Không hardcode cấu hình

**Quy trình:**
1. Đưa connection string và khóa JWT vào `appsettings.json` (`ConnectionStrings`, `JwtSettings`).
2. Đọc bằng `builder.Configuration.GetConnectionString(...)` và `GetSection("JwtSettings")`.
3. Nhờ vậy integration test có thể **ghi đè** connection string sang DB trong Docker mà không sửa code.

**Code:** `appsettings.json`, `Program.cs`. **Từ khóa:** IConfiguration, appsettings.

### 1.3. Lưu mật khẩu an toàn (BCrypt)

**Quy trình đăng ký:**
1. `POST /api/XacThuc/dangky` → kiểm tra rỗng (`BadRequestException`) → kiểm tra trùng tài khoản (`ConflictException`).
2. `BCrypt.HashPassword(password)` → tự sinh **salt** ngẫu nhiên và băm nhiều vòng → lưu vào `PasswordHash`.

**Quy trình đăng nhập:**
1. Tìm user theo `Username`.
2. `BCrypt.Verify(password, PasswordHash)` → sai thì trả chung một câu "Tài khoản hoặc mật khẩu không chính xác!" (không tiết lộ sai ở đâu).

**Code:** `Controllers/XacThucController.cs`. **Từ khóa:** Hash, Salt, BCrypt, one-way.

### 1.4. JWT (JSON Web Token)

**Quy trình:**
1. Đăng nhập đúng → `GenerateJwtToken` tạo token chứa claims: `NameIdentifier`, `Name`, `Role`, `FullName`, hạn 2 giờ, ký bằng `HmacSha256`.
2. Angular lưu token vào LocalStorage (`services/auth.ts`).
3. `jwtInterceptor` tự gắn `Authorization: Bearer <token>` vào mọi request.
4. Backend `AddJwtBearer` + `TokenValidationParameters` kiểm tra chữ ký, Issuer, Audience, hạn dùng.
5. Token hợp lệ → `HttpContext.User` có Role → `[Authorize(Roles="Admin")]` quyết định cho qua hay trả 403.

**Code:** `XacThucController.cs`, `Program.cs`, `interceptors/jwt.ts`, `services/auth.ts`.
**Từ khóa:** Claims, Bearer, Signature, Stateless authentication.

### 1.5. Lập trình bất đồng bộ (async/await)

**Quy trình:** mọi thao tác DB dùng phiên bản `...Async` (`ToListAsync`, `FindAsync`, `SaveChangesAsync`) và `await` → luồng xử lý được trả lại cho server trong lúc chờ DB, server phục vụ được nhiều request hơn.

**Từ khóa:** async/await, Task, non-blocking I/O.

### 1.6. Xử lý khi server sập / sai đăng nhập

**Quy trình:**
1. Request lỗi → `errorInterceptor` bắt bằng `catchError`.
2. `status === 0` (không kết nối được) → hiện câu "mất kết nối máy chủ" (`utils/error-message.ts`).
3. Sai mật khẩu → toast hiện ngay nhờ `ChangeDetectorRef.detectChanges()` (Angular chạy zoneless nên phải báo cập nhật giao diện thủ công).

**Code:** `interceptors/error.ts`, `utils/error-message.ts`, `login/login.ts`.
**Từ khóa:** HttpInterceptorFn, catchError, zoneless, change detection.

---

## 2. Tuần 2 – Tính năng chính

### 2.1. Phân trang, tìm kiếm, sắp xếp phía server

**Mục tiêu:** không tải toàn bộ bảng về client; SQL Server chỉ trả đúng 1 trang.

**Quy trình:**
1. Angular gửi `?pageNumber=2&pageSize=5&keyword=an&sortBy=hoten&isDescending=true`.
2. Model binding đổ vào `SinhVienQuery`.
3. Service chặn giá trị sai: `PageNumber < 1 → 1`, `PageSize` ngoài 1–50 → 5.
4. Xây truy vấn bằng `IQueryable` (chưa chạy SQL):
   - Lọc: `Where(HoTen.ToLower().Contains(keyword) || Email...)`
   - Sắp xếp: `switch (sortBy, isDescending)` → `OrderBy / OrderByDescending`
5. `CountAsync()` → tổng số dòng (để tính số trang).
6. `Skip((page-1)*size).Take(size)` → SQL sinh `OFFSET ... FETCH NEXT ...`.
7. Trả `PagedResult<T>` gồm `Items, TotalCount, PageNumber, PageSize, TotalPages`.

**Code:** `Services/SinhVienService.cs` (`GetAllAsync`), `DTOs/PagedResult.cs`, `DTOs/SinhVienQuery.cs`.
**Từ khóa:** IQueryable, deferred execution, Skip/Take, OFFSET FETCH, AsNoTracking.

### 2.2. Phân quyền 3 lớp

| Lớp | Cách làm | Bảo mật thật? |
|---|---|---|
| Giao diện | `*ngIf="authService.hasRole('Admin')"` ẩn nút Thêm/Sửa/Xóa | ❌ chỉ để gọn giao diện |
| Route | `roleGuard(['Admin'])` (`CanActivateFn`) chặn gõ URL `/lich-su` | ❌ |
| API | `[Authorize(Roles = "Admin")]` trên Create/Update/Delete | ✅ chặn cả Postman |

**Quy trình:** Role nằm trong JWT → Angular đọc Role để ẩn/hiện → Backend đọc Role từ token để cho phép hoặc trả 403.

**Code:** `guards/role.ts`, `app.routes.ts`, `SinhVienController.cs`.
**Từ khóa:** Authorization, Role-based, CanActivateFn, UrlTree.

### 2.3. Audit Log

**Mục tiêu:** ghi lại ai đã Thêm/Sửa/Xóa gì, lúc nào, giá trị cũ và mới.

**Quy trình:** bảng `AuditLogs` (Username, Action, TableName, OldValues, NewValues, Timestamp) → được ghi tự động bởi Interceptor (xem 3.2) → `AuditLogController.GetAll` (chỉ Admin) trả về `AuditLogDto` → trang `/lich-su` hiển thị.

**Code:** `Models/AuditLog.cs`, `Controllers/AuditLogController.cs`, `lich-su/`.

### 2.4. Upload ảnh đại diện

**Quy trình backend (`UploadAvatar`):**
1. Kiểm tra sinh viên tồn tại → 404 nếu không.
2. Kiểm tra file rỗng, đuôi `.jpg/.jpeg/.png`, dung lượng ≤ 2MB → 400 nếu sai.
3. Tạo thư mục `wwwroot/avatars`, đặt tên file bằng `Guid` (tránh trùng, tránh đoán tên).
4. Lưu file → cập nhật `AvatarUrl` → `SaveChangesAsync` (lỗi thì xóa file vừa ghi) → xóa ảnh cũ (3.3).

**Quy trình frontend:** gửi `FormData` với `reportProgress: true, observe: 'events'` → nhận `HttpEventType.UploadProgress` để tính % → `HttpEventType.Response` thì tải lại danh sách.

**Code:** `SinhVienController.cs`, `services/sinh-vien.ts`, `sinh-vien/sinh-vien.ts`.
**Từ khóa:** IFormFile, multipart/form-data, FormData, HttpEventType.

### 2.5. Chuẩn hóa lỗi + Toast

**Quy trình:** mọi lỗi từ backend đều có dạng `{statusCode, message, details}` → `errorInterceptor` đọc `message` → `ToastService.showError(message)` → 401 thì logout về `/login`, 403 thì về `/sinh-vien`. Component không tự hiện toast lỗi nữa để tránh hiện trùng.

**Code:** `DTOs/ErrorResponse.cs`, `interceptors/error.ts`, `services/toast.ts`.

---

## 3. Tuần 3-4 – Exception, Audit Interceptor, quản lý file

### 3.1. Exception Middleware

**Mục tiêu:** Controller/Service chỉ cần `throw`, một nơi duy nhất đổi exception thành JSON chuẩn.

**Quy trình:**
1. Tạo exception nghiệp vụ: `AppException(message, statusCode)` và các lớp con `BadRequestException (400)`, `ForbiddenException (403)`, `NotFoundException (404)`, `ConflictException (409)`.
2. `ExceptionMiddleware.InvokeAsync` bọc `await _next(context)` trong `try/catch`.
3. Có lỗi:
   - `Response.HasStarted` → không đổi được status code nữa → ghi log rồi ném lại.
   - `AppException` → `LogWarning` (lỗi người dùng); còn lại → `LogError` (lỗi hệ thống).
4. `switch` theo loại exception → (statusCode, message):
   - `AppException` → mã của nó · `UnauthorizedAccessException` → 401 · `ArgumentException` → 400
   - `DbUpdateConcurrencyException` → 409 (**đặt trước** `DbUpdateException` vì là lớp con) · `DbUpdateException` → 409
   - Khác → 500 "Đã xảy ra sự cố hệ thống!..."
5. `Details = StackTrace` **chỉ** khi môi trường Development; Production là `null`.
6. Lỗi validation (xảy ra trước Controller) → `InvalidModelStateResponseFactory` trả `ErrorResponse` 400.
7. 401/403/404 không phải exception → `UseStatusCodePages` viết JSON.

**Code:** `Exceptions/AppException.cs`, `Middleware/ExceptionMiddleware.cs`, `Program.cs`.
**Từ khóa:** Middleware, pipeline, RequestDelegate, HasStarted, global exception handling.

### 3.2. Audit bằng SaveChangesInterceptor

**Mục tiêu:** Controller không phải tự ghi log; mọi lần `SaveChanges` đều được ghi.

**Quy trình:**
1. `AuditSaveChangesInterceptor` kế thừa `SaveChangesInterceptor`, ghi đè `SavingChangesAsync` (chạy **trước** khi lưu).
2. Duyệt `ChangeTracker.Entries()`, bỏ qua `AuditLog`, `Detached`, `Unchanged`.
3. Xác định hành động: `Added` → Thêm · `Modified` → Sửa · `IsDeleted` chuyển true → Xóa.
4. Lấy `OriginalValue` (cũ) và `CurrentValue` (mới), bỏ các trường nhạy cảm (`PasswordHash`, `RowVersion`).
5. Lấy người thực hiện qua `IHttpContextAccessor` → `User.Identity.Name`.
6. Thêm dòng `AuditLog` vào cùng lần lưu → dữ liệu và log lưu chung một giao dịch.
7. Đăng ký: `AddScoped<AuditSaveChangesInterceptor>()` + `options.AddInterceptors(...)`.

**Code:** `Data/AuditSaveChangesInterceptor.cs`, `Program.cs`.
**Từ khóa:** Interceptor, ChangeTracker, EntityState, OriginalValue/CurrentValue, IHttpContextAccessor.

### 3.3. Xóa ảnh cũ khi upload ảnh mới

**Quy trình:** lưu `oldAvatarUrl` trước → ghi file mới + cập nhật DB → **chỉ khi DB lưu thành công** mới `File.Delete` ảnh cũ. Nếu DB lỗi thì xóa file mới vừa ghi. Thứ tự này đảm bảo không bao giờ mất ảnh đang dùng và không để lại file rác.

**Code:** `SinhVienController.UploadAvatar`.

---

## 4. Tuần 5-6 – Soft-Delete, tầng Service, Unit Test

### 4.1. Soft-Delete + Global Query Filter

**Mục tiêu:** "xóa" không làm mất dữ liệu, có thể khôi phục và vẫn còn lịch sử.

**Quy trình:**
1. Thêm cột `IsDeleted BIT DEFAULT 0` vào `SinhVien` và `Users`.
2. Xóa = `sv.IsDeleted = true; SaveChangesAsync()` → SQL là **UPDATE**, không phải DELETE.
3. `OnModelCreating`: `HasQueryFilter(s => !s.IsDeleted)` → EF tự thêm `WHERE IsDeleted = 0` vào **mọi** truy vấn.
4. Muốn xem cả dữ liệu đã xóa → `.IgnoreQueryFilters()`.
5. Interceptor thấy `IsDeleted` đổi sang true → ghi Audit "Xóa".

**Code:** `Data/AppDbContext.cs`, `Services/SinhVienService.DeleteAsync`.
**Từ khóa:** Soft delete, Global Query Filter, HasQueryFilter, IgnoreQueryFilters.

### 4.2. Tách tầng Service

**Quy trình:**
1. Tạo `SinhVienService` nhận `AppDbContext`, chứa toàn bộ logic: `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
2. Gom phần lặp "tìm, không thấy thì 404" vào `TimHoacBaoLoiAsync`.
3. Bỏ `State = EntityState.Modified` (EF đang theo dõi entity nên tự biết cột nào đổi).
4. Đăng ký `AddScoped<SinhVienService>()`; Controller chỉ còn gọi Service và trả mã HTTP.
5. Chạy lại integration test để chứng minh hành vi API không đổi.

**Code:** `Services/SinhVienService.cs`, `Controllers/SinhVienController.cs`.
**Từ khóa:** Separation of concerns, Dependency Injection, Scoped lifetime.

### 4.3. Unit Test (xUnit + EF InMemory + Moq)

**Quy trình:**
1. `TaoDb()` tạo DB InMemory với tên `Guid` riêng cho mỗi test + 3 sinh viên mẫu.
2. Viết test theo mẫu **AAA**: Arrange (chuẩn bị) → Act (gọi hàm) → Assert (kiểm tra).
3. `[Theory]` + `[InlineData]` chạy 9 lần để đi qua mọi nhánh sắp xếp.
4. Mỗi hàm có test trường hợp đúng và trường hợp ném lỗi (`Assert.ThrowsAsync<NotFoundException>` ...).
5. `ExceptionMiddlewareTests`: tạo `DefaultHttpContext`, giả lập `_next` ném exception, kiểm tra status code và JSON trả về (500, 401, 400, 404, Production `details = null`).

**Code:** `QuanLySinhVien.Tests/SinhVienServiceTests.cs`, `ExceptionMiddlewareTests.cs`.
**Từ khóa:** Unit test, AAA, Fact/Theory, InMemory provider, Mock.

---

## 5. Tuần 7 – Integration Test, Code Coverage, Optimistic Concurrency

### 5.1. Integration Test với TestContainers

**Mục tiêu:** kiểm tra cả luồng thật HTTP → Middleware → Controller → Service → EF Core → **SQL Server thật**.

**Quy trình:**
1. Thêm `public partial class Program { }` cuối `Program.cs` để project test thấy được `Program`.
2. `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`:
   - `InitializeAsync`: `MsSqlContainer.StartAsync()` bật SQL Server trong Docker.
   - `ConfigureWebHost`: `UseSetting("ConnectionStrings:DefaultConnection", container.GetConnectionString())`.
   - Chốt an toàn: connection string chứa `QLSINHVIEN` thì dừng ngay.
   - `EnsureCreatedAsync()` tạo bảng theo Model.
   - `DisposeAsync`: xóa container.
3. `SinhVienApiTests : IClassFixture<ApiFactory>` → cả class dùng chung 1 container.
4. Helper `TaoClientAsync(role)`: đăng ký → sửa Role trong DB → đăng nhập lấy JWT → gắn Bearer.
5. Dữ liệu ngẫu nhiên bằng `Guid` để các test độc lập.
6. 6 kịch bản: tạo rồi đọc lại (201) · email trùng (409) · xóa mềm (204 → 404, DB còn dòng) · phân quyền (401/403) · tuổi sai (400) · hai người cùng sửa (204 / 409).

**Code:** `IntegrationTests/ApiFactory.cs`, `IntegrationTests/SinhVienApiTest.cs`.
**Từ khóa:** Integration test, WebApplicationFactory, TestContainers, Docker, IClassFixture, IAsyncLifetime.

### 5.2. Code Coverage

**Mục tiêu:** đo tỉ lệ code tầng Service được test chạy qua, ≥ 80%.

**Quy trình:**
1. `coverlet.collector` (có sẵn trong project test) chèn bộ đếm vào từng dòng khi chạy test.
2. `dotnet test --collect:"XPlat Code Coverage"` → sinh `coverage.cobertura.xml`.
3. `reportgenerator ... -classfilters:"+QuanLySinhVien.Services.*"` → báo cáo HTML chỉ tính tầng Service.
4. Đọc báo cáo: xanh = đã chạy, đỏ = chưa chạy, vàng = nhánh chạy một phần → viết thêm test cho chỗ đỏ/vàng.

**Lưu ý:** coverage cao không có nghĩa là test tốt; chất lượng nằm ở các `Assert`. Branch coverage quan trọng hơn line coverage.

**Từ khóa:** Line/Branch coverage, Coverlet, ReportGenerator, instrumentation.

### 5.3. Optimistic Concurrency (RowVersion)

**Mục tiêu:** hai người cùng sửa một sinh viên → người lưu sau bị báo 409 thay vì âm thầm ghi đè (Lost Update).

**Quy trình:**
1. DB: `ALTER TABLE SinhVien ADD RowVersion ROWVERSION;` → SQL Server tự đổi giá trị mỗi khi dòng bị sửa.
2. Model: `[Timestamp] public byte[]? RowVersion` → EF tự thêm `AND RowVersion = @cũ` vào mọi UPDATE.
3. DTO có `RowVersion` (JSON tự chuyển thành chuỗi base64) → client nhận khi đọc.
4. Angular: `sua()` giữ `rowVersion` của lúc mở form, `update()` gửi lại nguyên vẹn.
5. Service `UpdateAsync`:
   - Thiếu `RowVersion` → 400.
   - `Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion` (dùng phiên bản của client, không phải bản vừa đọc từ DB).
   - `SaveChangesAsync` → UPDATE ảnh hưởng 0 dòng → `DbUpdateConcurrencyException` → `ConflictException` 409.
6. Angular nhận 409 → toast (do interceptor) → `lamMoiForm()` + `taiDanhSach()` để lấy dữ liệu mới.
7. Không ghi `RowVersion` vào Audit Log.

**Code:** `Models/SinhVien.cs`, `DTOs/SinhVienDto.cs`, `Services/SinhVienService.UpdateAsync`, `sinh-vien/sinh-vien.ts`, integration test `HaiNguoiCungSua_NguoiSauBi409`.
**Từ khóa:** Optimistic concurrency, rowversion, `[Timestamp]`, concurrency token, OriginalValue, Lost update.

---

## 6. Bảng tổng hợp

| Tuần | Yêu cầu | File chính | Từ khóa |
|---|---|---|---|
| 1 | DTO | `DTOs/SinhVienDto.cs` | Projection, `.Select` |
| 1 | Cấu hình | `appsettings.json`, `Program.cs` | IConfiguration |
| 1 | Hash mật khẩu | `XacThucController.cs` | BCrypt, Salt |
| 1 | JWT + Interceptor | `XacThucController.cs`, `jwt.ts` | Claims, Bearer |
| 1 | Lỗi kết nối / đăng nhập | `error.ts`, `error-message.ts` | catchError, zoneless |
| 2 | Phân trang/lọc/sắp xếp | `SinhVienService.GetAllAsync` | IQueryable, Skip/Take |
| 2 | Phân quyền 3 lớp | `role.ts`, `[Authorize(Roles)]` | CanActivateFn |
| 2 | Audit Log | `AuditLogController.cs`, `lich-su/` | Audit trail |
| 2 | Upload ảnh | `UploadAvatar`, `sinh-vien.ts` | IFormFile, HttpEventType |
| 2 | Chuẩn hóa lỗi + Toast | `ErrorResponse.cs`, `error.ts` | Interceptor |
| 3-4 | Exception Middleware | `ExceptionMiddleware.cs`, `AppException.cs` | Global exception handling |
| 3-4 | Audit Interceptor | `AuditSaveChangesInterceptor.cs` | ChangeTracker |
| 3-4 | Xóa ảnh cũ | `UploadAvatar` | File.Delete |
| 5-6 | Soft-Delete | `AppDbContext.cs`, `DeleteAsync` | HasQueryFilter |
| 5-6 | Tầng Service | `SinhVienService.cs` | Separation of concerns |
| 5-6 | Unit Test | `SinhVienServiceTests.cs`, `ExceptionMiddlewareTests.cs` | AAA, Theory |
| 7 | Integration Test | `ApiFactory.cs`, `SinhVienApiTest.cs` | TestContainers |
| 7 | Code Coverage | `CoverageReport/index.html` | Coverlet, ReportGenerator |
| 7 | Optimistic Concurrency | `SinhVien.cs`, `UpdateAsync` | rowversion, 409 |
