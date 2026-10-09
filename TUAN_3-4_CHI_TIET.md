# TUẦN 3-4 – GIẢI THÍCH TƯỜNG TẬN: YÊU CẦU · CÁCH LÀM · TRÌNH BÀY

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên

Tuần 3-4 có **3 yêu cầu**, cùng một tinh thần: **làm cho hệ thống "chuyên nghiệp" hơn** — lỗi được báo gọn gàng, mọi thay đổi đều có dấu vết, và file trên server không bị rác.

| # | Yêu cầu | Một câu tóm tắt |
|---|---|---|
| 1 | **Dứt điểm Exception Handling** | Mọi lỗi của API đều trả về **một định dạng JSON chuẩn** `{statusCode, message, details}`, và Angular đọc được định dạng đó |
| 2 | **Dứt điểm Audit Logging** bằng `SaveChangesInterceptor` | Ghi nhật ký Thêm/Sửa/Xóa **tự động** ở tầng EF Core, Controller không phải viết dòng log nào |
| 3 | **Quản lý file nâng cao** | Khi đổi ảnh đại diện, **xóa file ảnh cũ** trên ổ cứng để không để lại rác |

Mỗi phần dưới đây trình bày theo thứ tự: **① Vấn đề trước khi làm → ② Ý tưởng giải quyết → ③ Các bước làm (code thật) → ④ Luồng chạy → ⑤ Kiểm tra trên web → ⑥ Từ khóa → ⑦ Câu hỏi vấn đáp**.

---

# YÊU CẦU 1 – DỨT ĐIỂM EXCEPTION HANDLING

## ① Vấn đề trước khi làm

Ở Tuần 1-2, mỗi hàm trong Controller **tự xử lý lỗi theo kiểu riêng**:

```csharp
// Kiểu cũ – mỗi chỗ một kiểu
if (sinhVien == null) return NotFound();                          // trả về body rỗng
if (emailTonTai) return BadRequest("Email đã tồn tại");           // trả về chuỗi chữ
if (id != dto.Id) return BadRequest(new { error = "Sai Id" });     // trả về object khác tên trường
// Lỗi bất ngờ (mất kết nối DB...) → ASP.NET trả trang lỗi HTML / stack trace dài
```

**Hậu quả:**

| Vấn đề | Ảnh hưởng |
|---|---|
| Mỗi API trả lỗi một kiểu (rỗng, chuỗi, object khác nhau) | Angular phải viết nhiều `if` để đoán cách đọc lỗi |
| Lỗi bất ngờ lộ stack trace ra ngoài | **Lộ thông tin nội bộ** (đường dẫn, tên class) – rủi ro bảo mật |
| Code `try/catch` lặp lại ở nhiều Controller | Khó bảo trì, dễ quên |
| Không phân biệt lỗi người dùng (4xx) và lỗi hệ thống (5xx) khi ghi log | Khó tìm sự cố thật |

## ② Ý tưởng giải quyết

> **"Ném lỗi ở đâu cũng được – bắt lỗi ở MỘT chỗ duy nhất."**

Ví von: **tấm lưới an toàn dưới gánh xiếc**. Diễn viên (Controller, Service) cứ biểu diễn; lỡ ngã (`throw`) thì lưới (Middleware) hứng lại, không ai bị thương (người dùng nhận thông báo lịch sự).

Giải pháp gồm 5 mảnh ghép:

| Mảnh | Vai trò |
|---|---|
| **Exception tự định nghĩa** (`NotFoundException`, `ConflictException`...) | Mỗi loại lỗi gắn sẵn một mã HTTP |
| **`ErrorResponse`** | "Khuôn" JSON chung cho mọi lỗi |
| **`ExceptionMiddleware`** | Bắt mọi exception, đổi thành JSON chuẩn |
| **`InvalidModelStateResponseFactory` + `UseStatusCodePages`** | Bắt nốt các lỗi **không phải exception** (validation, 401/403/404) |
| **Angular `errorInterceptor` + `layThongBaoLoi`** | Đọc `message` và hiện Toast ở một nơi |

## ③ Các bước làm

### Bước 1 – Tạo các exception tự định nghĩa

**File:** `Exceptions/AppException.cs`

```csharp
public abstract class AppException : Exception
{
    public int StatusCode { get; }
    protected AppException(string message, int statusCode) : base(message) => StatusCode = statusCode;
}
public class BadRequestException : AppException { public BadRequestException(string m) : base(m, 400) { } }
public class ForbiddenException  : AppException { public ForbiddenException(string m)  : base(m, 403) { } }
public class NotFoundException   : AppException { public NotFoundException(string m)   : base(m, 404) { } }
public class ConflictException   : AppException { public ConflictException(string m)   : base(m, 409) { } }
```

| Dòng | Vì sao |
|---|---|
| `abstract` | Không cho dùng trực tiếp `AppException`, bắt buộc chọn loại cụ thể |
| `: Exception` | Kế thừa exception chuẩn nên `throw`/`catch` được |
| `StatusCode { get; }` | Chỉ đọc, gán một lần khi tạo |
| Mỗi lớp con gắn sẵn mã | Ném `NotFoundException` là tự hiểu 404, **Service không cần biết gì về HTTP** |

**Cách dùng ở Service/Controller:**
```csharp
throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
throw new ConflictException("Email này đã tồn tại trong hệ thống!");
throw new BadRequestException("Định dạng file không hợp lệ!");
```

