# BÁO CÁO KẾT QUẢ THỰC HIỆN – TUẦN 1 ĐẾN TUẦN 7
## Dự án: Hệ thống Quản lý Sinh viên

| | |
|---|---|
| **Người thực hiện** | Nguyễn Thanh Phong |
| **Mã nguồn backend** | `D:\TT\QuanLySinhVien` |
| **Mã nguồn frontend** | `D:\TT\QuanLySinhVienAngular` |
| **Thời gian** | Tuần 1 – Tuần 7 |
| **Ngày lập báo cáo** | 05/10/2026 |

---

## 1. TỔNG QUAN DỰ ÁN

### 1.1. Mục tiêu
Xây dựng hệ thống quản lý sinh viên gồm Web API và giao diện web. Hệ thống cho phép đăng nhập, phân quyền Admin / Giảng viên, quản lý danh sách sinh viên (thêm, sửa, xóa, tìm kiếm, phân trang, ảnh đại diện) và ghi lại lịch sử thao tác. Qua từng tuần, hệ thống được nâng cấp về bảo mật, xử lý lỗi, toàn vẹn dữ liệu và kiểm thử.

### 1.2. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core Web API – **.NET 10** |
| Truy cập dữ liệu | **Entity Framework Core 10** (SQL Server provider) |
| Cơ sở dữ liệu | **SQL Server Express** (`MSI\SQLEXPRESS`), database `QLSINHVIEN` |
| Xác thực | **JWT Bearer**, mật khẩu băm bằng **BCrypt.Net-Next** |
| Frontend | **Angular 22** (standalone component, zoneless, SSR) |
| Kiểm thử | **xUnit**, **Moq**, **EF Core InMemory**, **Microsoft.AspNetCore.Mvc.Testing**, **Testcontainers.MsSql**, **Coverlet**, **ReportGenerator** |
| Hạ tầng test | **Docker Desktop** (WSL2) |

### 1.3. Kiến trúc và luồng xử lý một request

```
[Angular]  Component → Service (HttpClient)
              ↓ jwtInterceptor: gắn Authorization: Bearer <token>
              ↓ errorInterceptor: bắt lỗi → Toast / điều hướng
[ASP.NET]  ExceptionMiddleware → UseStatusCodePages → HTTPS → CORS → StaticFiles
              → UseAuthentication (JWT) → UseAuthorization ([Authorize])
              → Controller (mỏng) → SinhVienService (nghiệp vụ)
              → AppDbContext (Global Query Filter + AuditSaveChangesInterceptor)
              → SQL Server
```

### 1.4. Cấu trúc mã nguồn chính

| Thư mục / File | Nội dung |
|---|---|
| `Controllers/` | `SinhVienController`, `XacThucController`, `AuditLogController` |
| `Services/SinhVienService.cs` | Nghiệp vụ sinh viên |
| `Data/AppDbContext.cs` | DbContext + Global Query Filter |
| `Data/AuditSaveChangesInterceptor.cs` | Ghi lịch sử tự động |
| `Middleware/ExceptionMiddleware.cs` | Xử lý lỗi tập trung |
| `Exceptions/AppException.cs` | `BadRequest`, `Forbidden`, `NotFound`, `Conflict` Exception |
| `DTOs/` | `SinhVienDto`, `SinhVienQuery`, `PagedResult<T>`, `ErrorResponse`, `AuditLogDto`, `DangKyDto`, `DangNhapDto` |
| `Models/` | `SinhVien`, `NguoiDung`, `AuditLog` |
| `QuanLySinhVien.Tests/` | `SinhVienServiceTests`, `ExceptionMiddlewareTests`, `IntegrationTests/ApiFactory`, `IntegrationTests/SinhVienApiTest` |
| Angular `src/app/` | `services/`, `interceptors/`, `guards/`, `utils/`, các trang `login`, `register`, `sinh-vien`, `lich-su`, `toast` |

---

## 2. BẢNG TỔNG HỢP TRẠNG THÁI CÁC YÊU CẦU

