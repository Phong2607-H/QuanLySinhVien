# QUẢN LÝ SINH VIÊN – Tài liệu kiến thức & cách trình bày

> **Người thực hiện:** Nguyễn Thanh Phong
> **Công nghệ:** ASP.NET Core Web API (.NET 10) · Entity Framework Core · SQL Server · Angular 22 (standalone, zoneless) · JWT · BCrypt · xUnit + Moq
>
> Tài liệu này ghi lại **từng yêu cầu** của mentor: *khái niệm – lý do – cách làm trong dự án – cách demo – câu hỏi hay gặp*. Dùng để ôn tập và trình bày khi review.
>
> **Lưu ý thuật ngữ:** trong yêu cầu của mentor, **"User" = Sinh viên** (bảng `SinhVien`) – đối tượng dữ liệu chính của hệ thống.

### Tiến độ tổng quan
| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| Tuần 1 | DTO, đặt tên, cấu hình, hash, JWT, async, lifecycle, git | ✅ |
| Tuần 2 | Phân trang, phân quyền, Audit, Upload, chuẩn hóa lỗi + Toast | ✅ |
| Tuần 3-4 | Dứt điểm Exception, Audit Interceptor, xóa ảnh cũ | ✅ (còn 2 dòng `State = Modified` nên xóa) |
| Tuần 5-6 · Tuần 1 | Soft-Delete + Global Query Filter | ✅ |
| Tuần 5-6 · Tuần 2 | Tách Service + Unit Test (xUnit, Moq) | ⏳ Đã có 5 test Middleware; chưa tách Service |

---