### Bước 2 – Tạo "khuôn" JSON lỗi chung

**File:** `DTOs/ErrorResponse.cs`

```csharp
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }   // chỉ có ở Development

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }   // chỉ có khi lỗi validation
}
```

**Kết quả JSON mọi lỗi đều giống nhau:**
```json
{ "statusCode": 404, "message": "Không tìm thấy sinh viên có Id = 99!", "details": "at QuanLySinhVien.Services..." }
```

| Trường | Vì sao |
|---|---|
| `statusCode` | Angular biết loại lỗi |
| `message` | Câu tiếng Việt hiện cho người dùng |
| `details` | Stack trace để debug – **chỉ ở Development** |
| `errors` + `JsonIgnore(WhenWritingNull)` | Lỗi từng ô nhập; không có thì không xuất hiện trong JSON cho gọn |

### Bước 3 – Viết ExceptionMiddleware

**File:** `Middleware/ExceptionMiddleware.cs`

```csharp
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    { _next = next; _logger = logger; _env = env; }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);                       // (a) cho request đi tiếp
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)            // (b) đã gửi response thì không sửa được
            {
                _logger.LogError(ex, "Lỗi xảy ra sau khi response đã bắt đầu gửi");
                throw;
            }
            if (ex is AppException)                     // (c) phân mức log
                _logger.LogWarning("Lỗi nghiệp vụ: {Message}", ex.Message);
            else
                _logger.LogError(ex, "Một sự cố hệ thống đã xảy ra: {Message}", ex.Message);

            await HandleExceptionAsync(context, ex);    // (d) trả JSON
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var response = new ErrorResponse
        {
            Details = _env.IsDevelopment() ? exception.StackTrace?.ToString() : null   // (e)
        };

        var (statusCode, message) = exception switch                                    // (f)
        {
            AppException appEx           => (appEx.StatusCode, appEx.Message),
            UnauthorizedAccessException  => (401, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ!"),
            ArgumentException or BadHttpRequestException => (400, exception.Message),
            DbUpdateConcurrencyException => (409, "Dữ liệu đã bị thay đổi bởi người khác, vui lòng tải lại!"),
            DbUpdateException            => (409, "Dữ liệu bị trùng hoặc vi phạm ràng buộc CSDL!"),
            _                            => (500, "Đã xảy ra sự cố hệ thống! Vui lòng liên hệ Admin hoặc thử lại sau.")
        };
        context.Response.StatusCode = statusCode;
        response.StatusCode = statusCode;
        response.Message = message;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };  // (g)
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
```

| Ký hiệu | Giải thích dễ hiểu | Vì sao |
|---|---|---|
| (a) `try { await _next(context); }` | "Mở cửa" cho request đi qua **toàn bộ** phần phía sau (Auth, Controller, Service, DB) | Lỗi ở bất kỳ đâu phía sau đều rơi vào `catch` này |
| (b) `HasStarted` | Response đã bắt đầu gửi về client chưa? | Đã gửi rồi thì **không đổi được mã lỗi** nữa; cố ghi sẽ sinh lỗi thứ 2 → chỉ log rồi `throw;` |
| (c) `LogWarning` / `LogError` | Lỗi người dùng nhập sai chỉ là "cảnh báo"; lỗi hệ thống mới là "lỗi" | Khi đọc log, sự cố thật nổi bật, không bị lẫn |
| (d) `HandleExceptionAsync` | Tách phần "dựng JSON" ra hàm riêng | Code `InvokeAsync` gọn, dễ đọc |
| (e) `IsDevelopment() ? StackTrace : null` | Chỉ lộ stack trace khi đang phát triển | **Bảo mật**: Production không lộ cấu trúc code |
| (f) `exception switch` | Pattern matching: xét **loại** exception để chọn mã + câu | Thay cho chuỗi `if-else` dài |
| (f) `DbUpdateConcurrencyException` **trước** `DbUpdateException` | Lớp con phải đứng trước lớp cha | Switch xét từ trên xuống; đặt cha trước thì con không bao giờ được bắt riêng |
| (f) `_ => 500` | Lỗi lạ không lường trước | Trả câu chung chung, không lộ chi tiết |
| (g) `CamelCase` | `StatusCode` → `statusCode` | Đúng quy ước JavaScript để Angular đọc |

### Bước 4 – Đăng ký Middleware ĐẦU TIÊN trong pipeline

**File:** `Program.cs`

```csharp
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();   // ← đứng đầu
app.UseStatusCodePages(...);
app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Vì sao đứng đầu?** Middleware chỉ bắt được lỗi của những phần chạy **sau** nó. Đứng đầu = bọc toàn bộ pipeline.

### Bước 5 – Bắt nốt lỗi KHÔNG phải exception

Có 2 loại lỗi **không đi qua** `catch` của Middleware:

**a) Lỗi validation** (`[Required]`, `[Range]`...) – bị `[ApiController]` chặn **trước** khi vào Controller, không ném exception.

```csharp
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new ErrorResponse
            {
                StatusCode = 400,
                Message = errors.Values.SelectMany(v => v).FirstOrDefault(m => !string.IsNullOrEmpty(m))
                          ?? "Dữ liệu gửi lên không hợp lệ!",
                Errors = errors
            });
        };
        options.SuppressMapClientErrors = true;
    });