| Tuần | # | Yêu cầu | Trạng thái |
|---|---|---|---|
| 1 | 1.1 | DTO và kiểm tra dữ liệu đầu vào | ✅ Hoàn thành |
| 1 | 1.2 | Không hardcode cấu hình | ✅ Hoàn thành |
| 1 | 1.3 | Lưu mật khẩu an toàn (BCrypt) | ✅ Hoàn thành |
| 1 | 1.4 | Xác thực JWT | ✅ Hoàn thành |
| 1 | 1.5 | Lập trình bất đồng bộ (async/await) | ✅ Hoàn thành |
| 1 | 1.6 | Xử lý khi server sập / đăng nhập sai | ✅ Hoàn thành |
| 2 | 2.1 | Phân trang, tìm kiếm, sắp xếp phía server | ✅ Hoàn thành (còn 1 điểm nhỏ về hiển thị, xem mục 5) |
| 2 | 2.2 | Phân quyền 3 lớp (giao diện, route, API) | ✅ Hoàn thành |
| 2 | 2.3 | Audit Log và trang Lịch sử | ✅ Hoàn thành |
| 2 | 2.4 | Upload ảnh đại diện | ✅ Hoàn thành |
| 2 | 2.5 | Chuẩn hóa lỗi + Toast | ✅ Hoàn thành |
| 3-4 | 3.1 | Dứt điểm Exception Handling (JSON chuẩn, Angular đọc được) | ✅ Hoàn thành |
| 3-4 | 3.2 | Audit Logging bằng `SaveChangesInterceptor` | ✅ Hoàn thành |
| 3-4 | 3.3 | Xóa file ảnh cũ khi cập nhật ảnh | ✅ Hoàn thành |
| 5-6 | 4.1 | Xóa mềm (Soft Delete) + Global Query Filter | ✅ Hoàn thành |
| 5-6 | 4.2 | Tách tầng Service + Unit Test | ✅ Hoàn thành |
| 7 | 5.1 | Integration Test với TestContainers (≥ 2 kịch bản) | ✅ Hoàn thành – **6 kịch bản** |
| 7 | 5.2 | Code Coverage tầng Service ≥ 80% | ✅ Hoàn thành – lần đo gần nhất: Line **100%**, Branch **96,8%** |
| 7 | 5.3 | Optimistic Concurrency với RowVersion | ✅ Hoàn thành |

---

## 3. CHI TIẾT KẾT QUẢ TỪNG TUẦN

### TUẦN 1 – XÂY DỰNG NỀN TẢNG

#### 1.1. DTO và kiểm tra dữ liệu đầu vào
- **Yêu cầu:** không trả trực tiếp Entity ra ngoài; kiểm tra dữ liệu gửi lên.
- **Kết quả thực hiện:**
  - `SinhVienDto` gồm `Id, HoTen, Email, Tuoi, AvatarUrl, RowVersion`; không chứa `IsDeleted`.
  - Ràng buộc: `[Required]` cho họ tên và email, `[EmailAddress]`, `[Range(18, 99)]` cho tuổi, kèm thông báo tiếng Việt.
  - Đọc dữ liệu bằng `.Select(s => new SinhVienDto {...})` để EF chỉ lấy cột cần thiết; ghi dữ liệu bằng cách gán từng trường từ DTO sang Entity (chống over-posting).
  - Tài khoản dùng `DangKyDto`, `DangNhapDto`; `PasswordHash` không bao giờ trả ra ngoài.
- **Minh chứng:** integration test `TaoSinhVien_TuoiKhongHopLe_Tra400` – tuổi 10 trả **400** với thông báo "Tuổi phải là số dương từ 18 đến 99!".

#### 1.2. Không hardcode cấu hình
- **Kết quả:** chuỗi kết nối (`ConnectionStrings:DefaultConnection`) và cấu hình JWT (`JwtSettings: Secret, Issuer, Audience`) nằm trong `appsettings.json`, đọc qua `IConfiguration`.
- **Minh chứng:** integration test ghi đè chuỗi kết nối sang SQL Server trong Docker bằng `UseSetting(...)` mà không sửa mã nguồn.

#### 1.3. Lưu mật khẩu an toàn
- **Kết quả (`XacThucController`):**
  - Đăng ký: kiểm tra rỗng (400), kiểm tra trùng tài khoản (409), băm bằng `BCrypt.HashPassword` (tự sinh salt).
  - Đăng nhập: `BCrypt.Verify`; sai tài khoản hoặc sai mật khẩu đều trả chung thông báo "Tài khoản hoặc mật khẩu không chính xác!" để tránh dò tài khoản.
  - Tài khoản tự đăng ký mặc định role `GiangVien`.
