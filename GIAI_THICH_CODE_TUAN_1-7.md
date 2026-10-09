# GIẢI THÍCH CODE TỪNG YÊU CẦU – DỰ ÁN QUẢN LÝ SINH VIÊN (TUẦN 1 → TUẦN 7)

> Người thực hiện: Nguyễn Thanh Phong
>
> **Cách đọc tài liệu này:** mỗi yêu cầu có 4 phần:
> 1. **Hiểu đơn giản**: một ví dụ đời thường để nắm ý tưởng.
> 2. **Code thật trong dự án**: trích từ file của bạn.
> 3. **Giải thích từng dòng**: dòng đó làm gì và **vì sao phải có**.
> 4. **Nếu bỏ dòng đó thì sao**: để hiểu tầm quan trọng.

---

## Mục lục

**PHẦN 0 – Bức tranh tổng thể**
- [0.1 Dự án gồm những gì](#01-dự-án-gồm-những-gì)
- [0.2 `Program.cs` – "bảng điều khiển" của backend](#02-programcs--bảng-điều-khiển-của-backend)
- [0.3 `app.config.ts` – "bảng điều khiển" của Angular](#03-appconfigts--bảng-điều-khiển-của-angular)

**TUẦN 1 – Nền tảng**
- [1.1 DTO](#11-dto--phiếu-thông-tin-rút-gọn)
- [1.2 Validation](#12-validation--kiểm-tra-dữ-liệu-đầu-vào)
- [1.3 Hash mật khẩu bằng BCrypt](#13-hash-mật-khẩu-bằng-bcrypt)
- [1.4 JWT – tạo token](#14-jwt--tạo-token-khi-đăng-nhập)
- [1.5 JWT – Angular lưu và gửi token](#15-jwt--angular-lưu-và-gửi-token)
- [1.6 Xử lý lỗi đăng nhập / mất kết nối](#16-xử-lý-lỗi-đăng-nhập-và-mất-kết-nối)

**TUẦN 2 – Tính năng chính**
- [2.1 Phân trang, tìm kiếm, sắp xếp](#21-phân-trang-tìm-kiếm-sắp-xếp)
- [2.2 Phân quyền 3 lớp](#22-phân-quyền-3-lớp)
- [2.3 Upload ảnh đại diện](#23-upload-ảnh-đại-diện)
- [2.4 Audit Log – xem lịch sử](#24-audit-log--api-xem-lịch-sử)
- [2.5 Chuẩn hóa lỗi + Toast](#25-chuẩn-hóa-lỗi--toast)

**TUẦN 3-4 – Exception, Audit, file**
- [3.1 Exception tự định nghĩa](#31-exception-tự-định-nghĩa)
- [3.2 ExceptionMiddleware](#32-exceptionmiddleware)
- [3.3 AuditSaveChangesInterceptor](#33-auditsavechangesinterceptor)
- [3.4 Xóa ảnh cũ](#34-xóa-ảnh-cũ-khi-đổi-ảnh)

**TUẦN 5-6 – Soft-Delete, Service, Unit Test**
- [4.1 Xóa mềm + Global Query Filter](#41-xóa-mềm--global-query-filter)
- [4.2 Tầng Service](#42-tầng-service)
- [4.3 Unit Test](#43-unit-test)

**TUẦN 7 – Kiểm thử nâng cao**
- [5.1 Integration Test + TestContainers](#51-integration-test--testcontainers)
- [5.2 Code Coverage](#52-code-coverage)
- [5.3 Optimistic Concurrency (RowVersion)](#53-optimistic-concurrency-rowversion)

---

# PHẦN 0 – BỨC TRANH TỔNG THỂ

## 0.1. Dự án gồm những gì

**Hiểu đơn giản:** hãy tưởng tượng dự án như một **nhà hàng**:

| Nhà hàng | Dự án |
|---|---|
| Khách hàng gọi món | Người dùng thao tác trên **Angular** |
| Phục vụ nhận order, mang món ra | **Controller** nhận request, trả response |
| Đầu bếp nấu món theo công thức | **Service** xử lý nghiệp vụ |
| Kho nguyên liệu | **SQL Server** (database) |
| Thủ kho lấy/cất nguyên liệu | **AppDbContext** (EF Core) |
| Bảo vệ kiểm tra thẻ ở cửa | **JWT + Authorize** |
| Quản lý xử lý khi có sự cố | **ExceptionMiddleware** |
| Camera ghi lại ai làm gì | **Audit Interceptor** |

## 0.2. `Program.cs` – "bảng điều khiển" của backend

`Program.cs` chạy **đầu tiên** khi khởi động API. Nó làm 2 việc: **đăng ký dịch vụ** (phần `builder.Services...`) và **sắp xếp đường đi của request** (phần `app.Use...`).

```csharp
var builder = WebApplication.CreateBuilder(args);
```
→ Tạo "người xây dựng" ứng dụng. Nó tự đọc `appsettings.json`, biến môi trường... **Vì sao:** mọi ứng dụng ASP.NET đều bắt đầu từ đây.

```csharp
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => { ... return new BadRequestObjectResult(new ErrorResponse {...}); };
        options.SuppressMapClientErrors = true;
    });
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `AddControllers()` | Bật tính năng Controller | Không có thì các `[HttpGet]`, `[HttpPost]` không hoạt động |
| `InvalidModelStateResponseFactory` | Khi dữ liệu gửi lên sai (ví dụ tuổi = 10), tạo response theo ý mình | Mặc định ASP.NET trả lỗi tiếng Anh, định dạng khác. Mình muốn **cùng định dạng** `{statusCode, message}` để Angular đọc dễ |
| `SuppressMapClientErrors = true` | Tắt việc ASP.NET tự sinh lỗi kiểu "ProblemDetails" | Để không bị trộn 2 kiểu định dạng lỗi |

```csharp
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
```
→ Đọc khóa bí mật từ `appsettings.json` rồi đổi thành mảng byte. **Vì sao:** thuật toán ký JWT cần khóa dạng byte; đọc từ cấu hình để không viết cứng trong code.

```csharp
builder.Services.AddHttpContextAccessor();
```
→ Cho phép các lớp **không phải Controller** (như Interceptor) lấy được thông tin request hiện tại. **Vì sao:** Audit Interceptor cần biết **ai** đang thao tác.

```csharp
builder.Services.AddAuthentication(...).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"], ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});
```
| Dòng | Kiểm tra gì | Nếu tắt thì sao |
|---|---|---|
| `ValidateIssuer` | Token có do **chính server này** phát hành? | Token của hệ thống khác cũng lọt vào |
| `ValidateAudience` | Token có **dành cho** ứng dụng này? | Token cho app khác dùng được ở đây |
| `ValidateLifetime` | Token **còn hạn**? | Token hết hạn vẫn dùng được mãi |
| `ValidateIssuerSigningKey` | **Chữ ký** có đúng khóa bí mật? | Ai cũng tự tạo token giả được — **nguy hiểm nhất** |

```csharp
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
});
builder.Services.AddScoped<SinhVienService>();
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `AddScoped<AuditSaveChangesInterceptor>()` | Đăng ký Interceptor, mỗi request một bản | Interceptor dùng `IHttpContextAccessor` của từng request |
| `UseSqlServer(...)` | Dùng SQL Server với chuỗi kết nối trong cấu hình | Nói cho EF biết kết nối DB nào |
| `AddInterceptors(...)` | Gắn Interceptor vào DbContext | Mỗi lần `SaveChanges` sẽ tự ghi Audit |
| `AddScoped<SinhVienService>()` | Đăng ký Service | Controller nhận Service qua constructor (Dependency Injection) |

**Scoped là gì?** "Mỗi request một bản". Request A và request B mỗi cái có DbContext riêng, không dùng chung → không bị lẫn dữ liệu.

```csharp
builder.Services.AddCors(o => o.AddPolicy("AllowAngular",
    p => p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
```
→ Cho phép trang web ở `localhost:4200` gọi API. **Vì sao:** trình duyệt **chặn** web gọi sang địa chỉ khác (khác cổng cũng tính là khác). Không có dòng này, Angular gọi API sẽ bị lỗi CORS.

**Thứ tự pipeline (rất quan trọng):**
```csharp
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();   // ① đứng đầu: bắt mọi lỗi phía sau
app.UseStatusCodePages(...);                // ② 401/403/404 rỗng → JSON
app.UseHttpsRedirection();                  // ③ chuyển http → https
app.UseCors("AllowAngular");                // ④ cho Angular gọi
app.UseStaticFiles();                       // ⑤ phục vụ ảnh trong wwwroot
app.UseAuthentication();                    // ⑥ đọc token: "bạn là ai?"
app.UseAuthorization();                     // ⑦ kiểm tra quyền: "bạn được làm không?"
app.MapControllers();                       // ⑧ chuyển tới Controller
app.Run();

public partial class Program { }            // cho project test nhìn thấy Program
```
**Ví von:** request đi qua các "trạm kiểm soát" theo đúng thứ tự từ trên xuống. ExceptionMiddleware giống **lưới an toàn** căng ở dưới cùng: đặt đầu tiên để hứng được mọi thứ rơi xuống từ các trạm phía sau. Authentication (hỏi "bạn là ai") phải trước Authorization (hỏi "bạn được làm gì"), vì chưa biết là ai thì không thể xét quyền.

## 0.3. `app.config.ts` – "bảng điều khiển" của Angular

```typescript
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor]))
  ]
};
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `provideRouter(routes)` | Bật điều hướng trang (`/login`, `/sinh-vien`...) | Ứng dụng có nhiều trang |
| `provideHttpClient(...)` | Bật `HttpClient` để gọi API | Không có thì không gọi được backend |
| `withInterceptors([jwt, error])` | Gắn 2 "trạm" vào mọi request | `jwtInterceptor` gắn token, `errorInterceptor` bắt lỗi — viết **một lần**, dùng cho **mọi** request |

---

# TUẦN 1 – NỀN TẢNG

## 1.1. DTO – "phiếu thông tin rút gọn"

**Hiểu đơn giản:** hồ sơ sinh viên trong kho (Entity) có cả những thông tin nội bộ như "đã xóa chưa". Khi đưa cho khách xem, mình chỉ photo **những trang cần thiết** (DTO), không đưa cả hồ sơ gốc.

**Entity – `Models/SinhVien.cs`** (ánh xạ đúng bảng trong DB):
```csharp
public class SinhVien
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Tuoi { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsDeleted { get; set; } = false;     // nội bộ, không cho ra ngoài
    [Timestamp] public byte[]? RowVersion { get; set; }
}
```

**DTO – `DTOs/SinhVienDto.cs`** (dữ liệu gửi qua lại với Angular):
```csharp
public class SinhVienDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Họ và tên không được để trống!")]
    public string HoTen { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email không được để trống!")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng!")]
    public string Email { get; set; } = string.Empty;
    [Range(18, 99, ErrorMessage = "Tuổi phải là số dương từ 18 đến 99!")]
    public int Tuoi { get; set; }
    public string? AvatarUrl { get; set; }
    public byte[]? RowVersion { get; set; }
}
```

| Điểm | Giải thích |
|---|---|
| DTO **không có** `IsDeleted` | Người dùng không thấy và **không gửi lên để sửa** được trường này |
| `= string.Empty` | Gán sẵn chuỗi rỗng để không bị `null`, tránh lỗi khi dùng `.ToLower()` |
| `string?` (có dấu `?`) | Cho phép null, ví dụ sinh viên chưa có ảnh |

**Nếu không dùng DTO:** client có thể gửi `{"id":1, "hoTen":"A", "isDeleted": true}` → tự xóa sinh viên mà không cần quyền. Lỗi này gọi là **over-posting**.

**Chuyển Entity → DTO:**
```csharp
private static SinhVienDto ToDto(SinhVien s) => new()
{
    Id = s.Id, HoTen = s.HoTen, Email = s.Email, Tuoi = s.Tuoi,
    AvatarUrl = s.AvatarUrl, RowVersion = s.RowVersion
};
```
→ Hàm nhỏ "chép" từng trường cần thiết. `static` vì không cần dùng dữ liệu nào của class. Viết thành hàm riêng để **không lặp code** ở GetById, Create.

## 1.2. Validation – kiểm tra dữ liệu đầu vào

**Hiểu đơn giản:** như nhân viên lễ tân kiểm tra phiếu đăng ký: thiếu tên, email sai định dạng, tuổi không hợp lệ thì trả phiếu lại ngay, **không chuyển vào bên trong**.

| Attribute | Kiểm tra | Câu báo lỗi |
|---|---|---|
| `[Required]` | Không được để trống | "Họ và tên không được để trống!" |
| `[EmailAddress]` | Đúng dạng `abc@xyz.com` | "Email không đúng định dạng!" |
| `[Range(18, 99)]` | Tuổi từ 18 đến 99 | "Tuổi phải là số dương từ 18 đến 99!" |

**Ai kiểm tra?** Thuộc tính `[ApiController]` trên Controller. Nó tự kiểm tra **trước khi** vào hàm `Create`/`Update`. Sai thì gọi `InvalidModelStateResponseFactory` (trong `Program.cs`) để trả về:
```json
{ "statusCode": 400, "message": "Tuổi phải là số dương từ 18 đến 99!", "errors": { "Tuoi": ["Tuổi phải là..."] } }
```
**Vì sao làm ở backend dù Angular có thể kiểm tra?** Vì người ta có thể gọi API bằng Postman, bỏ qua Angular. **Backend là chốt chặn cuối cùng.**

## 1.3. Hash mật khẩu bằng BCrypt

**Hiểu đơn giản:** hash giống **máy xay sinh tố**: cho trái cây vào ra nước ép, nhưng **không thể** từ nước ép ghép lại thành trái cây. Muốn kiểm tra mật khẩu, mình xay mật khẩu người dùng nhập rồi so 2 ly nước ép.

**Đăng ký – `XacThucController.DangKy`:**
```csharp
[HttpPost("dangky")]
public async Task<IActionResult> DangKy(DangKyDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        throw new BadRequestException("Tài khoản và mật khẩu không được để trống!");

    if (await _context.NguoiDung.AnyAsync(u => u.Username == dto.Username))
        throw new ConflictException("Tài khoản đã tồn tại!");

    var nguoiDung = new NguoiDung
    {
        Username = dto.Username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        FullName = dto.FullName
    };
    _context.NguoiDung.Add(nguoiDung);
    await _context.SaveChangesAsync();
    return Ok(new { message = "Đăng ký thành công!" });
}
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `IsNullOrWhiteSpace(...)` | Kiểm tra rỗng hoặc toàn dấu cách | Không cho tạo tài khoản trống |
| `AnyAsync(u => u.Username == ...)` | Hỏi DB "đã có username này chưa?" | `AnyAsync` chỉ trả true/false, nhanh hơn lấy cả bản ghi |
| `throw new ConflictException(...)` | Ném lỗi 409 | Middleware sẽ đổi thành JSON, Controller không phải tự viết response |
| `BCrypt.HashPassword(dto.Password)` | Băm mật khẩu, **tự thêm salt ngẫu nhiên** | Không bao giờ lưu mật khẩu gốc. Salt làm 2 người cùng mật khẩu ra 2 chuỗi khác nhau |
| Không gán `Role` | Mặc định `Role = "GiangVien"` (trong Model) | Người dùng **không được tự chọn** làm Admin |

**Đăng nhập – `XacThucController.DangNhap`:**
```csharp
var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
if (nguoiDung == null) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, nguoiDung.PasswordHash);
if (!isPasswordCorrect) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

var token = GenerateJwtToken(nguoiDung);
return Ok(new { Token = token, FullName = nguoiDung.FullName, Role = nguoiDung.Role });
```
| Dòng | Vì sao |
|---|---|
| `FirstOrDefaultAsync` | Lấy người dùng đầu tiên khớp, không có thì trả `null` (không ném lỗi) |
| `BCrypt.Verify(...)` | Băm mật khẩu vừa nhập bằng salt đã lưu rồi so sánh |
| **Cùng một câu báo lỗi** cho 2 trường hợp | Không cho kẻ xấu biết tài khoản nào có thật |

## 1.4. JWT – tạo token khi đăng nhập

**Hiểu đơn giản:** JWT giống **vé xem phim có đóng dấu**. Vé ghi tên bạn, loại ghế (Role), giờ hết hạn. Con dấu (chữ ký) chỉ rạp có. Bạn sửa chữ trên vé thì con dấu không còn khớp → bị từ chối.

```csharp
private string GenerateJwtToken(NguoiDung nguoiDung)
{
    var jwtSettings = _configuration.GetSection("JwtSettings");
    var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, nguoiDung.Id.ToString()),
        new Claim(ClaimTypes.Name, nguoiDung.Username),
        new Claim(ClaimTypes.Role, nguoiDung.Role),
        new Claim("FullName", nguoiDung.FullName)
    };

    var key = new SymmetricSecurityKey(secretKey);
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtSettings["Issuer"], audience: jwtSettings["Audience"],
        claims: claims, expires: DateTime.Now.AddHours(2), signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `claims` | Các thông tin ghi trên "vé" | `Name` để Audit biết ai thao tác, `Role` để phân quyền, `FullName` để hiển thị |
| `ClaimTypes.Role` | Claim vai trò chuẩn của .NET | `[Authorize(Roles="Admin")]` đọc đúng claim này |
| `SymmetricSecurityKey` | Khóa bí mật dùng để ký | Cùng một khóa để **ký** và **kiểm tra** |
| `HmacSha256` | Thuật toán ký | Thuật toán phổ biến, an toàn |
| `expires: AddHours(2)` | Hết hạn sau 2 giờ | Lỡ token bị lộ thì cũng chỉ dùng được thời gian ngắn |
| `WriteToken` | Đổi object thành chuỗi `xxx.yyy.zzz` | Để gửi về cho Angular |

**Lưu ý quan trọng:** phần thông tin (payload) chỉ **mã hóa base64**, ai cũng đọc được → **không bao giờ** đưa mật khẩu vào token.

## 1.5. JWT – Angular lưu và gửi token

**`services/auth.ts` – lưu token sau khi đăng nhập:**
```typescript
login(credentials: any): Observable<any> {
  return this.http.post<any>(`${this.apiUrl}/dangnhap`, credentials).pipe(
    tap(res => {
      if (res && res.token && this.isBrowser) {
        localStorage.setItem('token', res.token);
        localStorage.setItem('fullName', res.fullName);
        localStorage.setItem('role', res.role);
      }
    })
  );
}
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `.pipe(tap(...))` | "Nhìn trộm" kết quả trước khi trả cho component | Lưu token ngay trong service, component chỉ việc chuyển trang |
| `this.isBrowser` | Chỉ chạy khi ở trình duyệt | Dự án có SSR (render ở server), mà server **không có** `localStorage` |
| `localStorage.setItem` | Lưu vào bộ nhớ trình duyệt | Tải lại trang (F5) vẫn còn đăng nhập |

**`interceptors/jwt.ts` – tự gắn token vào mọi request:**
```typescript
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
    const token = localStorage.getItem('token');
    if (token) {
      const clonedReq = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      return next(clonedReq);
    }
  }
  return next(req);
};
```
| Dòng | Vì sao |
|---|---|
| `typeof window !== 'undefined'` | Tránh lỗi khi chạy ở server (SSR) |
| `req.clone(...)` | Request trong Angular **không sửa trực tiếp được** (bất biến), phải tạo bản sao rồi thêm header |
| `Bearer ${token}` | Định dạng chuẩn mà `AddJwtBearer` ở backend mong đợi |
| `return next(req)` | Chưa đăng nhập thì gửi request gốc (ví dụ gọi API đăng nhập) |

**Nếu không có interceptor:** phải tự gắn header ở **từng** hàm `getAll`, `create`, `update`... dễ quên và lặp code.

**`getRole()` – đọc vai trò từ token (chống sửa Local Storage):**
```typescript
getRole(): string {
  const token = localStorage.getItem('token');
  if (!token) return '';
  const payloadBase64 = token.split('.')[1];                               // lấy phần giữa của token
  const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
  const payload = JSON.parse(decodeURIComponent(escape(decodedJson)));     // hỗ trợ tiếng Việt
  const tokenRole = payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';

  const localRole = localStorage.getItem('role');
  if (localRole && tokenRole && localRole !== tokenRole) {                 // phát hiện bị sửa
    localStorage.setItem('role', tokenRole);
    this.toastService.showError('Không thể truy cập dưới quyền Admin.');
  }
  return tokenRole;
}
```
| Dòng | Vì sao |
|---|---|
| `token.split('.')[1]` | JWT có 3 phần ngăn bởi dấu chấm; phần giữa chứa thông tin |
| `replace(/-/g,'+')...` | JWT dùng base64 "an toàn cho URL" (dùng `-` và `_`), phải đổi lại trước khi `atob` giải mã |
| `decodeURIComponent(escape(...))` | Giải mã đúng ký tự tiếng Việt trong `FullName` |
| Hai tên claim role | .NET có thể ghi role bằng tên ngắn `role` hoặc tên đầy đủ dạng URL |
| So sánh `localRole` và `tokenRole` | Người dùng sửa `role` trong Local Storage thì bị phát hiện và trả lại role thật |

## 1.6. Xử lý lỗi đăng nhập và mất kết nối

**`utils/error-message.ts`:**
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
| Trường hợp | Vì sao kiểm tra |
|---|---|
| `status === 0` | Trình duyệt không kết nối được server (server tắt, sai địa chỉ) → không có mã HTTP |
| `body.message` | Lỗi theo định dạng chuẩn của mình |
| `typeof err.error === 'string'` | Phòng trường hợp server trả lỗi dạng chữ thường |
| Câu mặc định | Không bao giờ để người dùng thấy thông báo trống |

**Vì sao viết thành hàm riêng?** Trang đăng nhập, đăng ký và interceptor đều cần đọc lỗi → viết **một lần**, dùng nhiều nơi.

**`login.ts` – phần xử lý lỗi:**
```typescript
error: (err) => {
  this.errorMessage = layThongBaoLoi(err);
}
```
→ Gán câu lỗi vào biến, HTML hiển thị biến đó. Dự án chạy **zoneless** (Angular không tự theo dõi mọi thay đổi), nên ở các chỗ cần cập nhật ngay em gọi thêm `this.cdr.detectChanges()` để giao diện hiện thông báo ở **lần bấm đầu tiên**.

---

# TUẦN 2 – TÍNH NĂNG CHÍNH

## 2.1. Phân trang, tìm kiếm, sắp xếp

**Hiểu đơn giản:** thư viện có 10.000 cuốn sách. Bạn hỏi "cho tôi xem 5 cuốn tiếp theo có chữ *An*, xếp theo tên". Thủ thư **lọc và đếm ở trong kho** rồi chỉ mang ra 5 cuốn — không khuân hết 10.000 cuốn ra bàn để bạn tự lọc.

**Tham số – `DTOs/SinhVienQuery.cs`:**
```csharp
public class SinhVienQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsDescending { get; set; } = false;
}
```
→ Gom 5 tham số trên URL vào một object. `[FromQuery]` ở Controller báo ASP.NET lấy giá trị từ `?pageNumber=2&pageSize=5...`. Giá trị mặc định dùng khi Angular không gửi.

**Xử lý – `SinhVienService.GetAllAsync`:**
```csharp
if (query.PageNumber < 1) query.PageNumber = 1;
if (query.PageSize < 1 || query.PageSize > 50) query.PageSize = 5;
```
→ **Chặn giá trị xấu.** `PageNumber = 0` sẽ làm `Skip(-5)` lỗi; `PageSize = 100000` sẽ kéo cả bảng làm nặng server.

```csharp
var queryable = _context.SinhVien.AsNoTracking();
```
| Phần | Ý nghĩa |
|---|---|
| `_context.SinhVien` | Bảng sinh viên, kiểu `IQueryable` — **chưa chạy SQL** |
| `AsNoTracking()` | Chỉ đọc, không cần EF theo dõi để sửa → nhanh hơn, nhẹ hơn |

```csharp
if (!string.IsNullOrWhiteSpace(query.Keyword))
{
    var keyword = query.Keyword.Trim().ToLower();
    queryable = queryable.Where(s => s.HoTen.ToLower().Contains(keyword) || s.Email.ToLower().Contains(keyword));
}
```
| Phần | Vì sao |
|---|---|
| `if (...)` | Không nhập từ khóa thì không lọc |
| `Trim()` | Bỏ dấu cách thừa người dùng gõ nhầm |
| `ToLower()` cả hai vế | Tìm "AN", "an", "An" đều ra |
| `Contains` | Tìm chứa chuỗi, SQL dịch thành `LIKE '%an%'` |
| `queryable = queryable.Where(...)` | **Nối thêm** điều kiện, vẫn chưa chạy SQL |

```csharp
queryable = (query.SortBy?.Trim().ToLower(), query.IsDescending) switch
{
    ("hoten", false) => queryable.OrderBy(s => s.HoTen),
    ("hoten", true)  => queryable.OrderByDescending(s => s.HoTen),
    ("email", false) => queryable.OrderBy(s => s.Email),
    ("email", true)  => queryable.OrderByDescending(s => s.Email),
    ("tuoi", false)  => queryable.OrderBy(s => s.Tuoi),
    ("tuoi", true)   => queryable.OrderByDescending(s => s.Tuoi),
    (_, true)        => queryable.OrderByDescending(s => s.Id),
    _                => queryable.OrderBy(s => s.Id)
};
```
| Phần | Vì sao |
|---|---|
| `(cột, chiều) switch` | Xét **cùng lúc** 2 giá trị, gọn hơn nhiều `if-else` lồng nhau |
| `SortBy?.` (dấu `?.`) | Nếu `SortBy` là null thì không lỗi, trả null luôn |
| `_` | "Mọi trường hợp còn lại" |
| Luôn có `OrderBy` | SQL Server **bắt buộc** có sắp xếp khi dùng `OFFSET/FETCH` để phân trang |
| Không dùng tên cột người dùng gửi trực tiếp | Chỉ chấp nhận các cột cho phép → an toàn |

```csharp
var totalCount = await queryable.CountAsync();
```
→ **Lần chạy SQL thứ nhất:** `SELECT COUNT(*) ... WHERE ...`. **Vì sao đếm trước khi Skip/Take:** cần tổng số dòng **khớp điều kiện** để tính tổng số trang.

```csharp
var items = await queryable
    .Skip((query.PageNumber - 1) * query.PageSize)
    .Take(query.PageSize)
    .Select(s => new SinhVienDto { Id = s.Id, HoTen = s.HoTen, ... })
    .ToListAsync();
```
| Phần | Ví dụ trang 2, mỗi trang 5 | SQL |
|---|---|---|
| `Skip((2-1)*5)` | Bỏ qua 5 dòng đầu | `OFFSET 5 ROWS` |
| `Take(5)` | Lấy 5 dòng | `FETCH NEXT 5 ROWS ONLY` |
| `Select(...)` | Chỉ lấy các cột của DTO | `SELECT Id, HoTen, ...` |
| `ToListAsync()` | **Lần chạy SQL thứ hai**, lấy kết quả thật | |

**Khái niệm "Deferred execution" (thực thi trì hoãn):** từ đầu tới đây mình chỉ **viết công thức** (Where, OrderBy, Skip, Take), SQL chỉ thực sự chạy ở `CountAsync` và `ToListAsync`. Nhờ vậy tất cả điều kiện được gộp vào **một câu SQL**, DB làm hết việc nặng.

**Kết quả – `DTOs/PagedResult.cs`:**
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
```
| Phần | Vì sao |
|---|---|
| `<T>` (generic) | Dùng lại được cho bất kỳ loại dữ liệu nào (sinh viên, lịch sử...) |
| `TotalPages =>` | Thuộc tính **tự tính**, không cần gán |
| `(double)` + `Math.Ceiling` | 12 dòng / 5 = 2.4 → làm tròn **lên** thành 3 trang. Không ép `double` thì `12/5 = 2` (chia nguyên) sẽ thiếu trang |

**Phía Angular – `sinh-vien.ts`:**
```typescript
taiDanhSach(): void {
  this.sinhVienService.getAll(this.trangHienTai, this.soDongMoiTrang, this.tuKhoaTimKiem, this.sapXepTheo, this.sapXepGiamDan)
    .subscribe({
      next: (res) => {
        this.danhSachSinhVien = res.items;
        this.tongSoDong = res.totalCount;
        this.tongSoTrang = res.totalPages;
        this.cdr.detectChanges();
      }
    });
}
thayDoiSapXep(cot: string): void {
  if (this.sapXepTheo === cot) this.sapXepGiamDan = !this.sapXepGiamDan;  // bấm lại cột cũ → đảo chiều
  else { this.sapXepTheo = cot; this.sapXepGiamDan = false; }            // cột mới → tăng dần
  this.taiDanhSach();
}
timKiem(): void { this.trangHienTai = 1; this.taiDanhSach(); }          // tìm mới → về trang 1
```
**Vì sao `timKiem` đưa về trang 1?** Đang ở trang 3 mà tìm từ khóa chỉ có 2 kết quả → trang 3 sẽ trống.

## 2.2. Phân quyền 3 lớp

**Hiểu đơn giản:** một tòa nhà có 3 lớp bảo vệ:
1. **Biển báo** "Khu vực nhân viên" (ẩn nút) → người ngay thẳng không vào, nhưng không ngăn được ai.
2. **Bảo vệ ở hành lang** (Route Guard) → chặn người đi lạc, nhưng có thể đi đường khác.
3. **Khóa vân tay ở cửa phòng** (API) → **bảo mật thật**, không có quyền thì không mở được.

**Lớp 1 – Ẩn nút (`sinh-vien.html`):**
```html
<div class="form-box" *ngIf="authService.hasRole('Admin')"> ... form thêm/sửa ... </div>
<td *ngIf="authService.hasRole('Admin')">
  <button (click)="sua(sv)">Sửa</button>
  <button (click)="xoa(sv.id)">Xóa</button>
</td>
```
→ `*ngIf` = điều kiện sai thì phần HTML đó **không được tạo ra**. Giảng viên không thấy form và nút.

**Lớp 2 – Route Guard (`guards/role.ts`):**
```typescript
export const roleGuard = (allowedRoles: string[]): CanActivateFn => {
  return () => {
    if (!isPlatformBrowser(inject(PLATFORM_ID))) return true;   // SSR: để trình duyệt kiểm tra
    const authService = inject(AuthService);
    const router = inject(Router);
    if (!authService.isLoggedIn()) return router.parseUrl('/login');
    if (allowedRoles.includes(authService.getRole())) return true;
    inject(ToastService).showError('Không thể truy cập dưới quyền Admin.');
    return router.parseUrl('/sinh-vien');
  };
};
```
| Phần | Vì sao |
|---|---|
| `(allowedRoles) => CanActivateFn` | Hàm "tạo guard": dùng lại cho nhiều trang với danh sách quyền khác nhau |
| `isPlatformBrowser` | Ở server không có Local Storage, nên bỏ qua và để trình duyệt kiểm tra |
| `return true` | Cho vào trang |
| `router.parseUrl('/login')` | Trả về **UrlTree** = "đừng vào trang này, chuyển sang trang kia". Cách chuẩn của Angular, tránh điều hướng chồng chéo |

**Dùng trong `app.routes.ts`:**
```typescript
{ path: 'lich-su', component: LichSuComponent, canActivate: [roleGuard(['Admin'])] },
```

**Lớp 3 – API (bảo mật thật) – `SinhVienController`:**
```csharp
[Authorize]                                   // trên class: mọi API đều phải đăng nhập
public class SinhVienController : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]              // chỉ Admin được thêm
    public async Task<ActionResult<SinhVienDto>> Create(...)

    [HttpPost("upload-avatar/{id}")]
    [Authorize(Roles = "Admin,GiangVien")]    // dấu phẩy = "hoặc"
    public async Task<IActionResult> UploadAvatar(...)
}
```
| Kết quả | Khi nào |
|---|---|
| **401 Unauthorized** | Không có token, token sai chữ ký hoặc hết hạn |
| **403 Forbidden** | Token hợp lệ nhưng **không đủ quyền** (Giảng viên gọi Create) |

**Vì sao cần cả 3 lớp?** Lớp 1, 2 giúp **giao diện gọn và thân thiện**. Chỉ lớp 3 là **bảo mật thật**, vì người dùng có thể dùng Postman gọi thẳng API mà bỏ qua Angular.

## 2.3. Upload ảnh đại diện

**Hiểu đơn giản:** như nhận hàng gửi kho: kiểm tra người nhận có tồn tại không → kiện hàng có rỗng không → đúng loại hàng không → không quá cân → dán mã số riêng → cất vào kho → ghi sổ.

**Backend – `SinhVienController.UploadAvatar`:**
```csharp
var sinhVien = await _context.SinhVien.FindAsync(id);
if (sinhVien == null) throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
if (file == null || file.Length == 0) throw new BadRequestException("Vui lòng chọn một file ảnh!");
```
→ Kiểm tra từ dễ đến khó, sai thì dừng ngay (**fail fast**). `IFormFile` là kiểu .NET dùng để nhận file upload.

```csharp
var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
var extension = Path.GetExtension(file.FileName).ToLower();
if (!allowedExtensions.Contains(extension)) throw new BadRequestException("Định dạng file không hợp lệ!...");
if (file.Length > 2 * 1024 * 1024) throw new BadRequestException("Dung lượng file quá lớn!...");
```
| Dòng | Vì sao |
|---|---|
| Danh sách đuôi cho phép (whitelist) | Chỉ nhận đúng loại mình muốn, an toàn hơn liệt kê loại cấm |
| `.ToLower()` | `ẢNH.JPG` và `ảnh.jpg` đều hợp lệ |
| `2 * 1024 * 1024` | 2 MB tính bằng byte (1 KB = 1024 byte), viết như vậy dễ đọc hơn `2097152` |

```csharp
var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
var uniqueFileName = $"{Guid.NewGuid()}{extension}";
var filePath = Path.Combine(uploadsFolder, uniqueFileName);
using (var stream = new FileStream(filePath, FileMode.Create))
{
    await file.CopyToAsync(stream);
}
```
| Dòng | Vì sao |
|---|---|
| `_env.WebRootPath` | Đường dẫn tuyệt đối tới `wwwroot` trên máy chủ, không viết cứng `D:\...` |
| `Path.Combine` | Tự nối đường dẫn đúng dấu `\` hay `/` theo hệ điều hành |
| `CreateDirectory` | Lần đầu chưa có thư mục thì tạo |
| `Guid.NewGuid()` | Tên ngẫu nhiên không trùng: 2 người cùng upload `avatar.jpg` không đè nhau, không đoán được tên file |
| `using (...)` | Tự **đóng file** sau khi ghi xong, kể cả khi có lỗi. Không đóng thì file bị khóa |
| `CopyToAsync` | Ghi dữ liệu file vào ổ cứng, bất đồng bộ |

**Frontend – `services/sinh-vien.ts`:**
```typescript
uploadAvatar(id: number, file: File): Observable<any> {
  const formData = new FormData();
  formData.append('file', file);
  return this.http.post(`${this.apiUrl}/upload-avatar/${id}`, formData, {
    reportProgress: true,
    observe: 'events'
  });
}
```
| Dòng | Vì sao |
|---|---|
| `FormData` | File phải gửi dạng `multipart/form-data`, không gửi được như JSON |
| `append('file', file)` | Tên `'file'` phải **trùng** tên tham số `IFormFile file` ở backend |
| `reportProgress: true` | Bật báo cáo tiến trình |
| `observe: 'events'` | Nhận **mọi sự kiện** (đang gửi, xong...) chứ không chỉ kết quả cuối |

**Component – tính phần trăm:**
```typescript
next: (event: any) => {
  if (event.type === HttpEventType.UploadProgress && event.total) {
    this.tienTrinhUpload = Math.round(100 * event.loaded / event.total);
    this.cdr.detectChanges();
  } else if (event.type === HttpEventType.Response) {
    this.toastService.showSuccess('Tải ảnh đại diện lên thành công!');
    this.taiDanhSach();
  }
}
```
→ `loaded` = số byte đã gửi, `total` = tổng số byte. `detectChanges()` để thanh % cập nhật liên tục (zoneless).

## 2.4. Audit Log – API xem lịch sử

**`Controllers/AuditLogController.cs`:**
```csharp
[Authorize(Roles = "Admin")]
public class AuditLogController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Select(l => new AuditLogDto { Id = l.Id, Username = l.Username, Action = l.Action, ... })
            .ToListAsync();
        return Ok(logs);
    }
}
```
| Dòng | Vì sao |
|---|---|
| `[Authorize(Roles = "Admin")]` trên class | Nhật ký là thông tin nhạy cảm, **chỉ Admin** được xem |
| `AsNoTracking()` | Chỉ đọc |
| `OrderByDescending(Timestamp)` | Việc mới nhất hiện lên đầu |
| `Select(... AuditLogDto ...)` | Vẫn dùng DTO, không trả Entity |

Việc **ghi** log nằm ở Interceptor (xem 3.3).

## 2.5. Chuẩn hóa lỗi + Toast

**Định dạng lỗi chung – `DTOs/ErrorResponse.cs`:**
```csharp
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Errors { get; set; }
}
```
| Thuộc tính | Vì sao |
|---|---|
| `StatusCode` | Mã lỗi để Angular biết loại lỗi |
| `Message` | Câu tiếng Việt để hiện cho người dùng |
| `Details` | Chi tiết kỹ thuật, **chỉ có ở môi trường Dev** |
| `Errors` + `JsonIgnore(WhenWritingNull)` | Lỗi từng trường (chỉ có khi validation). Không có thì **không xuất hiện** trong JSON cho gọn |

**`interceptors/error.ts` – bắt mọi lỗi HTTP:**
```typescript
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (typeof window === 'undefined') return throwError(() => error);

      const apiError = error.error as ApiError | null;
      if (apiError?.details) console.error('[API details]', apiError.details);

      const laApiXacThuc = req.url.includes('/api/XacThuc/');
      if (!laApiXacThuc) toastService.showError(layThongBaoLoi(error));

      if (error.status === 401 && !laApiXacThuc) {
        authService.logout();
        router.navigate(['/login']);
      } else if (error.status === 403) {
        router.navigate(['/sinh-vien']);
      }
      return throwError(() => error);
    })
  );
};
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `inject(...)` | Lấy các service cần dùng | Interceptor dạng hàm không có constructor, dùng `inject` thay thế |
| `next(req).pipe(catchError(...))` | Gửi request, nếu lỗi thì chạy hàm xử lý | Bắt lỗi ở **một nơi** cho mọi request |
| `typeof window === 'undefined'` | Đang chạy ở server thì bỏ qua | Server không hiện toast, không điều hướng được |
| `console.error(details)` | In stack trace ra Console | Hỗ trợ lập trình viên debug ở môi trường Dev |
| `laApiXacThuc` | Lỗi của API đăng nhập/đăng ký thì không toast | Hai trang đó đã tự hiện thông báo, tránh **báo 2 lần** |
| `401` → `logout()` + về `/login` | Token hết hạn hoặc không hợp lệ | Xóa token cũ, bắt đăng nhập lại |
| `403` → về `/sinh-vien` | Không đủ quyền | Đưa về trang được phép xem |
| `return throwError(() => error)` | Ném lỗi tiếp cho component | Component còn có thể xử lý riêng, ví dụ đóng form khi 409 |

**`services/toast.ts` – hệ thống thông báo:**
```typescript
toastState = new Subject<ToastMessage>();
showError(msg: string) { this.emit({ message: msg, type: 'error' }); }
private emit(toast: ToastMessage) {
  if (this.toastState.observed) {
    this.toastState.next(toast);
  } else if (typeof window !== 'undefined') {
    sessionStorage.setItem('flashToast', JSON.stringify(toast));
  }
}
```
| Dòng | Vì sao |
|---|---|
| `Subject` | Như **loa phát thanh**: service phát, ToastComponent nghe và hiển thị. Bất kỳ đâu gọi `showError` đều được |
| `observed` | Kiểm tra có ai đang nghe không |
| `sessionStorage` | Nếu đang chuyển trang chưa có ai nghe, lưu tạm để trang mới hiện ra (ví dụ thông báo trước khi bị đẩy về `/login`) |

**Vì sao dùng Toast thay `alert()`?** `alert()` chặn cả trang, xấu, phải bấm OK. Toast tự biến mất, không cản thao tác.

---

# TUẦN 3-4 – EXCEPTION, AUDIT, QUẢN LÝ FILE

## 3.1. Exception tự định nghĩa

**Hiểu đơn giản:** thay vì mỗi phòng tự viết thư phàn nàn theo kiểu riêng, công ty in sẵn **các mẫu phiếu**: "Không tìm thấy", "Bị trùng", "Sai dữ liệu". Ai gặp vấn đề chỉ cần chọn đúng phiếu nộp lên, phòng quản lý xử lý theo mẫu.

**`Exceptions/AppException.cs`:**
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
| Phần | Vì sao |
|---|---|
| `abstract` | Không cho tạo trực tiếp `AppException`, bắt buộc dùng loại cụ thể |
| `: Exception` | Kế thừa exception chuẩn của .NET nên `throw` / `catch` được |
| `StatusCode { get; }` | Chỉ đọc: gán một lần lúc tạo, không ai sửa được |
| `protected` constructor | Chỉ lớp con được gọi |
| `: base(message)` | Đưa câu thông báo lên lớp cha để `ex.Message` dùng được |
| Mỗi lớp con gắn sẵn mã | Ném `NotFoundException` là **tự hiểu** 404, không phải nhớ số |

**Cách dùng ở Service:**
```csharp
throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
```
→ Service **không cần biết gì về HTTP**, chỉ cần ném đúng loại lỗi.

## 3.2. ExceptionMiddleware

**Hiểu đơn giản:** như **tấm lưới an toàn** dưới gánh xiếc. Diễn viên (Controller, Service) cứ biểu diễn; lỡ ngã (throw exception) thì lưới hứng lại, không ai bị thương (người dùng nhận thông báo lịch sự thay vì trang lỗi).

**`Middleware/ExceptionMiddleware.cs`:**
```csharp
private readonly RequestDelegate _next;
private readonly ILogger<ExceptionMiddleware> _logger;
private readonly IHostEnvironment _env;
```
| Biến | Dùng để |
|---|---|
| `_next` | "Trạm tiếp theo" trong pipeline |
| `_logger` | Ghi log |
| `_env` | Biết đang chạy Development hay Production |

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (Exception ex)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogError(ex, "Lỗi xảy ra sau khi response đã bắt đầu gửi");
            throw;
        }
        if (ex is AppException) _logger.LogWarning("Lỗi nghiệp vụ: {Message}", ex.Message);
        else _logger.LogError(ex, "Một sự cố hệ thống đã xảy ra: {Message}", ex.Message);

        await HandleExceptionAsync(context, ex);
    }
}
```
| Dòng | Làm gì | Vì sao |
|---|---|---|
| `InvokeAsync` | Hàm ASP.NET tự gọi với mỗi request | Quy ước bắt buộc của middleware |
| `try { await _next(context); }` | Cho request đi tiếp qua **toàn bộ** phần phía sau | Lỗi ở bất kỳ đâu phía sau đều rơi vào `catch` |
| `HasStarted` | Response đã bắt đầu gửi về chưa? | Đã gửi một phần thì **không đổi được** mã lỗi nữa, cố ghi sẽ sinh lỗi thứ hai → chỉ ghi log rồi `throw` |
| `throw;` | Ném lại **nguyên** lỗi cũ | Giữ nguyên thông tin lỗi gốc |
| `LogWarning` cho `AppException` | Lỗi do người dùng (4xx) | Không phải sự cố, chỉ cảnh báo |
| `LogError` cho lỗi khác | Lỗi hệ thống (5xx) | Sự cố thật, nổi bật để xử lý |

```csharp
private async Task HandleExceptionAsync(HttpContext context, Exception exception)
{
    context.Response.ContentType = "application/json";
    var response = new ErrorResponse
    {
        Details = _env.IsDevelopment() ? exception.StackTrace?.ToString() : null
    };

    var (statusCode, message) = exception switch
    {
        AppException appEx => (appEx.StatusCode, appEx.Message),
        UnauthorizedAccessException => (401, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ!"),
        ArgumentException or BadHttpRequestException => (400, exception.Message),
        DbUpdateConcurrencyException => (409, "Dữ liệu đã bị thay đổi bởi người khác, vui lòng tải lại!"),
        DbUpdateException => (409, "Dữ liệu bị trùng hoặc vi phạm ràng buộc CSDL!"),
        _ => (500, "Đã xảy ra sự cố hệ thống! Vui lòng liên hệ Admin hoặc thử lại sau.")
    };
    context.Response.StatusCode = statusCode;
    response.StatusCode = statusCode;
    response.Message = message;

    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
}
```
| Dòng | Vì sao |
|---|---|
| `ContentType = "application/json"` | Báo cho trình duyệt nội dung trả về là JSON |
| `IsDevelopment() ? StackTrace : null` | Stack trace lộ cấu trúc code → **chỉ hiện khi đang phát triển** |
| `exception switch { ... }` | **Pattern matching**: xét loại exception để chọn mã và câu thông báo |
| `AppException appEx =>` | Mọi lỗi nghiệp vụ dùng luôn mã và câu đã gắn sẵn |
| `DbUpdateConcurrencyException` đặt **trước** `DbUpdateException` | Nó là **lớp con**; switch xét từ trên xuống, đặt lớp cha trước thì lớp con không bao giờ được bắt riêng |
| `_ => (500, ...)` | Lỗi lạ: trả câu chung chung, **không lộ chi tiết** cho người dùng |
| `CamelCase` | `StatusCode` → `statusCode`, đúng quy ước JavaScript để Angular đọc |

**Hai trường hợp không đi qua middleware này:**

| Trường hợp | Ai xử lý | Vì sao |
|---|---|---|
| Lỗi validation (`[Required]`, `[Range]`) | `InvalidModelStateResponseFactory` | Bị chặn **trước** khi vào Controller, không ném exception |
| 401/403 từ JWT, 404 sai đường dẫn | `UseStatusCodePages` | Chỉ là mã trạng thái, không phải exception |

## 3.3. AuditSaveChangesInterceptor

**Hiểu đơn giản:** như **camera an ninh tự động** ở cửa kho. Nhân viên không cần tự ghi sổ mỗi lần lấy hàng; camera tự ghi lại ai, lấy gì, lúc nào.

**`Data/AuditSaveChangesInterceptor.cs`:**
```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HashSet<string> SensitiveProperties = new() { "PasswordHash", "RowVersion" };
```
| Phần | Vì sao |
|---|---|
| `: SaveChangesInterceptor` | Lớp có sẵn của EF Core cho phép "chen vào" quá trình lưu |
| `IHttpContextAccessor` | Lấy thông tin người dùng của request hiện tại |
| `SensitiveProperties` | Danh sách trường **không ghi vào log**: mật khẩu (bảo mật), RowVersion (dữ liệu kỹ thuật vô nghĩa với người đọc) |
| `HashSet` | Tìm kiếm rất nhanh, không trùng phần tử |
| `static readonly` | Danh sách cố định, dùng chung, không ai sửa |

```csharp
public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
{
    var context = eventData.Context;
    if (context != null) await OnBeforeSaveChanges(context);
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
}
```
| Phần | Vì sao |
|---|---|
| `override SavingChangesAsync` | Chạy **ngay trước** khi dữ liệu được lưu → còn kịp đọc giá trị cũ và thêm dòng log |
| `base.SavingChangesAsync(...)` | Sau khi ghi log, cho EF tiếp tục lưu bình thường |

```csharp
var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
foreach (var entry in context.ChangeTracker.Entries().ToList())
{
    if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
        continue;
```
| Phần | Vì sao |
|---|---|
| `?.` liên tiếp | Mỗi bước có thể null (ví dụ khi test không có HTTP) → không bị lỗi |
| `?? "Anonymous"` | Không biết ai thì ghi "Anonymous" |
| `Identity.Name` | Lấy từ claim `Name` trong JWT (chính là username) |
| `ChangeTracker.Entries()` | Danh sách mọi đối tượng EF đang theo dõi kèm trạng thái |
| `.ToList()` | Chụp lại danh sách trước khi duyệt, vì bên dưới sẽ **thêm** AuditLog vào context (thêm trong lúc duyệt sẽ gây lỗi) |
| Bỏ qua `AuditLog` | Không ghi log cho chính việc ghi log, tránh vòng lặp vô tận |
| Bỏ qua `Unchanged`, `Detached` | Không có thay đổi gì |

```csharp
var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
bool isSoftDelete = isDeletedProp != null && Equals(isDeletedProp.CurrentValue, true) /* ... */;

if (isSoftDelete) auditEntry.Action = "Xóa";
else if (entry.State == EntityState.Added) auditEntry.Action = "Thêm";
else if (entry.State == EntityState.Deleted) auditEntry.Action = "Xóa";
else if (entry.State == EntityState.Modified) auditEntry.Action = "Sửa";
```
→ Xóa mềm về mặt kỹ thuật là **sửa** (UPDATE `IsDeleted`), nên phải kiểm tra `IsDeleted` **trước** để ghi đúng là "Xóa".

```csharp
foreach (var property in entry.Properties)
{
    if (SensitiveProperties.Contains(property.Metadata.Name)) continue;
    switch (entry.State)
    {
        case EntityState.Added:
            auditEntry.NewValues[name] = property.CurrentValue ?? ""; break;
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
| `continue` với trường nhạy cảm | Không ghi mật khẩu vào log |
| Thêm mới → chỉ `NewValues` | Chưa có giá trị cũ |
| `IsModified` | Chỉ ghi **cột nào thật sự đổi**, log gọn và dễ đọc |
| `OriginalValue` / `CurrentValue` | Giá trị **trước** và **sau** khi sửa |

```csharp
foreach (var auditEntry in auditEntries)
    context.Set<AuditLog>().Add(auditEntry.ToAudit());
```
→ Thêm dòng log vào **cùng lần lưu**. Lưu dữ liệu thành công thì log cũng có, lỗi thì cả hai cùng không lưu → luôn khớp nhau.

**`ToAudit()`** đổi Dictionary thành chuỗi JSON (`JsonSerializer.Serialize`) để lưu vào cột `OldValues`, `NewValues`.

**Kết quả:** Controller và Service **không có dòng ghi log nào** mà mọi thao tác vẫn được ghi.

## 3.4. Xóa ảnh cũ khi đổi ảnh

```csharp
var oldAvatarUrl = sinhVien.AvatarUrl;                       // ① nhớ ảnh cũ
sinhVien.AvatarUrl = $"/avatars/{uniqueFileName}";            // ② gán ảnh mới
try
{
    await _context.SaveChangesAsync();                        // ③ lưu DB
}
catch
{
    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);   // lỗi → xóa file MỚI
    throw;
}
if (!string.IsNullOrEmpty(oldAvatarUrl))                      // ④ thành công → xóa file CŨ
{
    var oldAbsoluteFilePath = Path.Combine(_env.WebRootPath, oldAvatarUrl.TrimStart('/'));
    if (System.IO.File.Exists(oldAbsoluteFilePath)) System.IO.File.Delete(oldAbsoluteFilePath);
}
```
| Bước | Vì sao theo thứ tự này |
|---|---|
| ① Nhớ ảnh cũ **trước** khi gán | Gán xong thì mất đường dẫn cũ |
| ③ Lưu DB trong `try` | Lưu lỗi thì xóa file mới vừa ghi để **không để rác** trên ổ cứng |
| `throw;` | Vẫn báo lỗi cho người dùng qua middleware |
| ④ Chỉ xóa ảnh cũ **sau khi DB lưu thành công** | Nếu xóa trước mà DB lỗi, DB vẫn trỏ tới ảnh đã mất → hỏng ảnh |
| `TrimStart('/')` | `/avatars/a.jpg` có dấu `/` đầu, `Path.Combine` sẽ hiểu sai là đường dẫn gốc |
| `System.IO.File` (viết đầy đủ) | Trong Controller, `File` trùng tên với hàm `File()` trả file của ASP.NET |

---

# TUẦN 5-6 – SOFT-DELETE, SERVICE, UNIT TEST

## 4.1. Xóa mềm + Global Query Filter

**Hiểu đơn giản:** thay vì **xé bỏ** hồ sơ, mình **đóng dấu "ĐÃ HỦY"** rồi cất vào ngăn riêng. Bình thường không ai thấy, nhưng khi cần vẫn lấy lại được.

**Xóa – `SinhVienService.DeleteAsync`:**
```csharp
public async Task DeleteAsync(int id)
{
    var sv = await TimHoacBaoLoiAsync(id);
    sv.IsDeleted = true;
    await _context.SaveChangesAsync();
}
```
→ Không gọi `Remove(sv)`, chỉ đổi cờ. SQL thực tế: `UPDATE SinhVien SET IsDeleted = 1 WHERE Id = @id`.

**Tự động ẩn – `Data/AppDbContext.cs`:**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted);
    modelBuilder.Entity<NguoiDung>().HasQueryFilter(u => !u.IsDeleted);
}
```
| Phần | Vì sao |
|---|---|
| `OnModelCreating` | Nơi cấu hình cách EF ánh xạ bảng, chạy một lần khi khởi động |
| `HasQueryFilter(s => !s.IsDeleted)` | EF **tự thêm** `WHERE IsDeleted = 0` vào **mọi** truy vấn trên bảng SinhVien |
| Áp dụng cho cả `NguoiDung` | Tài khoản bị khóa/xóa mềm cũng không đăng nhập được |

**Nếu không có Global Query Filter:** phải nhớ viết `.Where(s => !s.IsDeleted)` ở **mọi** chỗ (GetAll, GetById, kiểm tra trùng email...). Quên một chỗ là lộ dữ liệu đã xóa.

**Muốn xem cả dữ liệu đã xóa:**
```csharp
_context.SinhVien.IgnoreQueryFilters().Where(s => s.IsDeleted)
```
→ Hiện chưa có API nào dùng, nên trên web không ai xem được sinh viên đã xóa.

## 4.2. Tầng Service

**Hiểu đơn giản:** trước đây **phục vụ kiêm luôn đầu bếp** (Controller vừa nhận order vừa nấu). Giờ tách ra: phục vụ chỉ nhận order và mang món, đầu bếp lo nấu. Mỗi người làm tốt một việc, dễ kiểm tra từng người.

**Controller sau khi tách – mỏng, gọn:**
```csharp
[HttpPut("{id}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Update(int id, SinhVienDto sinhVienDto)
{
    await _service.UpdateAsync(id, sinhVienDto);
    return NoContent();
}

[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
{
    var created = await _service.CreateAsync(sinhVienDto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
}
```
| Phần | Vì sao |
|---|---|
| Controller chỉ gọi Service | Controller lo **HTTP** (đường dẫn, quyền, mã trả về), Service lo **nghiệp vụ** |
| `NoContent()` (204) | Sửa/xóa thành công, không cần trả dữ liệu |
| `CreatedAtAction(...)` (201) | Chuẩn REST khi tạo mới: trả 201 kèm header `Location` chỉ tới API đọc bản ghi vừa tạo |
| `nameof(GetById)` | Lấy tên hàm an toàn; đổi tên hàm thì compiler báo lỗi chứ không sai âm thầm |

**Service nhận DbContext qua constructor (Dependency Injection):**
```csharp
public class SinhVienService
{
    private readonly AppDbContext _context;
    public SinhVienService(AppDbContext context) => _context = context;
```
→ Service **không tự tạo** DbContext mà được "đưa vào". **Vì sao:** khi test, mình đưa vào DbContext InMemory thay cho SQL Server thật.

**Hàm dùng chung:**
```csharp
private async Task<SinhVien> TimHoacBaoLoiAsync(int id) =>
    await _context.SinhVien.FindAsync(id)
    ?? throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
```
| Phần | Vì sao |
|---|---|
| `FindAsync(id)` | Tìm theo khóa chính, có tính Global Query Filter |
| `?? throw` | Tìm được thì trả về, không thì ném 404 — gọn trong một dòng |
| Dùng chung cho GetById, Update, Delete | Không lặp lại cùng một đoạn kiểm tra 3 lần |

**Thêm mới – `CreateAsync`:**
```csharp
if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower()))
    throw new ConflictException("Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác.");
var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
_context.SinhVien.Add(sv);
await _context.SaveChangesAsync();
return ToDto(sv);
```
→ Không lấy `Id` từ client (DB tự sinh). Sau `SaveChangesAsync`, EF **tự điền** `sv.Id` vừa sinh nên trả về được Id mới.

**Vì sao bỏ `_context.Entry(sv).State = EntityState.Modified;`?** Đối tượng đọc bằng `FindAsync` đã được EF **theo dõi**, EF tự biết cột nào đổi. Gán `Modified` khiến EF UPDATE **tất cả** các cột và Audit ghi như thể mọi cột đều thay đổi.

**Đăng ký Service trong `Program.cs`:** `builder.Services.AddScoped<SinhVienService>();` — Scoped vì Service dùng DbContext (cũng Scoped).

## 4.3. Unit Test

**Hiểu đơn giản:** như **kiểm tra từng linh kiện** của xe trên bàn thử (động cơ riêng, phanh riêng) bằng dụng cụ giả lập, thay vì lắp cả xe chạy ngoài đường.

**Chuẩn bị DB giả – `SinhVienServiceTests.TaoDb()`:**
```csharp
private static AppDbContext TaoDb()
{
    var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    db.SinhVien.AddRange(
        new SinhVien { HoTen = "Binh",  Email = "c@x.com", Tuoi = 22, RowVersion = new byte[] { 1 } },
        new SinhVien { HoTen = "An",    Email = "b@x.com", Tuoi = 20, RowVersion = new byte[] { 1 } },
        new SinhVien { HoTen = "Cuong", Email = "a@x.com", Tuoi = 21, RowVersion = new byte[] { 1 } });
    db.SaveChanges();
    db.ChangeTracker.Clear();
    return db;
}
```
| Phần | Vì sao |
|---|---|
| `UseInMemoryDatabase` | DB nằm trong RAM: cực nhanh, không cần SQL Server |
| Tên `Guid.NewGuid()` | Mỗi test một DB riêng, không ảnh hưởng nhau |
| Dữ liệu mẫu cố ý chọn | Tên, email, tuổi có thứ tự khác nhau để kiểm tra sắp xếp |
| `RowVersion = { 1 }` | InMemory không tự sinh rowversion, phải gán sẵn |
| `ChangeTracker.Clear()` | Xóa "trí nhớ" của EF để test đọc lại từ DB như thật |

**Test theo mẫu AAA:**
```csharp
[Fact]
public async Task Create_HopLe_LuuVaoDb()
{
    var db = TaoDb();                                                    // Arrange – chuẩn bị
    var created = await new SinhVienService(db)
        .CreateAsync(new SinhVienDto { HoTen = "Moi", Email = "moi@x.com", Tuoi = 19 });  // Act – thực hiện
    Assert.True(created.Id > 0);                                        // Assert – kiểm tra
    Assert.Equal(4, await db.SinhVien.CountAsync());
}
```

**Một test chạy nhiều bộ dữ liệu – `[Theory]`:**
```csharp
[Theory]
[InlineData("hoten", false, 2)]   // An đứng đầu
[InlineData("hoten", true, 3)]    // Cuong đứng đầu
[InlineData("tuoi", false, 2)]    // 20 tuổi đứng đầu
[InlineData(null, false, 1)]      // không truyền SortBy → theo Id
public async Task GetAll_SapXep_DungThuTu(string? sortBy, bool desc, int idDauTien)
{
    var result = await new SinhVienService(TaoDb()).GetAllAsync(new SinhVienQuery { SortBy = sortBy, IsDescending = desc });
    Assert.Equal(idDauTien, result.Items[0].Id);
}
```
→ Mỗi `[InlineData]` là một lần chạy. 9 dòng = 9 lần, đi qua **mọi nhánh** của `switch` sắp xếp.

**Kiểm tra ném đúng lỗi:**
```csharp
[Fact]
public async Task GetById_KhongTonTai_NemNotFound()
{
    await Assert.ThrowsAsync<NotFoundException>(() => new SinhVienService(TaoDb()).GetByIdAsync(99));
}
```
→ Kiểm tra Service ném **đúng loại** exception, vì Middleware dựa vào loại để trả đúng mã 404.

**Test Middleware – `ExceptionMiddlewareTests`:** tạo `DefaultHttpContext` (request giả), cho `_next` ném một exception, rồi kiểm tra `StatusCode` và JSON trả về. Có 5 test: lỗi lạ → 500, `UnauthorizedAccessException` → 401, `ArgumentException` → 400, Production → `details = null`, `NotFoundException` → 404.

---

# TUẦN 7 – KIỂM THỬ NÂNG CAO VÀ CHẤT LƯỢNG CODE

## 5.1. Integration Test + TestContainers

**Hiểu đơn giản:** unit test là thử từng linh kiện; integration test là **lắp cả chiếc xe chạy thử**. Docker giống **đường thử riêng dùng một lần**: mỗi lần thử có một con đường mới, sạch sẽ; thử xong dọn đi, không ảnh hưởng đường thật (DB `QLSINHVIEN`).

**`IntegrationTests/ApiFactory.cs`:**
```csharp
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
```
| Phần | Vì sao |
|---|---|
| `WebApplicationFactory<Program>` | Chạy **toàn bộ** API (Program.cs, middleware, JWT...) ngay trong bộ nhớ, không cần `dotnet run` |
| `IAsyncLifetime` | Cho phép chạy code **trước** (`InitializeAsync`) và **sau** (`DisposeAsync`) bộ test |
| `MsSqlBuilder("...")` | Khai báo container SQL Server 2022 trong Docker |

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
}
```
→ **Ghi đè** chuỗi kết nối: API sẽ dùng DB trong container thay vì `QLSINHVIEN`. Làm được nhờ ở Tuần 1 mình **không viết cứng** chuỗi kết nối.

```csharp
public async Task InitializeAsync()
{
    await _db.StartAsync();
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.GetConnectionString()!.Contains("QLSINHVIEN"))
        throw new InvalidOperationException("Test đang trỏ vào DB thật!");
    await db.Database.EnsureCreatedAsync();
}
public new async Task DisposeAsync() => await _db.DisposeAsync();
```
| Dòng | Vì sao |
|---|---|
| `StartAsync()` | Bật container SQL Server |
| `CreateScope()` | DbContext là Scoped, phải tạo "phạm vi" để lấy ra |
| Chốt an toàn `Contains("QLSINHVIEN")` | Lỡ cấu hình sai thì **dừng ngay**, không làm bẩn DB thật |
| `EnsureCreatedAsync()` | Tạo các bảng theo Model (dự án không dùng Migration) |
| `public new ... DisposeAsync` | Xóa container khi xong. `new` vì lớp cha đã có hàm cùng tên nhưng khác kiểu trả về |

**`public partial class Program { }` (cuối `Program.cs`):** Program viết kiểu top-level nên compiler sinh class `Program` dạng `internal`. Dòng này thêm một phần `public` để project test dùng được `WebApplicationFactory<Program>`.

**`IntegrationTests/SinhVienApiTest.cs`:**
```csharp
public class SinhVienApiTests : IClassFixture<ApiFactory>
```
→ Cả class dùng **chung một** container (bật container mất vài giây, mỗi test bật một cái sẽ rất chậm).

**Helper tạo người dùng đã đăng nhập:**
```csharp
private async Task<HttpClient> TaoClientAsync(string role)
{
    var username = $"user_{Guid.NewGuid():N}";
    var client = _factory.CreateClient();
    (await client.PostAsJsonAsync("/api/XacThuc/dangky", new DangKyDto { Username = username, Password = "123456", FullName = "Test" }))
        .EnsureSuccessStatusCode();
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.NguoiDung.SingleAsync(u => u.Username == username);
        user.Role = role;
        await db.SaveChangesAsync();
    }
    var res = await client.PostAsJsonAsync("/api/XacThuc/dangnhap", new DangNhapDto { Username = username, Password = "123456" });
    var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
}
```
| Bước | Vì sao |
|---|---|
| Username bằng Guid | Mỗi test một tài khoản riêng, không bị "Tài khoản đã tồn tại" |
| Gọi API đăng ký **thật** | Kiểm tra luôn luồng đăng ký, BCrypt |
| Sửa Role trong DB | API đăng ký (đúng bảo mật) không cho chọn Role, nên test phải "nâng quyền" từ phía DB |
| Gọi API đăng nhập **thật** | Token do chính `GenerateJwtToken` tạo ra |
| `DefaultRequestHeaders.Authorization` | Mọi request sau của client này đều kèm token |

**Một kịch bản – xóa mềm:**
```csharp
var del = await admin.DeleteAsync($"/api/SinhVien/{created!.Id}");
Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);          // xóa được

var get = await admin.GetAsync($"/api/SinhVien/{created.Id}");
Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);           // API không còn thấy

var row = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == created.Id);
Assert.True(row.IsDeleted);                                       // nhưng DB vẫn còn
```
→ Chứng minh xuyên suốt: Controller → Service → Global Query Filter → SQL Server thật.

## 5.2. Code Coverage

**Hiểu đơn giản:** như đội kiểm tra đi qua các phòng trong tòa nhà. Coverage 85% = đã bước vào 85% số phòng. Nó cho biết **phòng nào chưa ai vào** (rủi ro), nhưng **không** cho biết các phòng đã vào có an toàn thật hay không.

**Các lệnh:**
```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```
| Phần | Vì sao |
|---|---|
| `--collect:"XPlat Code Coverage"` | Bật **Coverlet** (gói `coverlet.collector` có sẵn) để đếm dòng nào đã chạy |
| `coverage.cobertura.xml` | File kết quả dạng XML |
| `reportgenerator` | Đổi XML thành trang HTML tô xanh/đỏ dễ đọc |
| `-classfilters:"+...Services.*"` | **Chỉ tính tầng Service** — nơi chứa logic nghiệp vụ, đúng yêu cầu ≥ 80% |

| Màu trong báo cáo | Ý nghĩa |
|---|---|
| Xanh | Dòng đã được test chạy qua |
| Đỏ | Dòng chưa test nào chạy tới → cần viết thêm test |
| Vàng | Có nhánh `if` mới chạy một phía |

**Vì sao tách Service rồi mới đo được?** Yêu cầu tính coverage **cho tầng Service**. Trước đây logic nằm trong Controller nên không có gì để đo.

## 5.3. Optimistic Concurrency (RowVersion)

**Hiểu đơn giản:** hai người cùng mượn **bản nháp số 7** của một tài liệu để sửa. Người A nộp trước, tài liệu thành **bản số 8**. Người B nộp sau, trên tay vẫn là bản số 7 → thư ký thấy "bạn đang sửa trên bản cũ" và trả lại, không cho đè lên bản số 8 của A.

**Bước 1 – Cột tự đổi phiên bản (SQL):**
```sql
ALTER TABLE SinhVien ADD RowVersion ROWVERSION;
```
→ SQL Server **tự đổi** giá trị cột này mỗi khi dòng bị sửa. Mình không bao giờ tự gán.

**Bước 2 – Model:**
```csharp
[Timestamp]
public byte[]? RowVersion { get; set; }
```
→ `[Timestamp]` báo EF: (1) đừng ghi vào cột này, DB tự lo; (2) **thêm** `AND RowVersion = @giá_trị_cũ` vào mọi câu UPDATE.

**Bước 3 – DTO có `RowVersion`:** client nhận khi đọc. `byte[]` tự đổi thành chuỗi base64 trong JSON, ví dụ `"AAAAAAAAB9E="`.

**Bước 4 – Angular giữ và gửi lại (`sinh-vien.ts`):**
```typescript
sua(sv: SinhVien): void {
  this.sinhVien = {
    id: sv.id, hoTen: sv.hoTen, email: sv.email, tuoi: sv.tuoi,
    avatarUrl: sv.avatarUrl,
    rowVersion: sv.rowVersion        // giữ "số phiên bản" lúc mở form
  };
}
```
→ Thiếu dòng này thì khi lưu không gửi RowVersion → server báo "Thiếu RowVersion".

**Bước 5 – Service kiểm tra (`UpdateAsync`):**
```csharp
if (dto.RowVersion is null)
    throw new BadRequestException("Thiếu RowVersion! Vui lòng tải lại dữ liệu trước khi sửa.");

var sv = await TimHoacBaoLoiAsync(id);
// ...kiểm tra trùng email...

_context.Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;

sv.HoTen = dto.HoTen; sv.Email = dto.Email; sv.Tuoi = dto.Tuoi;
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    throw new ConflictException("Dữ liệu sinh viên đã bị người khác thay đổi. Vui lòng tải lại trang rồi sửa lại!");
}
```
| Dòng | Vì sao |
|---|---|
| Kiểm tra `RowVersion is null` → 400 | Không có thì SQL so `RowVersion IS NULL`, luôn sai → 409 khó hiểu. Báo 400 rõ ràng hơn |
| `OriginalValue = dto.RowVersion` | **Dòng quan trọng nhất.** `FindAsync` vừa đọc phiên bản **mới nhất** từ DB nên lúc nào cũng khớp. Phải thay bằng phiên bản **lúc người dùng mở form** thì mới phát hiện được người khác đã sửa |
| `SaveChangesAsync` | EF chạy `UPDATE ... WHERE Id = @id AND RowVersion = @bản_cũ` |
| UPDATE ảnh hưởng 0 dòng | EF hiểu là có người sửa trước → ném `DbUpdateConcurrencyException` |
| `catch` → `ConflictException` | Đổi thành lỗi 409 với câu thông báo dễ hiểu |

**Bước 6 – Angular nhận 409 (`luu()` → phần `error` của `update`):**
```typescript
error: (error) => {
  if (error.status === 409) {
    this.lamMoiForm();     // đóng form vì dữ liệu trên form đã cũ
    this.taiDanhSach();    // tải lại để có dữ liệu + RowVersion mới
  }
}
```
→ Toast đã do `errorInterceptor` hiện, nên ở đây không gọi toast nữa để tránh báo 2 lần.

**Bước 7 – Integration test chứng minh:**
```csharp
var formA = await admin.GetFromJsonAsync<SinhVienDto>(...);   // A mở form
var formB = await admin.GetFromJsonAsync<SinhVienDto>(...);   // B mở form, cùng RowVersion
formA.HoTen = "A đã sửa";
Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(..., formA)).StatusCode);  // A lưu được
formB.HoTen = "B đã sửa";
Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(..., formB)).StatusCode);   // B bị chặn 409
```

**Vì sao chọn "lạc quan" (optimistic) mà không khóa dữ liệu (pessimistic)?** Trên web, người dùng có thể mở form rất lâu rồi bỏ đi. Nếu khóa dòng dữ liệu ngay khi mở form, người khác sẽ bị chặn rất lâu. Cách lạc quan không khóa gì, chỉ **kiểm tra lúc lưu**, phù hợp vì đụng độ hiếm khi xảy ra.

---

# TỔNG KẾT – MỘT CÂU CHO MỖI YÊU CẦU

| Yêu cầu | Một câu dễ nhớ |
|---|---|
| DTO | Chỉ đưa ra ngoài những trang hồ sơ cần thiết |
| Validation | Lễ tân trả phiếu sai ngay ở cửa |
| BCrypt | Máy xay: xay được, không ghép lại được |
| JWT | Vé có đóng dấu, sửa chữ là dấu không khớp |
| Interceptor Angular | Trạm gắn vé và bắt lỗi cho mọi request |
| Phân trang | Thủ thư lọc trong kho, chỉ mang ra 5 cuốn |
| Phân quyền 3 lớp | Biển báo, bảo vệ hành lang, khóa vân tay — chỉ khóa vân tay là thật |
| Upload | Kiểm hàng, dán mã riêng, cất kho, ghi sổ |
| Exception tự định nghĩa | Mẫu phiếu in sẵn cho từng loại sự cố |
| ExceptionMiddleware | Lưới an toàn dưới gánh xiếc |
| Audit Interceptor | Camera tự ghi ai làm gì |
| Xóa ảnh cũ | Cất đồ mới xong mới vứt đồ cũ |
| Xóa mềm | Đóng dấu "ĐÃ HỦY" thay vì xé bỏ |
| Global Query Filter | Bộ lọc tự động che hồ sơ đã hủy |
| Tầng Service | Tách phục vụ và đầu bếp |
| Unit Test | Thử từng linh kiện trên bàn |
| Integration Test + Docker | Lắp cả xe chạy trên đường thử dùng một lần |
| Code Coverage | Đếm số phòng đội kiểm tra đã bước vào |
| RowVersion | Nộp bản nháp số cũ thì bị trả lại |