```
→ Lấy **câu lỗi đầu tiên** làm `message` (để Toast hiện 1 câu rõ ràng), và giữ toàn bộ lỗi trong `errors`.

**b) Lỗi 401/403/404 do Authentication/Authorization/routing** – chỉ là **mã trạng thái** với body rỗng, không phải exception.

```csharp
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    var status = http.Response.StatusCode;
    var message = status switch
    {
        401 => "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn!",
        403 => "Bạn không có quyền thực hiện chức năng này!",
        404 => "Không tìm thấy tài nguyên yêu cầu!",
        _   => "Đã xảy ra lỗi khi xử lý yêu cầu!"
    };
    await http.Response.WriteAsJsonAsync(new ErrorResponse { StatusCode = status, Message = message });
});
```
→ Khi response **rỗng** với mã lỗi, viết thêm JSON chuẩn vào.

**Tổng hợp: lỗi nào – ai xử lý:**

| Loại lỗi | Ví dụ | Ai xử lý |
|---|---|---|
| Lỗi nghiệp vụ | Không tìm thấy SV, trùng email | `ExceptionMiddleware` (AppException) |
| Lỗi DB | Vi phạm UNIQUE, đụng độ dữ liệu | `ExceptionMiddleware` (DbUpdate...) |
| Lỗi bất ngờ | Mất kết nối DB, NullReference | `ExceptionMiddleware` → 500 |
| Lỗi validation | Tuổi = 10, email sai định dạng | `InvalidModelStateResponseFactory` |
| 401 / 403 / 404 route | Chưa đăng nhập, không đủ quyền | `UseStatusCodePages` |

### Bước 6 – Controller/Service chỉ việc `throw`

```csharp
// Service – gọn gàng, không try/catch, không return BadRequest(...)
if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower()))
    throw new ConflictException("Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác.");
```

### Bước 7 – Angular đọc định dạng chuẩn

**File:** `models/api-error.ts` – khai báo hình dạng JSON lỗi:
```typescript
export interface ApiError {
  statusCode: number;
  message: string;
  details?: string;
  errors?: Record<string, string[]>;
}
```

**File:** `utils/error-message.ts` – một hàm duy nhất lấy câu thông báo:
```typescript
export function layThongBaoLoi(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại backend!';
    const body = err.error as ApiError | null;
    if (body && typeof body === 'object' && body.message) return body.message;
    if (typeof err.error === 'string' && err.error.trim()) return err.error;
  }
  return 'Đã xảy ra lỗi không xác định!';
}
```
| Trường hợp | Vì sao |
|---|---|
| `status === 0` | Server tắt / sai địa chỉ → trình duyệt không nhận được mã HTTP nào |
| `body.message` | Định dạng chuẩn của mình – trường hợp chính |
| Chuỗi chữ | Phòng hờ server trả lỗi dạng text |
| Câu mặc định | Không bao giờ hiện thông báo trống |

**File:** `interceptors/error.ts` – bắt mọi lỗi HTTP ở một nơi:
```typescript
return next(req).pipe(
  catchError((error: HttpErrorResponse) => {
    if (typeof window === 'undefined') return throwError(() => error);        // SSR: bỏ qua

    const apiError = error.error as ApiError | null;
    if (apiError?.details) console.error('[API details]', apiError.details);  // debug ở Dev

    const laApiXacThuc = req.url.includes('/api/XacThuc/');
    if (!laApiXacThuc) toastService.showError(layThongBaoLoi(error));         // hiện Toast

    if (error.status === 401 && !laApiXacThuc) { authService.logout(); router.navigate(['/login']); }
    else if (error.status === 403) { router.navigate(['/sinh-vien']); }

    return throwError(() => error);                                            // component vẫn xử lý riêng được
  })
);
```
| Dòng | Vì sao |
|---|---|
| `console.error(details)` | Lập trình viên xem stack trace trong F12 (chỉ có ở Dev) |
| `laApiXacThuc` → không Toast | Trang Đăng nhập/Đăng ký tự hiện banner lỗi, tránh **báo 2 lần** |
| `401` → logout + `/login` | Token hết hạn/không hợp lệ → bắt đăng nhập lại |
| `403` → `/sinh-vien` | Không đủ quyền → về trang được phép |
| `throwError` | Ném tiếp để component xử lý thêm (ví dụ đóng form khi 409 đụng độ) |

## ④ Luồng chạy hoàn chỉnh – ví dụ "Thêm sinh viên trùng email"

```
Angular  POST /api/SinhVien {email: "a@x.com"}
   ▼
① ExceptionMiddleware: try { await _next(context) }
   ▼
② ...Auth, Authorization (Admin ✓)...
   ▼
③ SinhVienController.Create → SinhVienService.CreateAsync
   ▼
④ AnyAsync(email trùng) = true → throw new ConflictException("Email này đã tồn tại...")
   ▼  (exception "nổi" ngược lên)
⑤ ExceptionMiddleware.catch → LogWarning → switch → (409, "Email này đã tồn tại...")
   ▼
⑥ Response 409: {"statusCode":409,"message":"Email này đã tồn tại...","details":"at ..."}
   ▼