- **Minh chứng:** cột `PasswordHash` trong bảng `Users` chỉ chứa chuỗi băm dạng `$2a$11$...`.

#### 1.4. Xác thực JWT
- **Kết quả:**
  - `GenerateJwtToken` tạo token chứa claim `NameIdentifier`, `Name`, `Role`, `FullName`; hạn 2 giờ; ký `HmacSha256`.
  - `Program.cs` cấu hình `AddJwtBearer` kiểm tra Issuer, Audience, Lifetime, chữ ký; pipeline `UseAuthentication()` trước `UseAuthorization()`.
  - Angular: `AuthService` lưu token; `jwtInterceptor` tự gắn header `Authorization: Bearer` vào mọi request; có kiểm tra môi trường trình duyệt để tương thích SSR.
- **Minh chứng:** integration test `PhanQuyen_401Va403` – không có token trả **401**.

#### 1.5. Lập trình bất đồng bộ
- **Kết quả:** toàn bộ thao tác cơ sở dữ liệu dùng phương thức `...Async` với `await` (`ToListAsync`, `FindAsync`, `AnyAsync`, `CountAsync`, `SaveChangesAsync`).

#### 1.6. Xử lý khi server sập / đăng nhập sai
- **Kết quả:**
  - Hàm dùng chung `layThongBaoLoi()` (`utils/error-message.ts`): `status = 0` → "Không thể kết nối đến máy chủ..."; có `message` từ API → hiển thị đúng message.
  - Ứng dụng chạy zoneless nên trang đăng nhập gọi `ChangeDetectorRef.detectChanges()` để thông báo lỗi hiện ngay lần bấm đầu tiên.
- **Minh chứng:** tắt backend rồi đăng nhập → hiện thông báo mất kết nối; nhập sai mật khẩu → thông báo hiện ngay.

---

### TUẦN 2 – CÁC TÍNH NĂNG CHÍNH

#### 2.1. Phân trang, tìm kiếm, sắp xếp phía server
- **Kết quả:**
  - API nhận `SinhVienQuery` (`PageNumber`, `PageSize`, `Keyword`, `SortBy`, `IsDescending`) qua `[FromQuery]`.
  - Chuẩn hóa tham số: `PageNumber < 1` → 1; `PageSize` ngoài khoảng 1–50 → 5.
  - Xây truy vấn bằng `IQueryable` + `AsNoTracking()`: lọc theo họ tên/email (không phân biệt hoa thường), sắp xếp bằng biểu thức `switch` trên danh sách cột cố định (họ tên, email, tuổi, mặc định Id), `CountAsync` lấy tổng, `Skip/Take` lấy một trang (SQL `OFFSET ... FETCH`).
  - Trả `PagedResult<T>` gồm `Items, TotalCount, PageNumber, PageSize, TotalPages`.
  - Angular: tìm kiếm đưa về trang 1, bấm cột lần 2 đảo chiều sắp xếp, tự lùi trang khi xóa dòng cuối của trang.
- **Minh chứng:** unit test `GetAll_SapXep_DungThuTu` (9 trường hợp sắp xếp) và `GetAll_TimKiem_VaChanPhanTrangSai`.

#### 2.2. Phân quyền 3 lớp
- **Kết quả:**

  | Lớp | Cách làm |
  |---|---|
  | Giao diện | `*ngIf="authService.hasRole('Admin')"` ẩn form, nút Sửa/Xóa, nút Lịch sử |
  | Route | `roleGuard(['Admin'])` cho `/lich-su`; `authGuard` cho `/sinh-vien`; trả `UrlTree` để điều hướng |
  | API | `[Authorize]` toàn controller; `[Authorize(Roles = "Admin")]` cho Thêm/Sửa/Xóa và `AuditLogController`; Upload ảnh cho `Admin,GiangVien` |

  - `AuthService.getRole()` giải mã payload JWT để lấy role thật; nếu role trong localStorage bị sửa khác token thì tự khôi phục và báo lỗi.
- **Minh chứng:** integration test `PhanQuyen_401Va403` – Giảng viên thêm sinh viên trả **403**; sửa role trong localStorage không mở được chức năng Admin.