## Mục lục
1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Cách chạy dự án](#2-cách-chạy-dự-án)
3. [Cơ sở dữ liệu](#3-cơ-sở-dữ-liệu)
4. [Tuần 1 – Nền tảng](#4-tuần-1--nền-tảng)
5. [Tuần 2 – Tính năng chính](#5-tuần-2--tính-năng-chính)
6. [Tuần 3-4 – Exception, Audit, Quản lý file](#6-tuần-3-4--exception-audit-quản-lý-file)
7. [Tuần 5-6 – Soft-Delete & Unit Test](#7-tuần-5-6--soft-delete--unit-test)
8. [Các hàm / lớp quan trọng](#8-các-hàm--lớp-quan-trọng)
9. [Từ điển khái niệm](#9-từ-điển-khái-niệm)
10. [Kịch bản demo cho mentor](#10-kịch-bản-demo-cho-mentor)
11. [Câu hỏi vấn đáp thường gặp](#11-câu-hỏi-vấn-đáp-thường-gặp)

---

## 1. Kiến trúc tổng quan

```
[Angular – localhost:4200]
  Component (ngOnInit) → Service → jwtInterceptor (gắn token) → errorInterceptor (bắt lỗi → toast)
        │  HTTP / JSON (camelCase)
        ▼
[ASP.NET Core API – localhost:7280]
  ExceptionMiddleware → UseStatusCodePages → HTTPS → CORS → StaticFiles
  → Authentication (đọc JWT) → Authorization ([Authorize]) → Controller
        │
        ▼
  AppDbContext (HasQueryFilter: bỏ bản ghi IsDeleted)
  → SaveChangesAsync → AuditSaveChangesInterceptor (ghi AuditLogs)
        │
        ▼
[SQL Server – QLSINHVIEN]   SinhVien · Users · AuditLogs
```

### Cấu trúc thư mục chính

| Backend (`QuanLySinhVien/QuanLySinhVien`) | Vai trò |
|---|---|
| `Program.cs` | Cấu hình dịch vụ + pipeline middleware |
| `Controllers/SinhVienController.cs` | CRUD sinh viên, phân trang, upload ảnh |
| `Controllers/XacThucController.cs` | Đăng ký, đăng nhập, tạo JWT |
| `Controllers/AuditLogController.cs` | Xem lịch sử (chỉ Admin) |
| `Data/AppDbContext.cs` | DbContext + Global Query Filter |
| `Data/AuditSaveChangesInterceptor.cs` | Tự động ghi Audit Log |
| `Middleware/ExceptionMiddleware.cs` | Gom lỗi → JSON chuẩn |
| `Exceptions/AppException.cs` | Các exception nghiệp vụ tự định nghĩa |
| `DTOs/*` | Đối tượng truyền dữ liệu (không lộ Entity) |
| `Models/*` | Entity ánh xạ bảng DB |

| Frontend (`QuanLySinhVienAngular/src/app`) | Vai trò |
|---|---|
| `services/auth.ts` | Đăng nhập, lưu token, đọc Role từ JWT |
| `services/sinh-vien.ts` | Gọi API sinh viên, upload ảnh |
| `services/toast.ts` + `toast/` | Hệ thống thông báo Toast |
| `interceptors/jwt.ts` | Tự gắn `Authorization: Bearer <token>` |
| `interceptors/error.ts` | Bắt mọi lỗi HTTP, hiện toast, xử lý 401/403 |
| `utils/error-message.ts` | Hàm `layThongBaoLoi` đọc lỗi theo format chuẩn |
| `models/api-error.ts` | Interface `ApiError` |
| `guards/role.ts` | Route Guard theo quyền |
| `sinh-vien/`, `lich-su/`, `login/`, `register/` | Các màn hình |

---

## 2. Cách chạy dự án

**Backend**
```powershell
cd D:\TT\QuanLySinhVien\QuanLySinhVien
dotnet run --launch-profile https      # hoặc Visual Studio: Ctrl+F5
```
Đợi dòng `Now listening on: https://localhost:7280`.

**Frontend**
```powershell
cd D:\TT\QuanLySinhVienAngular
npm start
```
Mở `http://localhost:4200`.

**Test**
```powershell
cd D:\TT\QuanLySinhVien
dotnet test
```

> 💡 Chạy bằng **F5** (Debug) thì Visual Studio sẽ **dừng** ở mỗi dòng `throw` — đó là debugger, không phải lỗi. Dùng **Ctrl+F5** khi demo, hoặc bỏ tích *"Break when this exception type is user-unhandled"* cho `AppException`.

---

## 3. Cơ sở dữ liệu

| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `SinhVien` | Id, HoTen, Email, Tuoi, AvatarUrl, **IsDeleted** | Dữ liệu chính |
| `Users` | Id, Username (UNIQUE), **PasswordHash**, FullName, CreatedAt, **Role**, **IsDeleted** | Tài khoản; Role = `Admin` / `GiangVien` |
| `AuditLogs` | Id, Username, Action, TableName, OldValues, NewValues, Timestamp | Nhật ký thao tác |

- Không có khóa ngoại giữa các bảng → **không có N+1**, không cần `.Include()`.
- Cấp quyền Admin: `UPDATE Users SET Role = 'Admin' WHERE Username = N'<tên>';`

---

## 4. Tuần 1 – Nền tảng

### 4.1 DTO (Data Transfer Object)
- **Khái niệm:** Class riêng chỉ chứa dữ liệu API cần gửi/nhận, tách biệt với Entity (bản sao bảng DB).
- **Lý do:** (1) không lộ cột nhạy cảm (`PasswordHash`, `IsDeleted`); (2) API không phụ thuộc cấu trúc DB; (3) chống *over-posting* (client gửi thêm trường không được phép).
- **Trong dự án:**
  - Nhận vào: `SinhVienDto`, `DangKyDto`, `DangNhapDto`.
  - Trả ra: `SinhVienDto`, `PagedResult<T>`, `AuditLogDto`, `ErrorResponse`.
  - Mapping bằng `.Select(s => new SinhVienDto { ... })` → EF chỉ SELECT đúng cột cần.
  - `AuditLogController` dùng `.AsNoTracking()` vì chỉ đọc.
- **Demo:** F12 → Network → Response không có `passwordHash`, `isDeleted`.

### 4.2 Quy tắc đặt tên (Naming Convention)
| Thành phần | C# | TypeScript |
|---|---|---|
| Class, Interface | `PascalCase` | `PascalCase` |
| Method / hàm | `PascalCase` | `camelCase` |
| Property | `PascalCase` | `camelCase` |
| Biến cục bộ, tham số | `camelCase` | `camelCase` |
| Field private | `_camelCase` | `camelCase` |
| Tên file | `PascalCase.cs` | `kebab-case.ts` |

- ASP.NET Core **tự đổi** `HoTen` → `hoTen` khi trả JSON, nên Angular dùng `hoTen`.

### 4.3 Không hardcode cấu hình
- **Khái niệm:** Tách cấu hình (chuỗi kết nối, secret) khỏi code.
- **Lý do:** Đổi máy/môi trường không phải sửa code; không lộ bí mật trong mã nguồn.
- **Trong dự án:** `appsettings.json` → đọc qua `builder.Configuration.GetSection("JwtSettings")` và `GetConnectionString("DefaultConnection")`.
- **Nâng cao:** `dotnet user-secrets` cho Secret; Angular nên dùng `environment.ts` cho URL API.

### 4.4 Lưu mật khẩu an toàn (Hash)
- **Khái niệm:** *Băm* (hash) là biến đổi **một chiều**, khác *mã hóa* (hai chiều).
- **Lý do:** DB bị lộ vẫn không lấy được mật khẩu gốc.
- **Vì sao BCrypt:** tự thêm **salt** ngẫu nhiên (cùng mật khẩu → hash khác nhau) và **chạy chậm có chủ đích** (work factor 11) → chống dò mật khẩu.
- **Trong dự án:** `BCrypt.HashPassword()` khi đăng ký, `BCrypt.Verify()` khi đăng nhập. Sai username hay sai mật khẩu đều báo **cùng một câu** để không lộ username tồn tại.
- **Demo:** `SELECT Username, PasswordHash FROM Users;` → chuỗi `$2a$11$...` dài 60 ký tự.

### 4.5 JWT & gắn token
- **Khái niệm:** JSON Web Token = "thẻ ra vào" có chữ ký, chứa *claims* (Id, Username, Role, FullName, hạn dùng).
- **Lý do:** HTTP không lưu trạng thái; mỗi request phải tự chứng minh danh tính.
- **Luồng:** Đăng nhập → backend ký token (HmacSha256 + Secret) → Angular lưu `localStorage` → `jwtInterceptor` gắn header `Authorization: Bearer <token>` → backend `UseAuthentication` kiểm tra chữ ký/Issuer/Audience/hạn → gán `HttpContext.User`.
- **Demo:** F12 → Network → Request Headers có `Authorization`; dán token vào jwt.io.

### 4.6 Lập trình bất đồng bộ (async/await)
- **Khái niệm:** `await` chờ tác vụ I/O mà **không chiếm thread**.
- **Lý do:** Trong lúc chờ SQL, thread được trả về để phục vụ request khác → server chịu tải cao hơn.
- **Trong dự án:** `ToListAsync`, `CountAsync`, `FindAsync`, `AnyAsync`, `SaveChangesAsync`, `CopyToAsync`. `SinhVienExists` dùng `AnyAsync`.
- **Lưu ý:** không dùng `.Result`, `.Wait()`, `.ToList()` với DbContext (chặn thread, dễ deadlock).

### 4.7 Include & lỗi N+1
- **N+1:** 1 truy vấn lấy N dòng + N truy vấn lấy dữ liệu liên quan.
- **`.Include()`:** gộp thành 1 câu JOIN (Eager Loading).
- **Dự án:** Role là cột chuỗi trong `Users`, không có bảng liên kết → **không cần Include, không có N+1**.
- **Cách kiểm chứng:** xem console `dotnet run` – EF in ra từng câu SQL.

### 4.8 Vòng đời component Angular
- **`constructor`:** chỉ dùng để nhận service (Dependency Injection).
- **`ngOnInit`:** component đã sẵn sàng → nơi gọi API tải dữ liệu.
- **`typeof window !== 'undefined'`:** chỉ gọi API khi chạy ở trình duyệt (dự án có SSR).
- **Zoneless:** dự án không dùng `zone.js` → Angular **không tự vẽ lại** giao diện khi dữ liệu thay đổi trong callback của API. Sau khi gán dữ liệu phải gọi `this.cdr.detectChanges()`.
  - Ví dụ thực tế: trang Đăng nhập trước đây phải bấm **2 lần** mới hiện "Sai mật khẩu" → đã thêm `ChangeDetectorRef` + `detectChanges()` trong `login.ts`, `register.ts`.

### 4.9 Xử lý khi server sập
- `HttpErrorResponse.status === 0` = không kết nối được server.
- 2 lớp xử lý: `errorInterceptor` hiện toast + component hiện banner có nút **Thử lại** → không bao giờ trắng trang.

### 4.10 Cấu hình `Program.cs` (vấn đáp)
- **CORS:** trình duyệt chặn gọi khác origin (4200 ≠ 7280); policy `AllowAngular` cho phép origin 4200, mọi header (để gửi `Authorization`), mọi method.
- **Thứ tự pipeline:**
  1. `ExceptionMiddleware` – bọc try/catch mọi thứ phía sau
  2. `UseStatusCodePages` – bổ sung JSON cho 401/403/404 rỗng
  3. `UseHttpsRedirection`
  4. `UseCors`
  5. `UseStaticFiles` – phục vụ `/avatars/*.jpg`
  6. `UseAuthentication` – biết **ai**
  7. `UseAuthorization` – xét **được làm gì**
  8. `MapControllers`

### 4.11 Git
- Commit nhỏ theo tính năng, thông điệp rõ ràng (`feat:`, `fix:`, `refactor:`).
- Lý do: dễ quay lại, dễ tìm lỗi, dễ review.

---

## 5. Tuần 2 – Tính năng chính

### 5.1 Phân trang, lọc, sắp xếp phía server
- **Khái niệm:** `IQueryable` chỉ **xây** câu truy vấn; chỉ khi gọi `ToListAsync()` mới dịch thành **1 câu SQL** (*deferred execution*).
- **Lý do:** SQL Server chỉ trả đúng số dòng cần hiển thị, không kéo cả bảng lên RAM.
- **Thứ tự bắt buộc:** `Where` (lọc) → `OrderBy` (sắp xếp) → `CountAsync` (tổng) → `Skip/Take` (phân trang) → `Select` (DTO) → `ToListAsync`.
  - Lọc trước khi đếm: tổng phải là số dòng *sau lọc*.
  - Sắp xếp trước `Skip/Take`: SQL bắt buộc `ORDER BY` khi dùng `OFFSET`.
- **Kiểm tra tham số:** `PageNumber < 1 → 1`; `PageSize < 1 hoặc > 50 → 5` (tránh OFFSET âm → lỗi 500).
- **Sắp xếp:** `switch` theo `(sortBy, isDescending)` hỗ trợ HoTen, Email, Tuoi, Id (tăng/giảm).
- **Angular:** tìm kiếm → quay về trang 1; bấm lại cùng cột → đảo chiều; xóa dòng cuối của trang → lùi về trang trước.
- **SQL sinh ra:** `... WHERE [IsDeleted] = 0 AND ... ORDER BY ... OFFSET @p0 ROWS FETCH NEXT @p1 ROWS ONLY`

### 5.2 Giao diện theo quyền & Route Guard
| Lớp | Công cụ | Mục đích | Bảo mật thật? |
|---|---|---|---|
| Nút | `*ngIf="authService.hasRole('Admin')"` | Ẩn nút không có quyền | ❌ |
| Trang | `roleGuard(['Admin'])` (`CanActivateFn`) | Chặn gõ URL | ❌ |
| API | `[Authorize(Roles = "Admin")]` | Chặn cả Postman | ✅ |

- Role đọc **từ JWT** (có chữ ký), không tin `localStorage.role`.
- Guard trả `UrlTree` (`router.parseUrl`) để điều hướng gọn; chưa đăng nhập → `/login`.
- Create / Update / Delete sinh viên chỉ Admin; Upload ảnh cho Admin, GiangVien.

### 5.3 Audit Logging
- **Khái niệm:** Tự động ghi *ai – làm gì – lúc nào – giá trị cũ/mới*.
- **Vì sao `SaveChangesInterceptor`:** tự động cho **mọi** lần lưu, không phải gọi thủ công ở từng controller; chạy ở tầng dữ liệu nên biết chính xác cột nào thay đổi.
- **Luồng:** `SaveChangesAsync()` → `SavingChangesAsync` (trước khi ghi DB) → duyệt `ChangeTracker.Entries()` → xác định Thêm/Sửa/Xóa theo `EntityState` (có `IsDeleted = true` → "Xóa") → lấy `OriginalValue`/`CurrentValue` → JSON → thêm `AuditLog` vào **cùng transaction**.
- **Bảo mật:** bỏ qua cột `PasswordHash` (`SensitiveProperties`).
- **Người thực hiện:** lấy từ `IHttpContextAccessor.HttpContext.User.Identity.Name`.
- **Màn hình:** `/lich-su`, chỉ Admin (Route Guard + `[Authorize(Roles = "Admin")]`), dữ liệu trả qua `AuditLogDto`.

### 5.4 Upload file & thanh tiến trình
- **`multipart/form-data`:** gửi file nhị phân trực tiếp (JSON phải base64, nặng hơn ~33%).
- **Backend:** nhận `IFormFile` → kiểm tra rỗng, đuôi (`.jpg/.jpeg/.png`), dung lượng ≤ 2MB → tên file `Guid.NewGuid()` (tránh trùng, chống path traversal) → lưu `wwwroot/avatars` → DB lưu đường dẫn tương đối `/avatars/xxx.jpg` → **xóa ảnh cũ sau khi lưu DB thành công**.
- **Frontend:** `FormData.append('file', file)` (không tự đặt `Content-Type`), `reportProgress: true` + `observe: 'events'`, dùng `HttpEventType.UploadProgress` / `HttpEventType.Response`; reset `input.value` để chọn lại cùng file; thuộc tính `accept` lọc ảnh.
- **Demo:** F12 → Network → **Slow 3G** để thấy % chạy.

### 5.5 Chuẩn hóa xử lý lỗi & Toast
- **Format lỗi chuẩn:** `{ "statusCode": 400, "message": "...", "details": null, "errors": { ... } }`
  - `details`: stack trace, **chỉ ở Development**.
  - `errors`: chỉ có khi lỗi validation.
- **Nguồn lỗi & nơi xử lý:**

| Loại lỗi | Ai xử lý |
|---|---|
| Exception nghiệp vụ (`NotFoundException`, `ConflictException`...) | `ExceptionMiddleware` |
| Lỗi DB (`DbUpdateException`) | `ExceptionMiddleware` → 409 |
| Lỗi hệ thống bất ngờ | `ExceptionMiddleware` → 500 |
| Validation (`[Required]`, `[Range]`...) | `InvalidModelStateResponseFactory` |
| 401/403 từ JWT, 404 route | `UseStatusCodePages` |

- **Angular:**
  - `layThongBaoLoi(err)`: `status 0` → "Không thể kết nối…"; có `message` → dùng `message`.
  - `errorInterceptor`: hiện toast (trừ API đăng nhập/đăng ký – trang đó tự hiện banner); **401** → logout + về `/login`; **403** → về `/sinh-vien`.
  - Toàn bộ `alert()` đã thay bằng `toastService.showSuccess / showWarning / showError`.

---

## 6. Tuần 3-4 – Exception, Audit, Quản lý file

### 6.1 Dứt điểm Exception Handling
- **Yêu cầu:** Mọi loại lỗi trả về **cùng một format JSON** `{ statusCode, message, details }`, Angular đọc đúng format để báo lỗi.
- **Lý do:** Trước đó lỗi trả về nhiều kiểu (chuỗi thuần, `{message}`, ProblemDetails, body rỗng) → Angular phải "đoán", dễ hiện `[object Object]`; mỗi controller tự `try/catch` gây lặp code; stack trace có thể lộ ra Production.

**Custom Exception (`Exceptions/AppException.cs`)**
```csharp
public abstract class AppException : Exception { public int StatusCode { get; } ... }
public class BadRequestException : AppException  // 400
public class ForbiddenException  : AppException  // 403
public class NotFoundException   : AppException  // 404
public class ConflictException   : AppException  // 409
```
- **Lý do:** mỗi exception mang sẵn mã HTTP → controller chỉ cần `throw`, không nhớ mã lỗi.

**ExceptionMiddleware – các điểm quan trọng**
| Đoạn code | Ý nghĩa / Lý do |
|---|---|
| `try { await _next(context); } catch (Exception ex)` | Bọc toàn bộ pipeline phía sau |
| `if (context.Response.HasStarted) { ...; throw; }` | Response đã gửi một phần thì **không thể đổi status code** → ghi log và ném lại, tránh sinh lỗi thứ hai |
| `if (ex is AppException) LogWarning else LogError` | Lỗi người dùng (4xx) chỉ là cảnh báo; lỗi hệ thống (5xx) mới là Error → log sự cố thật nổi bật |
| `switch (exception)` → `(statusCode, message)` | Ánh xạ loại lỗi → mã HTTP; `DbUpdateConcurrencyException` phải đứng **trước** `DbUpdateException` (lớp con) |
| `Details = _env.IsDevelopment() ? ... : null` | Chỉ lộ stack trace ở môi trường Dev |

**Các nguồn lỗi và nơi xử lý**
| Loại lỗi | Nơi xử lý |
|---|---|
| Exception nghiệp vụ / DB / hệ thống | `ExceptionMiddleware` |
| Validation (`[Required]`, `[Range]`) – xảy ra *trước* controller | `InvalidModelStateResponseFactory` (`Program.cs`) |
| 401/403 từ JWT, 404 route – không phải exception | `UseStatusCodePages` (`Program.cs`, đặt trước Authentication) |

**Unit test (`ExceptionMiddlewareTests.cs`) – 5 test:** lỗi 500 + format JSON, 401, 400, Production ẩn `details`, `NotFoundException` → 404.

> **Debugger:** chạy F5 thì Visual Studio **dừng** ở mỗi dòng `throw` – đó là debugger, không phải lỗi. Middleware vẫn bắt và trả JSON khi nhấn Continue / chạy Ctrl+F5.

### 6.2 Dứt điểm Audit Logging bằng `SaveChangesInterceptor`
- **Yêu cầu:** Tự động ghi log thay vì gọi thủ công ở từng Controller.
- **Lý do:** Ghi thủ công dễ quên, lặp code, khó lấy giá trị cũ. Interceptor gắn vào EF Core, chạy **mỗi lần** `SaveChangesAsync()`, trước khi ghi DB, khi `ChangeTracker` còn giữ giá trị cũ.
- **Các bước đã làm:**
  1. Bảng `AuditLogs` + model + `DbSet`.
  2. `AddHttpContextAccessor()` → interceptor biết **ai** thao tác.
  3. Override `SavingChangesAsync` (*Saving* chứ không phải *Saved*: sau khi lưu thì mất `OriginalValue`).
  4. Duyệt `ChangeTracker.Entries()`, bỏ qua `AuditLog` (tránh ghi log cho chính log), `Detached`, `Unchanged`.
  5. Xác định hành động theo `EntityState`; `IsDeleted = true` → "Xóa".
  6. Lấy `OriginalValue` / `CurrentValue` → JSON.
  7. Bỏ qua cột nhạy cảm (`SensitiveProperties = { "PasswordHash" }`).
  8. Log nằm **cùng transaction** với dữ liệu.
  9. Đăng ký: `AddScoped<AuditSaveChangesInterceptor>()` + `options.AddInterceptors(...)`.
  10. Không controller nào tự ghi `AuditLogs.Add(...)`.
- **Cải tiến nên làm:** xóa `_context.Entry(sinhVien).State = EntityState.Modified;` còn ở `Update` và `Delete`.
  - **Lý do:** entity lấy bằng `FindAsync` đã được EF theo dõi, tự biết cột nào đổi. Dòng trên ép **mọi cột** thành "đã sửa" → log ghi cả cột không đổi.

### 6.3 Quản lý file nâng cao – xóa ảnh cũ
- **Yêu cầu:** Khi cập nhật ảnh mới, xóa file ảnh cũ trên server để tránh rò rỉ dung lượng.
- **Lý do:** Không xóa → **file mồ côi** (orphan file) tích tụ, ổ cứng đầy dần.
- **Luồng trong `UploadAvatar`:**
  1. Kiểm tra file (rỗng, đuôi, ≤ 2MB).
  2. Lưu file mới vào `_env.WebRootPath/avatars` với tên `Guid.NewGuid()`.
  3. Ghi nhớ `oldAvatarUrl`.
  4. Cập nhật `AvatarUrl` → `SaveChangesAsync()` trong `try`:
     - **Lỗi** → `catch` xóa **file mới** vừa ghi rồi `throw;` (không để rác, middleware vẫn trả JSON lỗi).
     - **Thành công** → xóa **file cũ** (kiểm tra `File.Exists` trước).
- **Vì sao xóa file cũ *sau* khi lưu DB:** nếu lưu DB lỗi mà đã xóa ảnh cũ → ảnh hỏng. Xóa sau thì tệ nhất chỉ còn 1 file thừa.
- **Vì sao dùng `IWebHostEnvironment.WebRootPath`:** `Directory.GetCurrentDirectory()` phụ thuộc thư mục đang đứng khi chạy lệnh (sai khi chạy từ nơi khác hoặc trong Docker); `WebRootPath` luôn trỏ đúng `wwwroot`.
- **Kiểm tra:** đổi ảnh 1 sinh viên 3 lần → thư mục `wwwroot/avatars` không tăng số file; đối chiếu `SELECT Id, AvatarUrl FROM SinhVien WHERE AvatarUrl IS NOT NULL;`.

---

## 7. Tuần 5-6 – Soft-Delete & Unit Test

### 7.1 Tuần 1 – Soft-Delete (Xóa mềm)
- **Khái niệm:** Không `DELETE` mà đặt `IsDeleted = 1`; dòng vẫn còn trong DB, chỉ bị ẩn khỏi ứng dụng.
- **Lý do:** khôi phục được khi xóa nhầm; giữ lịch sử, đối chiếu với Audit Log; hệ thống thật thường bắt buộc lưu dữ liệu.
- **Global Query Filter** (`AppDbContext.OnModelCreating`):
  ```csharp
  modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted);
  modelBuilder.Entity<NguoiDung>().HasQueryFilter(u => !u.IsDeleted); // áp dụng thêm cho tài khoản
  ```
  - **Lý do:** khai báo 1 lần, EF **tự thêm** `WHERE IsDeleted = 0` vào **mọi** truy vấn (`ToListAsync`, `FindAsync`, `AnyAsync`, `CountAsync`) → không phải viết `.Where(!IsDeleted)` ở khắp nơi, không sợ quên.
- **Các bước:**
  1. SQL: `ALTER TABLE SinhVien ADD IsDeleted BIT NOT NULL DEFAULT 0;` (`DEFAULT 0` để dữ liệu cũ tự là "chưa xóa").
  2. Model: `public bool IsDeleted { get; set; } = false;`
  3. `HasQueryFilter` trong `OnModelCreating`.
  4. `Delete`: `sinhVien.IsDeleted = true;` thay cho `Remove(sinhVien)`.
  5. Audit Interceptor tự ghi hành động "Xóa".
- **Xem lại bản ghi đã xóa:** `IgnoreQueryFilters()` (dùng cho Thùng rác / khôi phục).

**Kịch bản checkpoint**
1. SSMS: `SELECT Id, HoTen, Email, IsDeleted FROM SinhVien;` → chọn 1 sinh viên (`IsDeleted = 0`).
2. Web (Admin): bấm **Xóa** → toast "Xóa thành công!", sinh viên **biến mất** khỏi lưới.
3. SSMS: `SELECT Id, HoTen, IsDeleted FROM SinhVien WHERE Id = X;` → dòng **vẫn còn**, `IsDeleted = 1` (SSMS hiển thị `bit` True = **1**).
4. Chứng minh thêm: trang Lịch sử có dòng "Xóa"; chỉ vào dòng `HasQueryFilter`.
5. Khôi phục sau demo: `UPDATE SinhVien SET IsDeleted = 0 WHERE Id = X;`

### 7.2 Tuần 2 – Unit Test (xUnit + Moq) cho tầng Service
- **Khái niệm:**
  - **Unit test:** code tự động kiểm tra từng phần nhỏ, chạy lại trong vài giây sau mỗi lần sửa.
  - **Mẫu AAA:** Arrange (chuẩn bị) – Act (gọi hàm) – Assert (kiểm tra).
  - **Moq:** tạo đối tượng **giả** (`Mock<T>`) thay phụ thuộc thật; `Setup/ReturnsAsync` quy định giá trị trả về, `Verify` kiểm tra hàm có được gọi.
  - **EF Core InMemory:** database thật chạy trong RAM, vẫn áp dụng Global Query Filter.
- **Vì sao phải tách tầng Service:** controller gọi thẳng `AppDbContext` → muốn test phải có SQL Server thật. Tách `ISinhVienService` / `SinhVienService` → test Service bằng InMemory, test Controller bằng Moq (**Separation of Concerns**: Controller nhận request/trả mã HTTP; Service chứa nghiệp vụ; DbContext truy cập dữ liệu).
- **Các bước:**
  1. `Services/ISinhVienService.cs` – khai báo `GetPagedAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
  2. `Services/SinhVienService.cs` – chuyển nguyên logic từ controller sang, ném `NotFoundException` / `ConflictException` / `BadRequestException`.
  3. `Program.cs`: `builder.Services.AddScoped<ISinhVienService, SinhVienService>();`
  4. Controller chỉ gọi service (`Ok(await _service.GetByIdAsync(id))`), không `try/catch`.
  5. `dotnet add QuanLySinhVien.Tests package Microsoft.EntityFrameworkCore.InMemory --version 10.0.11`
  6. `SinhVienServiceTests` (InMemory):
     - `DeleteAsync_XoaMem_BanGhiVanConTrongDb`
     - `GetByIdAsync_IdKhongTonTai_NemNotFoundException`
     - `CreateAsync_EmailTrung_NemConflictException`
     - `GetPagedAsync_Trang2_Tra5DongVaTongDung`
     - `GetPagedAsync_KhongTraVeSinhVienDaXoa`
  7. `SinhVienControllerTests` (Moq):
     - `GetById_GoiServiceVaTraVe200`
     - `Delete_GoiServiceDungMotLanVaTraVe204` (`mock.Verify(s => s.DeleteAsync(5), Times.Once)`)
  8. `ExceptionMiddlewareTests` – giả lập lỗi, kiểm tra JSON `{ statusCode, message, details }` ✅ (đã có 5 test).
- **Đặt tên test:** `TenHam_TinhHuong_KetQuaMongDoi` → test hỏng là biết ngay chỗ lỗi.
- **Chạy:** `dotnet test` hoặc Visual Studio → **Test Explorer**.
- **Demo giá trị của test:** tạm đổi `IsDeleted = true` thành `Remove(sv)` → test xóa mềm chuyển **đỏ** → đổi lại.
- **Giới hạn InMemory:** không kiểm tra UNIQUE, độ dài cột như SQL Server → vẫn cần validate trong DTO/Service.

---

## 8. Các hàm / lớp quan trọng

### Backend
| Hàm / Lớp | Vị trí | Chức năng |
|---|---|---|
| `GetAll(SinhVienQuery)` | `SinhVienController` | Lọc, sắp xếp, phân trang bằng IQueryable |
| `Create / Update / Delete` | `SinhVienController` | CRUD, chỉ Admin; Delete = xóa mềm |
| `UploadAvatar(id, IFormFile)` | `SinhVienController` | Upload ảnh, xóa ảnh cũ |
| `SinhVienExists(id)` | `SinhVienController` | Kiểm tra tồn tại bằng `AnyAsync` |
| `DangKy / DangNhap` | `XacThucController` | BCrypt Hash/Verify |
| `GenerateJwtToken` | `XacThucController` | Tạo JWT với claims |
| `InvokeAsync / HandleExceptionAsync` | `ExceptionMiddleware` | Bắt exception → JSON |
| `SavingChangesAsync` | `AuditSaveChangesInterceptor` | Ghi Audit tự động |
| `OnModelCreating` | `AppDbContext` | Cấu hình Global Query Filter |
| `InvalidModelStateResponseFactory` | `Program.cs` | Lỗi validation → `ErrorResponse` |
| `UseStatusCodePages` | `Program.cs` | 401/403/404 rỗng → `ErrorResponse` |

### Frontend
| Hàm / Thành phần | Vị trí | Chức năng |
|---|---|---|
| `login / logout / getRole / hasRole` | `services/auth.ts` | Quản lý phiên, đọc Role từ JWT |
| `getAll / create / update / delete / uploadAvatar` | `services/sinh-vien.ts` | Gọi API |
| `jwtInterceptor` | `interceptors/jwt.ts` | Gắn token |
| `errorInterceptor` | `interceptors/error.ts` | Bắt lỗi, toast, 401/403 |
| `layThongBaoLoi` | `utils/error-message.ts` | Lấy câu thông báo từ lỗi |
| `roleGuard / authGuard` | `guards/role.ts`, `app.routes.ts` | Chặn route |
| `taiDanhSach / chuyenTrang / timKiem / thayDoiSapXep` | `sinh-vien.ts` | Phân trang, tìm, sắp xếp |
| `luu / sua / xoa / taiLenAvatarTuDong` | `sinh-vien.ts` | Thêm, sửa, xóa, upload |
| `showSuccess / showWarning / showError` | `services/toast.ts` | Thông báo toast |

---

## 9. Từ điển khái niệm

| Thuật ngữ | Giải thích ngắn |
|---|---|
| **Entity** | Class ánh xạ trực tiếp bảng DB |
| **DTO** | Class chỉ để truyền dữ liệu qua API |
| **Middleware** | Một "trạm" trong pipeline xử lý request của ASP.NET |
| **HttpContext** | Toàn bộ thông tin 1 request/response (User, Request, Response) |
| **IHttpContextAccessor** | Cách lấy HttpContext ở class không phải controller/middleware |
| **HttpInterceptor** (Angular) | "Middleware" phía trình duyệt, chặn request/response của HttpClient |
| **SaveChangesInterceptor** | Chặn thao tác lưu DB của EF Core (không liên quan HTTP) |
| **IQueryable** | Truy vấn chưa chạy, được dịch sang SQL |
| **Deferred execution** | Chỉ thực thi khi gọi `ToListAsync`, `CountAsync`... |
| **Global Query Filter** | Điều kiện EF tự thêm vào mọi truy vấn |
| **Soft delete** | Đánh dấu đã xóa thay vì xóa thật |
| **JWT / Claim** | Token có chữ ký / một mẩu thông tin trong token |
| **Hash / Salt** | Băm một chiều / chuỗi ngẫu nhiên trộn trước khi băm |
| **CORS** | Cơ chế server cho phép trình duyệt gọi từ origin khác |
| **RBAC** | Phân quyền theo vai trò |
| **CanActivateFn / UrlTree** | Hàm guard / đích điều hướng của Router |
| **multipart/form-data** | Định dạng gửi file qua HTTP |
| **HttpEventType** | Loại sự kiện HTTP (UploadProgress = 1, Response = 4) |
| **Zoneless** | Angular không dùng zone.js → phải tự gọi `detectChanges()` |
| **Toast** | Thông báo nhỏ tự biến mất, không chặn giao diện |
| **AAA / Mock** | Mẫu viết test / đối tượng giả trong test |

---

## 10. Kịch bản demo cho mentor

1. **DTO + Token:** F12 → Network → mở request `SinhVien` → Headers có `Authorization`, Response không có `passwordHash`.
2. **Hash:** SSMS → `SELECT Username, PasswordHash FROM Users;`
3. **Phân trang:** chuyển trang, tìm kiếm, bấm cột Email 2 lần → xem URL request và console SQL `OFFSET … FETCH`.
4. **Phân quyền:** đăng nhập GiangVien → không thấy nút; gõ `/lich-su` → bị chặn; Postman `DELETE` bằng token GiangVien → **403**.
5. **Audit:** Admin sửa tuổi → mở Lịch sử thấy dòng "Sửa" với giá trị cũ/mới.
6. **Upload:** Slow 3G → thanh % chạy; thư mục `wwwroot/avatars` chỉ còn ảnh mới.
7. **Lỗi:** nhập tuổi 10 (400), trùng email (409), sai mật khẩu (banner), gọi `/api/abc` (404 JSON), tắt backend (toast + nút Thử lại), sửa token trong Local Storage (401 → về Login).
8. **Xóa mềm:** xóa sinh viên trên web → SSMS vẫn còn dòng với `IsDeleted = 1`.
9. **Test:** `dotnet test` → toàn bộ test qua.

---

## 11. Câu hỏi vấn đáp thường gặp

**Hỏi:** Vì sao không trả thẳng Entity?
**Đáp:** Tránh lộ dữ liệu nhạy cảm, tách API khỏi cấu trúc DB, chống over-posting.

**Hỏi:** Ẩn nút bằng `*ngIf` đã đủ bảo mật chưa?
**Đáp:** Chưa. Chỉ `[Authorize(Roles)]` ở backend mới là bảo mật thật; Postman vẫn gọi được API nếu backend không chặn.

**Hỏi:** Sửa payload JWT để thành Admin được không?
**Đáp:** Không. Chữ ký sẽ không khớp Secret → backend trả 401.

**Hỏi:** Vì sao `ExceptionMiddleware` đặt đầu tiên?
**Đáp:** Nó bọc try/catch mọi thứ phía sau; đặt sau thì lỗi của các trạm trước nó không bị bắt.

**Hỏi:** Lỗi `[Range]` có đi qua middleware không?
**Đáp:** Không. `[ApiController]` trả 400 trước khi vào controller, không phải exception → phải cấu hình `InvalidModelStateResponseFactory`.

**Hỏi:** Lưu dữ liệu lỗi thì log có bị ghi không?
**Đáp:** Không. Log nằm chung transaction với dữ liệu, cùng thành công hoặc cùng thất bại.

**Hỏi:** Vì sao xóa ảnh cũ **sau** khi lưu DB?
**Đáp:** Nếu lưu DB lỗi thì ảnh cũ vẫn dùng được; xóa trước sẽ làm ảnh bị hỏng.

**Hỏi:** `IQueryable` khác `IEnumerable`?
**Đáp:** `IQueryable` dịch sang SQL và chạy ở SQL Server; `IEnumerable` chạy bằng C# trên RAM.

**Hỏi:** Vì sao Visual Studio dừng ở dòng `throw` khi đăng nhập sai?
**Đáp:** Do debugger (F5) dừng ở mọi exception. Khi chạy thật (Ctrl+F5), middleware bắt lỗi và trả 400 bình thường.

**Hỏi:** User bị xóa mềm nhưng token còn hạn thì sao?
**Đáp:** Vẫn gọi API được đến khi token hết hạn (JWT stateless). Muốn chặn ngay: kiểm tra user trong `JwtBearerEvents.OnTokenValidated` hoặc dùng token ngắn hạn + Refresh Token.