⑦ Angular errorInterceptor → console.error(details) → Toast đỏ "Email này đã tồn tại..."
```

## ⑤ Kiểm tra trên web

| Thao tác | Kết quả đúng |
|---|---|
| Thêm SV với tuổi = 10 | Toast "Tuổi phải là số dương từ 18 đến 99!", Network: 400 có `errors` |
| Thêm SV trùng email | Toast "Email này đã tồn tại...", Network: 409 |
| Đăng nhập Giảng viên, gọi POST bằng Postman | 403 + JSON `{statusCode:403, message:"Bạn không có quyền..."}` |
| F12 → Console: `fetch('https://localhost:7280/api/SinhVien/99999', {headers:{Authorization:'Bearer '+localStorage.getItem('token')}}).then(r=>r.json()).then(console.log)` | `{statusCode: 404, message: "Không tìm thấy sinh viên có Id = 99999!", details: "..."}` |
| Tắt API, F5 trang | Thông báo "Không thể kết nối đến máy chủ..." |
| Mở Console khi có lỗi | Dòng đỏ `[API details]` = stack trace (chỉ ở Dev) |

## ⑥ Từ khóa

**Middleware · Pipeline · RequestDelegate (`_next`) · Global Exception Handling · Custom Exception · Pattern Matching (`switch`) · `HasStarted` · ILogger (LogWarning/LogError) · IHostEnvironment (`IsDevelopment`) · ProblemDetails · InvalidModelStateResponseFactory · UseStatusCodePages · camelCase · HttpInterceptorFn · catchError · throwError**

## ⑦ Câu hỏi vấn đáp

**Q: Vì sao không dùng `try/catch` trong từng Controller?**
A: Lặp code, dễ quên, mỗi chỗ trả một kiểu. Middleware gom về một nơi, mọi lỗi cùng định dạng.

**Q: Vì sao Middleware phải đứng đầu pipeline?**
A: Middleware chỉ bắt lỗi của phần chạy sau nó. Đứng đầu thì bọc được toàn bộ.

**Q: `HasStarted` dùng để làm gì?**
A: Response đã gửi một phần thì không thể đổi status code/header; middleware chỉ ghi log rồi ném lại, tránh sinh lỗi thứ hai.

**Q: Vì sao `DbUpdateConcurrencyException` phải đứng trước `DbUpdateException`?**
A: Nó là lớp con. Switch khớp từ trên xuống, đặt lớp cha trước thì lớp con không bao giờ được bắt riêng.

**Q: Vì sao `details` chỉ có ở Development?**
A: Stack trace lộ cấu trúc code, đường dẫn server. Production để `null` – đã có unit test kiểm tra.

**Q: Lỗi validation và 401/403 có đi qua Middleware không?**
A: Không. Validation bị chặn trước Controller → `InvalidModelStateResponseFactory`. 401/403 là mã trạng thái, không phải exception → `UseStatusCodePages`.

**Q: Angular đọc lỗi thế nào?**
A: `errorInterceptor` bắt mọi lỗi HTTP, dùng `layThongBaoLoi` đọc `message` rồi hiện Toast; 401 thì đăng xuất, 403 thì về trang danh sách.

---

# YÊU CẦU 2 – DỨT ĐIỂM AUDIT LOGGING BẰNG SaveChangesInterceptor

## ① Vấn đề trước khi làm

Ở Tuần 2, ghi Audit Log bằng cách **viết tay trong từng Controller**:

```csharp
// Kiểu cũ – trong Create, Update, Delete đều phải thêm
_context.AuditLogs.Add(new AuditLog { Username = User.Identity.Name, Action = "Sửa", ... });
await _context.SaveChangesAsync();
```

| Vấn đề | Ảnh hưởng |
|---|---|
| Phải nhớ viết ở **mọi** chỗ thay đổi dữ liệu | Quên một chỗ (ví dụ UploadAvatar) là mất dấu vết |
| Code log lẫn vào code nghiệp vụ | Controller dài, khó đọc |
| Tự lấy giá trị cũ/mới bằng tay | Dễ sai, dễ thiếu cột |

## ② Ý tưởng giải quyết

> **"Đừng bắt nhân viên tự ghi sổ – lắp camera tự động ở cửa kho."**

Mọi thay đổi dữ liệu trong EF Core **đều phải đi qua `SaveChanges`**. Nếu mình "chen" vào đúng thời điểm **ngay trước khi lưu**, mình thấy được toàn bộ thay đổi và tự ghi log – không cần Controller làm gì.

EF Core cung cấp sẵn lớp **`SaveChangesInterceptor`** cho đúng mục đích này.

## ③ Các bước làm

### Bước 1 – Bảng và Model AuditLog

**SQL:**
```sql
CREATE TABLE AuditLogs (
    Id INT IDENTITY PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Action NVARCHAR(20) NOT NULL,
    TableName NVARCHAR(50) NOT NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    Timestamp DATETIME NOT NULL
);
```

**File:** `Models/AuditLog.cs`
```csharp
public class AuditLog
{
    public int Id { get; set; }
    [MaxLength(100)] public string Username { get; set; } = "Anonymous";   // ai làm
    [MaxLength(20)]  public string Action { get; set; } = string.Empty;     // Thêm/Sửa/Xóa
    [MaxLength(50)]  public string TableName { get; set; } = string.Empty;  // bảng nào
    public string? OldValues { get; set; }                                   // giá trị cũ (JSON)
    public string? NewValues { get; set; }                                   // giá trị mới (JSON)
    public DateTime Timestamp { get; set; } = DateTime.Now;                  // lúc nào
}
```

### Bước 2 – Viết Interceptor

**File:** `Data/AuditSaveChangesInterceptor.cs`

**2a. Khai báo:**
```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HashSet<string> SensitiveProperties = new() { "PasswordHash", "RowVersion" };

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;
```
| Phần | Vì sao |
|---|---|
| `: SaveChangesInterceptor` | Lớp có sẵn của EF để "chen" vào quá trình lưu |
| `IHttpContextAccessor` | Interceptor không phải Controller nên không có `User`; cần cái này để biết **ai** đang thao tác |
| `SensitiveProperties` | Không ghi mật khẩu (bảo mật) và RowVersion (dữ liệu kỹ thuật vô nghĩa) vào log |

**2b. Chen vào trước khi lưu:**
```csharp
public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
{
    var context = eventData.Context;
    if (context != null) await OnBeforeSaveChanges(context);
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
}
```
→ `SavingChangesAsync` (có chữ **ing**) chạy **ngay trước** khi lưu → còn kịp đọc **giá trị cũ** và **thêm** dòng log vào cùng lần lưu. Xong thì gọi `base` để EF lưu bình thường.

**2c. Duyệt các thay đổi:**
```csharp
private async Task OnBeforeSaveChanges(DbContext context)
{
    context.ChangeTracker.DetectChanges();
    var auditEntries = new List<AuditEntry>();
    var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";

    foreach (var entry in context.ChangeTracker.Entries().ToList())
    {
        if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            continue;
```
| Phần | Vì sao |
|---|---|
| `DetectChanges()` | Bắt EF quét lại để chắc chắn biết đủ mọi thay đổi |
| `User.Identity.Name` | Lấy từ claim `Name` trong JWT = username |
| `?.` và `?? "Anonymous"` | Không có HTTP (ví dụ khi test) thì không lỗi, ghi "Anonymous" |
| `ChangeTracker.Entries()` | Danh sách mọi đối tượng EF đang theo dõi, kèm **trạng thái** |
| `.ToList()` | Chụp danh sách trước, vì bên dưới sẽ **thêm** AuditLog vào context (thêm trong lúc duyệt sẽ lỗi) |
| Bỏ qua `AuditLog` | Không ghi log cho chính việc ghi log → tránh vòng lặp vô tận |
| Bỏ qua `Unchanged`, `Detached` | Không thay đổi gì thì không ghi |

**2d. Xác định hành động:**
```csharp
var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
bool isSoftDelete = isDeletedProp != null && Equals(isDeletedProp.CurrentValue, true);

if (isSoftDelete)                                 auditEntry.Action = "Xóa";
else if (entry.State == EntityState.Added)        auditEntry.Action = "Thêm";
else if (entry.State == EntityState.Deleted)      auditEntry.Action = "Xóa";
else if (entry.State == EntityState.Modified)     auditEntry.Action = "Sửa";
```

| EntityState | Nghĩa | Ghi là |
|---|---|---|
| `Added` | Đối tượng mới, sắp INSERT | Thêm |
| `Modified` | Có cột bị đổi, sắp UPDATE | Sửa |
| `Deleted` | Sắp DELETE | Xóa |
| `Modified` + `IsDeleted = true` | Xóa mềm (UPDATE cờ) | **Xóa** |

→ Xóa mềm về kỹ thuật là **UPDATE**, nên phải kiểm tra `IsDeleted` **trước** thì mới ghi đúng là "Xóa".

**2e. Ghi giá trị cũ/mới:**
```csharp
foreach (var property in entry.Properties)
{
    string name = property.Metadata.Name;
    if (SensitiveProperties.Contains(name)) continue;
    if (property.Metadata.IsPrimaryKey()) { auditEntry.KeyValues[name] = property.CurrentValue ?? ""; continue; }

    switch (entry.State)
    {
        case EntityState.Added:
            auditEntry.NewValues[name] = property.CurrentValue ?? ""; break;
        case EntityState.Deleted:
            auditEntry.OldValues[name] = property.OriginalValue ?? ""; break;
        case EntityState.Modified:
            if (property.IsModified)
            {
                auditEntry.OldValues[name] = property.OriginalValue ?? "";
                auditEntry.NewValues[name] = property.CurrentValue ?? "";
            }
            break;
    }
}
```
| Phần | Vì sao |
|---|---|
| `OriginalValue` | Giá trị lúc EF đọc từ DB (**trước** khi sửa) |
| `CurrentValue` | Giá trị hiện tại (**sau** khi sửa) |
| `IsModified` | Chỉ ghi cột **thật sự đổi** → log gọn, dễ đọc |
| Thêm → chỉ `NewValues`; Xóa cứng → chỉ `OldValues` | Thêm thì chưa có giá trị cũ; xóa thì không còn giá trị mới |

**2f. Thêm log vào cùng lần lưu:**
```csharp
foreach (var auditEntry in auditEntries)
    context.Set<AuditLog>().Add(auditEntry.ToAudit());
```
→ Dòng log được lưu **trong cùng giao dịch** với dữ liệu: dữ liệu lưu thành công thì log cũng có; lỗi thì cả hai cùng không lưu → **luôn khớp nhau**.

`ToAudit()` dùng `JsonSerializer.Serialize(OldValues)` để biến Dictionary thành chuỗi JSON, ví dụ `{"Tuoi":20}` → `{"Tuoi":21}`.

### Bước 3 – Đăng ký trong Program.cs

```csharp
builder.Services.AddHttpContextAccessor();                       // cho phép lấy HttpContext ngoài Controller
builder.Services.AddScoped<AuditSaveChangesInterceptor>();       // mỗi request một bản
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());   // gắn vào DbContext
});
```
| Dòng | Vì sao |
|---|---|
| `AddHttpContextAccessor` | Không có thì `IHttpContextAccessor` không được cung cấp → Interceptor lỗi |
| `AddScoped` | Interceptor phụ thuộc thông tin của **từng request** |
| `AddInterceptors(...)` | Gắn "camera" vào DbContext; từ đây **mọi** `SaveChanges` đều qua Interceptor |

### Bước 4 – Xóa code ghi log tay trong Controller

Sau khi có Interceptor, **xóa hết** các dòng `_context.AuditLogs.Add(...)` cũ. Controller/Service chỉ còn nghiệp vụ.

### Bước 5 – API xem lịch sử (đã có từ Tuần 2)

```csharp
[Authorize(Roles = "Admin")]
public class AuditLogController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.AuditLogs.AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditLogDto { ... })
            .ToListAsync());
}
```

## ④ Luồng chạy – ví dụ "Sửa tuổi sinh viên từ 20 → 21"

```
Service: sv.Tuoi = 21; await _context.SaveChangesAsync();
   ▼