#### 2.3. Audit Log và trang Lịch sử
- **Kết quả:** bảng `AuditLogs` (Username, Action, TableName, OldValues, NewValues, Timestamp); `AuditLogController.GetAll` (chỉ Admin) trả `AuditLogDto` sắp xếp mới nhất trước; trang `/lich-su` hiển thị danh sách.
- Cơ chế ghi được nâng cấp ở Tuần 3-4 (mục 3.2).

#### 2.4. Upload ảnh đại diện
- **Kết quả (`SinhVienController.UploadAvatar`):**
  - Kiểm tra: sinh viên tồn tại (404), file rỗng (400), đuôi `.jpg/.jpeg/.png` (400), dung lượng ≤ 2MB (400).
  - Lưu vào `wwwroot/avatars` với tên `Guid` + đuôi file; cơ sở dữ liệu chỉ lưu đường dẫn `AvatarUrl`.
  - Angular gửi `FormData` với `reportProgress: true, observe: 'events'` để hiển thị phần trăm tải lên.

#### 2.5. Chuẩn hóa lỗi + Toast
- **Kết quả:** `errorInterceptor` bắt mọi lỗi HTTP, hiện Toast bằng `layThongBaoLoi()`; 401 → đăng xuất và về `/login`; 403 → về `/sinh-vien`; trang đăng nhập/đăng ký không hiện Toast trùng với banner. `ToastService` dùng RxJS `Subject`, lưu tạm vào `sessionStorage` khi đang chuyển trang.

---

### TUẦN 3-4 – HOÀN THIỆN XỬ LÝ LỖI, AUDIT VÀ QUẢN LÝ FILE

#### 3.1. Dứt điểm Exception Handling
- **Yêu cầu:** mọi lỗi API trả về JSON `{statusCode, message, details}`; Angular đọc được định dạng này.
- **Kết quả:**
  - Bộ exception nghiệp vụ kế thừa `AppException` (có `StatusCode`): `BadRequestException` (400), `ForbiddenException` (403), `NotFoundException` (404), `ConflictException` (409).
  - `ExceptionMiddleware` đăng ký **đầu tiên** trong pipeline:
    - Kiểm tra `Response.HasStarted` trước khi ghi.
    - Ghi log `Warning` cho lỗi nghiệp vụ, `Error` cho lỗi hệ thống.
    - Ánh xạ bằng pattern matching: `AppException` → mã của nó; `UnauthorizedAccessException` → 401; `ArgumentException`/`BadHttpRequestException` → 400; `DbUpdateConcurrencyException`, `DbUpdateException` → 409; còn lại → 500.
    - `details` (stack trace) **chỉ** có khi môi trường Development.
  - Lỗi không phát sinh từ exception cũng cùng định dạng: `InvalidModelStateResponseFactory` (lỗi validation 400, kèm `errors` theo từng trường) và `UseStatusCodePages` (401/403/404).
  - Angular: model `ApiError`, `layThongBaoLoi()` đọc `message`; `errorInterceptor` in `details` ra Console khi có (phục vụ debug).
- **Minh chứng:** `ExceptionMiddlewareTests` (5 test, dùng Moq cho `ILogger`, `IHostEnvironment`): lỗi bất ngờ → 500 đúng định dạng; `UnauthorizedAccessException` → 401; `ArgumentException` → 400; môi trường Production → `details = null`; `NotFoundException` → 404. Integration test `TaoSinhVien_EmailTrung_Tra409` kiểm tra JSON 409 qua HTTP thật.

#### 3.2. Audit Logging bằng SaveChangesInterceptor
- **Kết quả (`AuditSaveChangesInterceptor`):**
  - Ghi đè `SavingChangesAsync`, chạy ngay trước khi lưu; duyệt `ChangeTracker.Entries()`, bỏ qua bản ghi `AuditLog`, `Detached`, `Unchanged`.
  - Xác định hành động: `Added` → Thêm; `Modified` → Sửa; `Deleted` hoặc `IsDeleted = true` (xóa mềm) → Xóa.
  - Ghi `OldValues`/`NewValues` dạng JSON; với Sửa chỉ ghi các cột `IsModified`.
  - Lấy tên người thực hiện từ JWT qua `IHttpContextAccessor`; không có thì "Anonymous".
  - Bỏ qua thuộc tính nhạy cảm/kỹ thuật: `PasswordHash`, `RowVersion`.
  - Đăng ký `AddScoped<AuditSaveChangesInterceptor>()` và `options.AddInterceptors(...)` trong `AddDbContext`; Controller không còn mã ghi log thủ công.
- **Minh chứng:** sửa tuổi sinh viên → trang Lịch sử có dòng "Sửa" với giá trị cũ và mới; đăng ký tài khoản → log không chứa `PasswordHash`.

#### 3.3. Xóa file ảnh cũ khi cập nhật
- **Kết quả (`UploadAvatar`):** lưu `oldAvatarUrl` → ghi file mới → cập nhật DB. Nếu lưu DB lỗi thì xóa file mới vừa ghi và ném tiếp lỗi; chỉ khi lưu DB thành công mới xóa file cũ trong `wwwroot`.
- **Minh chứng:** đổi ảnh một sinh viên → thư mục `wwwroot/avatars` chỉ còn file mới.

---

### TUẦN 5-6 – XÓA MỀM, TẦNG SERVICE VÀ UNIT TEST

#### 4.1. Xóa mềm + Global Query Filter
- **Kết quả:**
  - Thêm cột `IsDeleted BIT NOT NULL DEFAULT 0` vào bảng `SinhVien`; thuộc tính `IsDeleted` trong Model.
  - `DeleteAsync` đặt `IsDeleted = true` (câu lệnh `UPDATE`, không `DELETE`).
  - `AppDbContext.OnModelCreating`: `HasQueryFilter(s => !s.IsDeleted)` – mọi truy vấn trên `SinhVien` tự loại bản ghi đã xóa; xóa lần hai trả 404.
  - Theo quyết định phạm vi: **chỉ áp dụng cho Sinh viên**, không áp dụng cho tài khoản (đã gỡ `IsDeleted` khỏi Model `NguoiDung`).
- **Minh chứng:** unit test `Delete_LaXoaMem` và integration test `XoaSinhVien_LaXoaMem` – API trả 404 sau khi xóa nhưng `IgnoreQueryFilters()` vẫn thấy bản ghi với `IsDeleted = true`.