EF: "sắp lưu" → gọi AuditSaveChangesInterceptor.SavingChangesAsync
   ▼
ChangeTracker: entry SinhVien, State = Modified, Tuoi: Original=20, Current=21 (IsModified)
   ▼
Tạo AuditLog { Username="admin1", Action="Sửa", TableName="SinhVien",
               OldValues={"Tuoi":20}, NewValues={"Tuoi":21} }
   ▼
EF lưu CÙNG LÚC:  UPDATE SinhVien SET Tuoi=21 ...;  INSERT INTO AuditLogs (...)
```

## ⑤ Kiểm tra trên web

| Thao tác (tài khoản Admin) | Kết quả đúng ở trang **Lịch sử** |
|---|---|
| Thêm sinh viên | Dòng "Thêm", NewValues có HoTen, Email, Tuoi |
| Sửa tuổi | Dòng "Sửa", chỉ có cột Tuoi cũ → mới |
| Xóa sinh viên | Dòng "**Xóa**" (dù thực chất là UPDATE IsDeleted) |
| Upload ảnh | Dòng "Sửa" với AvatarUrl cũ → mới (Controller **không** có dòng log nào – chứng minh tự động) |
| Đăng ký tài khoản | Dòng "Thêm" bảng Users, **không có** PasswordHash |
| Đăng nhập Giảng viên, gõ `/lich-su` | Bị chặn – chỉ Admin xem được |

## ⑥ Từ khóa

**Audit Trail · Interceptor · SaveChangesInterceptor · SavingChangesAsync · ChangeTracker · EntityEntry · EntityState (Added/Modified/Deleted/Unchanged/Detached) · OriginalValue / CurrentValue · IsModified · IHttpContextAccessor · Claims (Identity.Name) · Transaction · Cross-cutting concern · Separation of concerns**

## ⑦ Câu hỏi vấn đáp

**Q: Interceptor là gì? Vì sao dùng thay vì ghi log trong Controller?**
A: Là lớp chen vào quá trình `SaveChanges` của EF. Ghi tay dễ quên và lặp code; Interceptor chạy tự động ở **mọi** lần lưu, Controller không cần dòng log nào.

**Q: Vì sao dùng `SavingChangesAsync` (trước khi lưu) mà không phải `SavedChangesAsync` (sau khi lưu)?**
A: Trước khi lưu mới đọc được `OriginalValue` và thêm AuditLog vào **cùng** lần lưu. Sau khi lưu thì trạng thái đã reset về Unchanged, mất giá trị cũ, và phải lưu log ở lần thứ hai.

**Q: Làm sao biết ai thao tác?**
A: Qua `IHttpContextAccessor` → `HttpContext.User.Identity.Name` (claim Name trong JWT). Phải đăng ký `AddHttpContextAccessor()`.

**Q: Xóa mềm được ghi là gì? Vì sao?**
A: Ghi là "Xóa". Xóa mềm về kỹ thuật là Modified, nên Interceptor kiểm tra `IsDeleted` chuyển sang true trước rồi mới xét State.

**Q: Vì sao không ghi PasswordHash?**
A: Log được nhiều người xem; không bao giờ để lộ dữ liệu mật khẩu, dù đã băm.

**Q: Nếu lưu dữ liệu thất bại thì log có bị ghi không?**
A: Không. Log nằm trong cùng giao dịch `SaveChanges`, lỗi thì cả hai cùng không lưu.

**Q: Hạn chế hiện tại?**
A: Log chưa lưu Id của bản ghi bị tác động; có thể thêm cột `RecordId` để biết chính xác sinh viên nào.

---

# YÊU CẦU 3 – QUẢN LÝ FILE NÂNG CAO: XÓA ẢNH CŨ

## ① Vấn đề trước khi làm

Mỗi lần đổi ảnh đại diện, file **mới** được lưu vào `wwwroot/avatars` nhưng file **cũ vẫn nằm đó**. Đổi ảnh 10 lần = 10 file, chỉ 1 file được dùng → ổ cứng đầy rác.

Ngoài ra còn rủi ro: nếu lưu file xong mà **lưu DB lỗi**, file mới thành rác; nếu xóa ảnh cũ **trước** mà lưu DB lỗi, DB trỏ tới ảnh đã mất.

## ② Ý tưởng giải quyết

> **"Cất đồ mới vào tủ xong, chắc chắn đã ghi sổ, rồi mới vứt đồ cũ."**

Thứ tự đúng: **ghi file mới → lưu DB → (thành công) xóa file cũ**. Lưu DB lỗi → xóa file mới vừa ghi.

## ③ Các bước làm

**File:** `Controllers/SinhVienController.cs` → `UploadAvatar` (phần cuối)

```csharp
// (đã kiểm tra tồn tại, đuôi file, dung lượng; đã ghi file mới vào filePath)