#### 4.2. Tách tầng Service + Unit Test
- **Kết quả:**
  - `SinhVienService` chứa nghiệp vụ: `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, cùng hàm dùng chung `TimHoacBaoLoiAsync` và `ToDto`. Service ném exception nghiệp vụ, không phụ thuộc HTTP.
  - `SinhVienController` chỉ gọi Service và trả mã HTTP; đăng ký `AddScoped<SinhVienService>()`.
  - `SinhVienServiceTests`: xUnit + EF Core InMemory, mỗi test một DB riêng (tên `Guid`), dữ liệu mẫu 3 sinh viên, mẫu Arrange–Act–Assert.

  | Nhóm | Test | Số ca |
  |---|---|---|
  | GetAll | `GetAll_SapXep_DungThuTu` (`[Theory]`), `GetAll_TimKiem_VaChanPhanTrangSai` | 10 |
  | GetById | tồn tại / không tồn tại | 2 |
  | Create | hợp lệ / email trùng (không phân biệt hoa thường) | 2 |
  | Update | hợp lệ / Id lệch / thiếu RowVersion / không tồn tại / email trùng | 5 |
  | Delete | xóa mềm / không tồn tại | 2 |
  | **Tổng** | | **21** |

---

### TUẦN 7 – KIỂM THỬ NÂNG CAO, CHẤT LƯỢNG CODE, XỬ LÝ ĐỤNG ĐỘ

#### 5.1. Integration Test với TestContainers
- **Yêu cầu:** ít nhất 2 kịch bản gọi API từ Controller xuống DB và pass.
- **Kết quả:**
  - `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`: khai báo `MsSqlContainer` (image `mcr.microsoft.com/mssql/server:2022-latest`), ghi đè chuỗi kết nối bằng `UseSetting`, `EnsureCreatedAsync()` tạo bảng, `DisposeAsync()` xóa container. Có chốt an toàn: dừng test nếu chuỗi kết nối chứa `QLSINHVIEN`.
  - Thêm `public partial class Program { }` vào `Program.cs`.
  - Helper `TaoClientAsync(role)` đăng ký → đổi role → đăng nhập → gắn token; `SinhVienMoi()` sinh email ngẫu nhiên.
  - `SinhVienApiTests : IClassFixture<ApiFactory>` – 6 kịch bản:

  | # | Kịch bản | Kết quả mong đợi |
  |---|---|---|
  | 1 | `TaoSinhVien_RoiDocLai_DungDuLieu` | 201, đọc lại đúng dữ liệu |
  | 2 | `TaoSinhVien_EmailTrung_Tra409` | 409 + JSON chuẩn |
  | 3 | `XoaSinhVien_LaXoaMem` | 204 → 404, bản ghi vẫn còn `IsDeleted = true` |
  | 4 | `PhanQuyen_401Va403` | 401 khi không token, 403 khi Giảng viên thêm |
  | 5 | `TaoSinhVien_TuoiKhongHopLe_Tra400` | 400 + thông báo tiếng Việt |
  | 6 | `HaiNguoiCungSua_NguoiSauBi409` | Người đầu 204, người sau 409, DB giữ dữ liệu người đầu |

- **Môi trường:** Docker Desktop trên WSL2; vị trí lưu dữ liệu Docker chuyển sang `D:\DockerData` do ổ C hết dung lượng.

#### 5.2. Code Coverage ≥ 80% tầng Service
- **Kết quả:**
  - Thu thập bằng Coverlet (`coverlet.collector`): `dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults`.
  - Báo cáo HTML bằng ReportGenerator, lọc `-classfilters:"+QuanLySinhVien.Services.*"`, xuất ra `CoverageReport/`.
- **Số liệu lần đo gần nhất** (`CoverageReport/index.html`):

  | Lớp | Line coverage | Branch coverage |
  |---|---|---|
  | `QuanLySinhVien.Services.SinhVienService` | **100%** (81/81 dòng) | **96,8%** (31/32 nhánh) |

  > Báo cáo coverage này được tạo trước lần chỉnh sửa cuối của `SinhVienService` (phần RowVersion). Cần chạy đo lại (bật Docker, xóa `TestResults` và `CoverageReport` cũ) để cập nhật số liệu chính thức.

#### 5.3. Optimistic Concurrency với RowVersion
- **Yêu cầu:** hai người cùng sửa một bản ghi thì người lưu sau không được ghi đè (chống Lost Update).
- **Kết quả:**
  - Cột `RowVersion` kiểu `rowversion` (SQL Server tự tăng); Model `[Timestamp] public byte[]? RowVersion`; `SinhVienDto` mang `RowVersion` (Base64 trong JSON).
  - `UpdateAsync`: kiểm tra Id (400) → thiếu RowVersion (400) → tìm bản ghi (404) → email trùng (409) → gán `Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion` → `SaveChangesAsync()`; bắt `DbUpdateConcurrencyException` và ném `ConflictException` (409) "Dữ liệu sinh viên đã bị người khác thay đổi...".
  - Audit bỏ qua `RowVersion`.
  - Angular: `sua()` giữ `rowVersion` khi mở form; khi lưu gặp 409 đụng độ thì đóng form và tải lại danh sách, còn 409 do email trùng thì giữ form.
- **Minh chứng:** unit test `Update_ThieuRowVersion_NemBadRequest`; integration test `HaiNguoiCungSua_NguoiSauBi409`; kiểm tra thủ công bằng 2 tab trình duyệt.

---

## 4. TỔNG HỢP KIỂM THỬ

| Bộ test | Loại | Công cụ | Số ca |
|---|---|---|---|
| `SinhVienServiceTests` | Unit test tầng Service | xUnit, EF Core InMemory | 21 |
| `ExceptionMiddlewareTests` | Unit test Middleware | xUnit, Moq, `DefaultHttpContext` | 5 |
| `SinhVienApiTests` | Integration test | WebApplicationFactory, TestContainers (SQL Server 2022) | 6 |
| **Tổng** | | | **32** |

- Cách chạy: Visual Studio → Test Explorer → Run All, hoặc `dotnet test` tại thư mục `D:\TT\QuanLySinhVien` (cần bật Docker Desktop cho integration test).
- Kết quả theo lần chạy gần nhất: các test đều pass.

---

## 5. TỒN TẠI VÀ HƯỚNG CẢI THIỆN

| # | Nội dung | Mức độ | Đề xuất |
|---|---|---|---|
| 1 | Báo cáo coverage chưa cập nhật sau lần sửa cuối của Service | Cần làm trước khi nộp | Chạy lại 2 lệnh đo coverage |
| 2 | `PageSize > 50` bị đưa về 5 thay vì giới hạn ở 50 | Nhỏ | Đổi thành `Math.Clamp` về khoảng 1–50 |
| 3 | Danh sách rỗng hiển thị "Trang 1 / 0" | Nhỏ (giao diện) | Hiển thị tối thiểu 1 trang khi `totalPages = 0` |
| 4 | Audit Log chưa lưu Id của bản ghi bị tác động (`KeyValues` có thu thập nhưng chưa ghi) | Trung bình | Thêm cột `RecordId` vào `AuditLogs` |
| 5 | Người tự đăng ký nhận role Giảng viên, xem được danh sách sinh viên | Cần quyết định nghiệp vụ | Thêm role chờ duyệt, hoặc chỉ Admin được tạo tài khoản |
| 6 | Angular phân biệt hai loại 409 dựa vào nội dung câu thông báo | Nhỏ | Thêm trường mã lỗi (ví dụ `errorCode`) vào `ErrorResponse` |
| 7 | Upload ảnh chỉ kiểm tra đuôi file | Nhỏ | Kiểm tra thêm chữ ký byte đầu file (magic number) |
| 8 | Khóa bí mật JWT nằm trong `appsettings.json` | Lưu ý triển khai | Dùng User Secrets / biến môi trường khi triển khai thật |
| 9 | Chưa có API khôi phục sinh viên đã xóa mềm | Mở rộng | Thêm endpoint dùng `IgnoreQueryFilters()` để khôi phục |
| 10 | Dọn mã: `.IgnoreQueryFilters()` thừa trong `DangKy`; comment sai ở dòng `rowVersion` trong `sua()`; comment `// ===== Update =====` bị lặp trong file test; cột `Users.IsDeleted` còn trong DB nhưng không dùng | Nhỏ | Xóa / sửa |

---

## 6. KẾT LUẬN

Sau 7 tuần, hệ thống đã hoàn thành toàn bộ các yêu cầu được giao:

- **Tuần 1-2** xây dựng nền tảng bảo mật (DTO, BCrypt, JWT, phân quyền ở API) và các tính năng chính (phân trang phía server, Audit Log, upload ảnh, báo lỗi thống nhất).
- **Tuần 3-4** chuyên nghiệp hóa: xử lý lỗi tập trung bằng Middleware với một định dạng JSON duy nhất, ghi lịch sử tự động bằng Interceptor, dọn file ảnh cũ an toàn.
- **Tuần 5-6** bảo vệ dữ liệu bằng xóa mềm với Global Query Filter, tách tầng Service và bổ sung unit test.
- **Tuần 7** chứng minh hệ thống chạy đúng trong điều kiện thật: chống ghi đè dữ liệu bằng RowVersion, 6 integration test trên SQL Server thật trong Docker, và độ phủ tầng Service vượt yêu cầu 80%.

Các nguyên tắc được áp dụng xuyên suốt: **không tin dữ liệu từ client** (bảo mật đặt ở server), **xử lý tập trung một nơi** (middleware, interceptor, query filter), và **mọi tính năng đều có cách kiểm chứng** (unit test, integration test, coverage).

---

## PHỤ LỤC – HƯỚNG DẪN CHẠY

1. **Cơ sở dữ liệu:** SQL Server Express `MSI\SQLEXPRESS`, database `QLSINHVIEN` (script tạo bảng: `D:\TT\SQLQuery1.sql`, bổ sung cột `AvatarUrl`, `IsDeleted`, `RowVersion`).
2. **Backend:** mở `QuanLySinhVien.slnx` bằng Visual Studio → chạy bằng **Ctrl+F5** (API tại `https://localhost:7280`).
3. **Frontend:** tại `D:\TT\QuanLySinhVienAngular` chạy `npm install` (lần đầu) rồi `ng serve` → mở `http://localhost:4200`.
4. **Kiểm thử:** bật Docker Desktop → Test Explorer → Run All, hoặc `dotnet test`.
5. **Coverage:**
   ```bash
   dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
   reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
   ```