var oldAvatarUrl = sinhVien.AvatarUrl;                       // ① nhớ ảnh cũ
sinhVien.AvatarUrl = $"/avatars/{uniqueFileName}";            // ② gán ảnh mới

try
{
    await _context.SaveChangesAsync();                        // ③ lưu DB
}
catch
{
    if (System.IO.File.Exists(filePath))                      // ③' lỗi → xóa file MỚI
        System.IO.File.Delete(filePath);
    throw;                                                    //      ném tiếp cho Middleware
}

if (!string.IsNullOrEmpty(oldAvatarUrl))                      // ④ thành công → xóa file CŨ
{
    var oldAbsoluteFilePath = Path.Combine(_env.WebRootPath, oldAvatarUrl.TrimStart('/'));
    if (System.IO.File.Exists(oldAbsoluteFilePath))
        System.IO.File.Delete(oldAbsoluteFilePath);
}
return Ok(new { AvatarUrl = sinhVien.AvatarUrl });
```

| Bước | Giải thích | Vì sao |
|---|---|---|
| ① Lưu `oldAvatarUrl` **trước** khi gán | Gán xong thì mất đường dẫn cũ | Cần nó để xóa ở bước ④ |
| ③ `try { SaveChangesAsync }` | Lưu đường dẫn mới vào DB | |
| ③' `catch` → xóa file mới | DB lỗi thì file mới thành rác | Dọn ngay, không để lại |
| `throw;` | Vẫn báo lỗi | `ExceptionMiddleware` trả JSON lỗi cho người dùng |
| ④ Xóa file cũ **sau** khi DB thành công | Chắc chắn DB đã trỏ sang ảnh mới | Nếu xóa trước mà DB lỗi → mất ảnh đang dùng |
| `string.IsNullOrEmpty` | Sinh viên chưa có ảnh thì không xóa gì | Tránh lỗi đường dẫn rỗng |
| `TrimStart('/')` | `/avatars/a.jpg` → `avatars/a.jpg` | `Path.Combine` gặp `/` đầu sẽ hiểu là đường dẫn gốc, bỏ qua `WebRootPath` |
| `_env.WebRootPath` | Đường dẫn tuyệt đối tới `wwwroot` | Không viết cứng `D:\...` |
| `File.Exists` trước `Delete` | File có thể đã bị xóa tay | Tránh lỗi khi file không tồn tại |
| `System.IO.File` (đầy đủ) | Trong Controller, `File(...)` là hàm trả file của ASP.NET | Tránh trùng tên |

**Phần upload phía trước (nhắc lại):** kiểm tra SV tồn tại → file không rỗng → đuôi `.jpg/.jpeg/.png` → ≤ 2MB → tên file bằng `Guid` → ghi vào `wwwroot/avatars` bằng `using FileStream`.

## ④ Luồng chạy

```
Ảnh hiện tại: /avatars/aaa.jpg      Người dùng chọn ảnh mới
   ▼
Kiểm tra hợp lệ ✓ → ghi file bbb.jpg
   ▼
oldAvatarUrl = "/avatars/aaa.jpg"; AvatarUrl = "/avatars/bbb.jpg"
   ▼
SaveChangesAsync ──✗ lỗi──▶ xóa bbb.jpg → throw → JSON lỗi (aaa.jpg vẫn còn, DB vẫn trỏ aaa.jpg ✓)
   │
   ✓ thành công
   ▼
Xóa aaa.jpg → trả {avatarUrl: "/avatars/bbb.jpg"}   (thư mục chỉ còn bbb.jpg ✓)
```

## ⑤ Kiểm tra trên web

1. Mở thư mục `D:\TT\QuanLySinhVien\QuanLySinhVien\wwwroot\avatars`, ghi nhớ số file.
2. Upload ảnh cho sinh viên A (chưa có ảnh) → số file **+1**.
3. Upload ảnh **khác** cho chính sinh viên A → số file **không đổi** (file cũ đã bị xóa, file mới được thêm).
4. Trang Lịch sử: dòng "Sửa" với `AvatarUrl` cũ → mới.

## ⑥ Từ khóa

**IFormFile · wwwroot / WebRootPath · Path.Combine · Guid · FileStream + using · File.Exists / File.Delete · Rollback thủ công · Orphan file (file mồ côi) · Thứ tự thao tác an toàn**

## ⑦ Câu hỏi vấn đáp

**Q: Vì sao xóa ảnh cũ sau khi lưu DB mà không phải trước?**
A: Xóa trước mà DB lỗi thì DB vẫn trỏ tới ảnh đã mất. Lưu DB thành công rồi mới xóa là an toàn.

**Q: Nếu lưu DB lỗi thì sao?**
A: Xóa file mới vừa ghi để không thành rác, rồi `throw` để Middleware trả lỗi; ảnh cũ và DB giữ nguyên.

**Q: Vì sao đặt tên file bằng Guid?**
A: Tránh trùng tên khi nhiều người upload cùng tên, tránh ghi đè, tránh đoán được đường dẫn.

**Q: Vì sao `TrimStart('/')`?**
A: `Path.Combine` gặp chuỗi bắt đầu bằng `/` sẽ coi là đường dẫn gốc và bỏ phần `WebRootPath`.

**Q: Trường hợp xóa file cũ bị lỗi (file đang bị khóa)?**
A: Hiện tại sẽ ném lỗi dù DB đã lưu. Có thể cải tiến bằng cách bọc `try/catch` và chỉ ghi log cảnh báo, vì việc dọn file không nên làm hỏng thao tác chính.

---

# TỔNG HỢP – BỨC TRANH CHUNG CỦA TUẦN 3-4

## Ba yêu cầu cùng giải quyết "việc chung" (cross-cutting concerns)

```
                       ┌───────────────────────────────────────────┐
  Request ──▶ ExceptionMiddleware (bắt MỌI lỗi → JSON chuẩn)         │  ← Yêu cầu 1
                       │                                             │
                       ▼                                             │
              Controller / Service  (chỉ lo nghiệp vụ, chỉ việc throw)│
                       │                                             │
                       ▼                                             │
              SaveChangesAsync ──▶ AuditInterceptor (tự ghi log)      │  ← Yêu cầu 2
                       │                                             │
                       ▼                                             │
              SQL Server  +  wwwroot/avatars (xóa ảnh cũ an toàn)     │  ← Yêu cầu 3
                       └───────────────────────────────────────────┘
  Angular errorInterceptor ◀── JSON lỗi chuẩn ── hiện Toast           ← Yêu cầu 1 (frontend)
```

| | Trước Tuần 3-4 | Sau Tuần 3-4 |
|---|---|---|
| **Lỗi** | Mỗi API trả một kiểu, có thể lộ stack trace | Mọi lỗi cùng định dạng `{statusCode, message, details}`, Production không lộ gì |
| **Nhật ký** | Viết tay từng chỗ, dễ quên | Tự động 100% qua Interceptor, cùng giao dịch với dữ liệu |
| **File** | Ảnh cũ tích tụ thành rác | Ảnh cũ được xóa an toàn, lỗi thì dọn file mới |
| **Controller** | Dài, lẫn try/catch và log | Gọn, chỉ có nghiệp vụ |

## Nguyên lý cốt lõi (nói được câu này là "ăn điểm")

1. **Separation of Concerns (tách trách nhiệm):** Controller/Service lo nghiệp vụ; việc chung (lỗi, log) để Middleware/Interceptor lo.
2. **DRY (Don't Repeat Yourself):** viết một lần, áp dụng cho mọi request / mọi lần lưu.
3. **Fail-safe ordering (thứ tự an toàn):** làm bước khó hoàn tác sau cùng (xóa file cũ chỉ sau khi DB thành công).
4. **Security by default:** không lộ stack trace ở Production, không ghi mật khẩu vào log.

---

# KỊCH BẢN TRÌNH BÀY (~3 phút)

> "Ở Tuần 3-4, em tập trung vào ba việc chung của hệ thống: xử lý lỗi, ghi nhật ký và quản lý file.
>
> **Thứ nhất, Exception Handling.** Trước đây mỗi API trả lỗi một kiểu và lỗi bất ngờ có thể lộ stack trace. Em tạo các exception tự định nghĩa như `NotFoundException`, `ConflictException`, mỗi loại gắn sẵn mã HTTP. Service chỉ cần `throw`. `ExceptionMiddleware` đứng đầu pipeline, bọc toàn bộ request trong `try/catch`, dùng pattern matching để đổi exception thành JSON chuẩn `{statusCode, message, details}`. Chi tiết stack trace chỉ có ở Development. Lỗi validation và 401/403 không phải exception nên em xử lý thêm bằng `InvalidModelStateResponseFactory` và `UseStatusCodePages`. Phía Angular, `errorInterceptor` đọc `message` và hiện Toast ở một nơi duy nhất.
>
> **Thứ hai, Audit Logging.** Thay vì ghi log tay trong từng Controller, em viết `AuditSaveChangesInterceptor` kế thừa `SaveChangesInterceptor`. Ngay trước mỗi lần lưu, nó duyệt `ChangeTracker`, xác định Thêm/Sửa/Xóa — xóa mềm được nhận ra qua cột `IsDeleted` — lấy giá trị cũ và mới của các cột thật sự thay đổi, bỏ qua mật khẩu, lấy người thực hiện từ JWT qua `IHttpContextAccessor`, rồi thêm dòng log vào cùng giao dịch. Nhờ vậy dữ liệu và nhật ký luôn khớp nhau, và Controller không còn dòng log nào.
>
> **Thứ ba, quản lý file.** Khi đổi ảnh đại diện, em ghi file mới, lưu DB, và chỉ khi lưu thành công mới xóa file cũ. Nếu lưu lỗi, em xóa file mới vừa ghi. Thứ tự này đảm bảo không mất ảnh đang dùng và không để lại file rác.
>
> Ba phần này đều theo nguyên tắc tách trách nhiệm: Controller và Service chỉ lo nghiệp vụ, còn các việc chung được xử lý tự động ở một nơi."
