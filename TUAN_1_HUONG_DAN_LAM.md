# TUẦN 1 – HƯỚNG DẪN LÀM TỪNG YÊU CẦU VÀ VÌ SAO LÀM NHƯ VẬY

> Dự án: Quản lý Sinh viên · Backend `D:\TT\QuanLySinhVien` · Frontend `D:\TT\QuanLySinhVienAngular`

Tuần 1 có 6 yêu cầu nền tảng. Nên làm theo đúng thứ tự dưới đây, vì yêu cầu sau dùng lại yêu cầu trước:

| # | Yêu cầu | Làm xong thì có gì |
|---|---|---|
| 1 | Không hardcode cấu hình | Chỗ để chứa chuỗi kết nối, khóa JWT |
| 2 | DTO + Validation | API nhận/trả dữ liệu an toàn |
| 3 | Lưu mật khẩu bằng BCrypt | Đăng ký, đăng nhập an toàn |
| 4 | Xác thực JWT | Đăng nhập xong được cấp "vé", API biết "bạn là ai" |
| 5 | async/await | Server chịu được nhiều người dùng |
| 6 | Xử lý server sập / đăng nhập sai | Giao diện luôn báo lỗi rõ ràng |

Mỗi yêu cầu gồm: **Mục tiêu → Các bước làm (kèm "vì sao") → Kiểm tra**.

---

## YÊU CẦU 1 – KHÔNG HARDCODE CẤU HÌNH

### Mục tiêu
Chuỗi kết nối DB và khóa JWT **không viết cứng trong code**, mà nằm trong file cấu hình.

### Bước 1 – Khai báo trong `appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=MSI\\SQLEXPRESS;Database=QLSINHVIEN;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Secret": "<một chuỗi bí mật dài ít nhất 32 ký tự>",
    "Issuer": "QuanLySinhVienAPI",
    "Audience": "QuanLySinhVienClient"
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

**Vì sao:**
- Mỗi môi trường (máy em, máy mentor, server thật, môi trường test) có DB khác nhau. Để trong file cấu hình thì **chỉ sửa file**, không phải sửa code và build lại.
- Tên mục `ConnectionStrings` là **tên chuẩn** của ASP.NET → đọc được bằng hàm tắt `GetConnectionString`.
- `\\` vì trong JSON dấu `\` phải viết đôi.
- `Trusted_Connection=True`: đăng nhập SQL bằng tài khoản Windows → không phải ghi mật khẩu SQL vào chuỗi.
- `TrustServerCertificate=True`: SQL Express dùng chứng chỉ tự ký; thiếu dòng này kết nối bị từ chối.
- `Secret` phải đủ dài: thuật toán HmacSha256 cần khóa tối thiểu 256 bit (32 byte), ngắn hơn sẽ báo lỗi khi tạo token.

### Bước 2 – Đọc cấu hình trong `Program.cs`
```csharp
// Kết nối DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình JWT
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
```

**Vì sao:**
- `builder.Configuration` là **kho cấu hình** ASP.NET tự nạp từ `appsettings.json`, `appsettings.{Môi trường}.json`, biến môi trường… Nguồn nạp sau **đè** nguồn trước → sau này (Tuần 7) test ghi đè được chuỗi kết nối mà không sửa code.
- `GetSection("JwtSettings")` lấy cả nhóm, sau đó đọc từng khóa bằng `["Secret"]`.
- `Encoding.UTF8.GetBytes(...)`: thuật toán ký cần khóa dạng **byte**.
- Dấu `!`: báo trình biên dịch "chắc chắn có giá trị"; nếu quên cấu hình thì ứng dụng **lỗi ngay khi khởi động** – dễ phát hiện hơn lỗi lúc đang chạy.

### Bước 3 – Đọc cấu hình trong Controller (qua Dependency Injection)
```csharp
public class XacThucController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public XacThucController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
}
```

**Vì sao:** Controller **không tự tạo** `IConfiguration` mà **xin** qua constructor; ASP.NET tự đưa vào (Dependency Injection). Nhờ vậy Controller không phụ thuộc vào việc cấu hình đến từ đâu.

### Kiểm tra
Đổi `Database=QLSINHVIEN` thành tên DB không tồn tại → chạy API → lỗi kết nối. Đổi lại → chạy bình thường. Chứng minh code **thật sự** đọc từ file.

> Ghi chú khi trình bày: với môi trường thật, khóa `Secret` nên đặt ở **User Secrets** hoặc **biến môi trường** để không đưa lên Git.

---

## YÊU CẦU 2 – DTO VÀ KIỂM TRA DỮ LIỆU ĐẦU VÀO

### Mục tiêu
API **không nhận/trả trực tiếp Entity**; dữ liệu gửi lên phải được kiểm tra.

### Bước 1 – Phân biệt Entity và DTO
`Models/SinhVien.cs` – **Entity**, ánh xạ đúng bảng DB, chỉ dùng bên trong:
```csharp
public class SinhVien
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Tuoi { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsDeleted { get; set; } = false;   // cột nội bộ
}
```

**Vì sao cần DTO riêng:** nếu API trả thẳng Entity:
- **Lộ cột nội bộ** (`IsDeleted`; với tài khoản là `PasswordHash`).
- **Over-posting**: client gửi kèm `"isDeleted": true` và server lỡ lưu vào.
- Đổi cấu trúc bảng là API đổi theo → Angular bị vỡ.

### Bước 2 – Tạo DTO kèm luật kiểm tra
Tạo thư mục `DTOs/`, file `SinhVienDto.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

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
}
```

**Vì sao:**
| Thành phần | Vì sao |
|---|---|
| Không có `IsDeleted` | Client không thấy, không gửi lên được |
| `[Required]` | Họ tên, email là thông tin bắt buộc |
| `[EmailAddress]` | Email sai định dạng thì không liên lạc được |
| `[Range(18, 99)]` | Ràng buộc nghiệp vụ: chặn tuổi âm, tuổi vô lý |
| `ErrorMessage` tiếng Việt | Mặc định là tiếng Anh; người dùng cần hiểu |
| `= string.Empty` | Dự án bật Nullable → tránh chuỗi `null` |

Làm tương tự cho tài khoản: `DangKyDto` (Username, Password, FullName) và `DangNhapDto` (Username, Password) – **không bao giờ** có `PasswordHash`.

### Bước 3 – Bật kiểm tra tự động bằng `[ApiController]`
```csharp
[Route("api/[controller]")]
[ApiController]
public class SinhVienController : ControllerBase { ... }
```

**Vì sao:** `[ApiController]` làm 2 việc: tự đọc JSON body vào DTO (**model binding**), và tự kiểm tra các attribute (**model validation**). Dữ liệu sai thì **trả 400 ngay**, hàm Controller **không chạy** → không phải viết `if (!ModelState.IsValid)` ở từng hàm.

### Bước 4 – Đọc dữ liệu: chuyển Entity → DTO bằng `Select`
```csharp
var items = await _context.SinhVien
    .Select(s => new SinhVienDto
    {
        Id = s.Id, HoTen = s.HoTen, Email = s.Email, Tuoi = s.Tuoi, AvatarUrl = s.AvatarUrl
    })
    .ToListAsync();
```

**Vì sao:** EF dịch `Select` thành `SELECT Id, HoTen, ...` – chỉ lấy đúng cột cần, nhẹ hơn lấy cả dòng rồi mới chuyển (**projection**).

### Bước 5 – Ghi dữ liệu: gán từng trường DTO → Entity
```csharp
var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
_context.SinhVien.Add(sv);
await _context.SaveChangesAsync();
```

**Vì sao:** chỉ những trường **được liệt kê** mới vào DB. Dù client có gửi thêm gì cũng bị bỏ qua → **chống over-posting**.

### Kiểm tra
- Swagger/Postman `GET /api/SinhVien` → JSON **không có** `isDeleted`.
- `POST /api/SinhVien` với `"tuoi": 10` → **400**, có câu "Tuổi phải là số dương từ 18 đến 99!".
- Gửi email `abc` → 400 "Email không đúng định dạng!".

---

## YÊU CẦU 3 – LƯU MẬT KHẨU AN TOÀN BẰNG BCRYPT

### Mục tiêu
Không lưu mật khẩu gốc; đăng nhập vẫn kiểm tra được đúng/sai.

### Bước 1 – Cài thư viện
NuGet cho project `QuanLySinhVien`: **`BCrypt.Net-Next`**.

**Vì sao chọn BCrypt mà không dùng SHA256:** SHA256 rất nhanh → kẻ tấn công thử được hàng tỉ mật khẩu mỗi giây. BCrypt **cố tình chậm** (băm lặp nhiều vòng) và **tự sinh salt**.

### Bước 2 – Bảng và Model tài khoản
SQL:
```sql
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NULL,
    CreatedAt DATETIME DEFAULT GETDATE(),
    Role VARCHAR(20) NOT NULL DEFAULT 'GiangVien'
);
```
`Models/NguoiDung.cs`:
```csharp
[Table("Users")]
public class NguoiDung
{
    public int Id { get; set; }
    [Required, MaxLength(50)] public string Username { get; set; } = string.Empty;
    [Required] public string PasswordHash { get; set; } = string.Empty;
    [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [Required, MaxLength(20)] public string Role { get; set; } = "GiangVien";
}
```

**Vì sao:**
- Cột tên là `PasswordHash` (không phải `Password`) → nhìn là biết chỉ chứa chuỗi băm.
- `UNIQUE` trên Username → DB cũng chặn trùng tài khoản (lớp bảo vệ thứ hai).
- `[Table("Users")]`: tên lớp tiếng Việt `NguoiDung` nhưng bảng tên `Users` → chỉ định rõ.
- `Role = "GiangVien"` mặc định → người tự đăng ký chỉ có quyền thấp nhất.

### Bước 3 – API Đăng ký
`Controllers/XacThucController.cs`:
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

**Vì sao:**
| Dòng | Vì sao |
|---|---|
| `IsNullOrWhiteSpace` | Chặn cả chuỗi rỗng lẫn chuỗi toàn dấu cách |
| `AnyAsync(...)` | Chỉ hỏi "có hay không", nhanh hơn lấy cả bản ghi |
| 409 khi trùng | Đúng nghĩa "xung đột với dữ liệu đã có" |
| `HashPassword(...)` | Tự sinh **salt** ngẫu nhiên + băm nhiều vòng → kết quả dạng `$2a$11$...` chứa sẵn salt và số vòng |
| Không gán `Role` | Dùng mặc định `GiangVien` |

> Ở Tuần 1, chỗ báo lỗi có thể viết `return BadRequest("...")`. Từ Tuần 3-4 đổi thành `throw new BadRequestException(...)` để Middleware trả JSON chuẩn.

### Bước 4 – API Đăng nhập (phần kiểm tra mật khẩu)
```csharp
[HttpPost("dangnhap")]
public async Task<IActionResult> DangNhap(DangNhapDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        throw new BadRequestException("Tài khoản và mật khẩu không được để trống!");

    var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
    if (nguoiDung == null) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

    bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, nguoiDung.PasswordHash);
    if (!isPasswordCorrect) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

    var token = GenerateJwtToken(nguoiDung);   // Yêu cầu 4
    return Ok(new { Token = token, FullName = nguoiDung.FullName, Role = nguoiDung.Role });
}
```

**Vì sao:**
- Hash **không giải ngược** được → `Verify` lấy salt từ chuỗi đã lưu, băm lại mật khẩu vừa nhập rồi so.
- **Sai tài khoản và sai mật khẩu báo cùng một câu**: nếu báo riêng "tài khoản không tồn tại", kẻ xấu dò được tài khoản nào có thật (*user enumeration*).

### Kiểm tra
- Đăng ký 2 tài khoản cùng mật khẩu `123456` → SSMS `SELECT Username, PasswordHash FROM Users` → hai chuỗi băm **khác nhau** (nhờ salt), không thấy mật khẩu gốc.
- Đăng nhập sai tên và sai mật khẩu → cùng một câu thông báo.

---

## YÊU CẦU 4 – XÁC THỰC BẰNG JWT

### Mục tiêu
Đăng nhập đúng thì được cấp **token**; các request sau gửi kèm token để API biết "bạn là ai, quyền gì".
**Vì sao cần:** HTTP không nhớ request trước – mỗi request độc lập.

### Bước 1 – Cài thư viện
NuGet: **`Microsoft.AspNetCore.Authentication.JwtBearer`**.

### Bước 2 – Tạo token khi đăng nhập
Trong `XacThucController`:
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
        issuer: jwtSettings["Issuer"],
        audience: jwtSettings["Audience"],
        claims: claims,
        expires: DateTime.Now.AddHours(2),
        signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**Vì sao:**
| Dòng | Vì sao |
|---|---|
| `Claim` | Mỗi claim là một "thông tin ghi trên vé" |
| `ClaimTypes.Name` | Sau này đọc được bằng `User.Identity.Name` (Audit Tuần 3-4 dùng) |
| `ClaimTypes.Role` | **Phải đúng loại này** thì `[Authorize(Roles = "Admin")]` (Tuần 2) mới nhận ra |
| `SymmetricSecurityKey` | Cùng một khóa để **ký** và **kiểm tra** |
| `HmacSha256` | Thuật toán ký phổ biến, an toàn |
| `issuer`/`audience` | Ghi "ai phát" và "phát cho ai" → server kiểm tra lại, chặn token của hệ thống khác |
| `expires: 2 giờ` | Token bị lộ cũng chỉ dùng được tối đa 2 giờ |
| `WriteToken` | Đổi thành chuỗi `eyJ...` để gửi cho client |

**Bản chất JWT:** 3 phần `Header.Payload.Signature`. Payload **chỉ mã hóa Base64 – ai cũng đọc được**, nên **không** đưa mật khẩu vào. An toàn nằm ở **Signature**: sửa một chữ trong Payload là chữ ký không còn khớp.

### Bước 3 – Bật kiểm tra token trong `Program.cs`
```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

// ... sau builder.Build():
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Vì sao:**
- `DefaultChallengeScheme = JwtBearer`: chưa đăng nhập mà gọi API cần quyền → trả **401** (không chuyển hướng sang trang login như kiểu cookie).
- 4 dòng `Validate... = true`: kiểm tra đủ **nơi phát hành, đối tượng nhận, hạn dùng, chữ ký**.
- `UseAuthentication()` **trước** `UseAuthorization()`: phải biết "là ai" rồi mới xét "được làm gì".
- `UseCors("AllowAngular")` (khai báo policy cho `http://localhost:4200`): Angular chạy cổng khác backend, thiếu CORS thì trình duyệt chặn request.

### Bước 4 – Angular lưu token (`services/auth.ts`)
```ts
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

logout(): void {
  if (this.isBrowser) {
    localStorage.removeItem('token');
    localStorage.removeItem('fullName');
    localStorage.removeItem('role');
  }
}

isLoggedIn(): boolean {
  return this.isBrowser ? !!localStorage.getItem('token') : false;
}
```

**Vì sao:**
- `tap(...)`: làm thêm việc phụ (lưu token) mà **không thay đổi** dữ liệu trả cho component.
- `localStorage`: F5 trang vẫn còn đăng nhập.
- `isBrowser` (dùng `isPlatformBrowser(PLATFORM_ID)`): dự án có **SSR** – khi render trên server không có `localStorage`, gọi vào sẽ lỗi.
- `res.token` viết thường: ASP.NET tự đổi `Token` → `token` (camelCase) khi xuất JSON.

### Bước 5 – Angular tự gắn token (`interceptors/jwt.ts`)
```ts
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
Đăng ký trong `app.config.ts`:
```ts
provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor]))
```

**Vì sao:**
- **Interceptor** là "trạm kiểm soát" mà **mọi** request HttpClient đi qua → gắn token **một lần cho tất cả**, không phải thêm tay ở từng service.
- `req.clone(...)`: request trong Angular **bất biến**, muốn thêm header phải tạo bản sao.
- `Bearer <token>`: đúng định dạng `AddJwtBearer` chờ đọc.

### Kiểm tra
- Đăng nhập → F12 → **Application → Local Storage** có `token`.
- F12 → **Network** → request bất kỳ có header `Authorization: Bearer eyJ...`.
- Dán token vào **jwt.io** → thấy role, hạn dùng (chứng minh Payload đọc được nhưng không sửa được).
- Gọi API cần đăng nhập mà không gửi token → **401**.

---

## YÊU CẦU 5 – LẬP TRÌNH BẤT ĐỒNG BỘ (async/await)

### Mục tiêu
Mọi thao tác DB đều bất đồng bộ.

### Cách làm
Quy tắc cho **mọi** hàm có truy cập DB:
1. Khai báo `async Task<...>` (hoặc `async Task` nếu không trả gì).
2. Dùng bản **Async** của EF Core: `ToListAsync`, `FirstOrDefaultAsync`, `FindAsync`, `AnyAsync`, `CountAsync`, `SaveChangesAsync`.
3. Đặt `await` trước mỗi lời gọi.
4. Đặt tên hàm có đuôi `Async` (với hàm tự viết, ví dụ `GetAllAsync`).

```csharp
public async Task<IActionResult> DangNhap(DangNhapDto dto)
{
    var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
    ...
}
```

**Vì sao:**
- Truy vấn DB mất thời gian. Bản đồng bộ (`ToList()`, `SaveChanges()`) khiến **luồng xử lý đứng chờ**. Server có số luồng giới hạn → nhiều người truy cập cùng lúc là hết luồng, request phải xếp hàng.
- Với `await`, trong lúc chờ DB, luồng được **trả về** để phục vụ request khác; DB xong thì chạy tiếp.
- Ví von: người phục vụ đưa phiếu cho bếp rồi đi phục vụ bàn khác, không đứng chờ món chín.
- Lưu ý: async **không làm một request nhanh hơn**, mà giúp server **chịu được nhiều request cùng lúc**.
- Quên `await` → code chạy tiếp khi DB chưa xong → kết quả sai hoặc lỗi DbContext bị dùng song song.

### Kiểm tra
Tìm trong project (Ctrl+Shift+F): `.ToList()`, `.SaveChanges()`, `.FirstOrDefault(` trong Controller/Service → không còn bản đồng bộ.

---

## YÊU CẦU 6 – XỬ LÝ KHI SERVER SẬP / ĐĂNG NHẬP SAI

### Mục tiêu
Backend tắt hay đăng nhập sai → giao diện báo rõ ràng **ngay lập tức**, không treo, không im lặng.

### Bước 1 – Hàm dùng chung đọc thông báo lỗi (`utils/error-message.ts`)
```ts
import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../models/api-error';

export function layThongBaoLoi(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại backend!';
    }
    const body = err.error as ApiError | null;
    if (body && typeof body === 'object' && body.message) return body.message;
    if (typeof err.error === 'string' && err.error.trim()) return err.error;
  }
  return 'Đã xảy ra lỗi không xác định!';
}
```

**Vì sao:**
| Dòng | Vì sao |
|---|---|
| Viết thành **một hàm** trong `utils/` | Đăng nhập, đăng ký, các trang khác dùng chung → báo lỗi **thống nhất** |
| `status === 0` | Trình duyệt **không nhận được phản hồi nào** (server tắt, sai địa chỉ, CORS chặn) |
| `body.message` | Server có phản hồi → hiện đúng câu server gửi |
| `typeof err.error === 'string'` | Phòng trường hợp server trả chuỗi thuần |
| Câu cuối | Không bao giờ để thông báo trống |

### Bước 2 – Dùng trong trang đăng nhập (`login/login.ts`)
```ts
constructor(private authService: AuthService, private router: Router, private cdr: ChangeDetectorRef) {
  if (this.authService.isLoggedIn()) this.router.navigate(['/sinh-vien']);
}

onSubmit(): void {
  this.errorMessage = '';
  this.authService.login(this.credentials).subscribe({
    next: () => this.router.navigate(['/sinh-vien']),
    error: (err) => {
      this.errorMessage = layThongBaoLoi(err);
      this.cdr.detectChanges(); // cập nhật giao diện ngay (ứng dụng chạy zoneless)
    }
  });
}
```

**Vì sao:**
- `errorMessage = ''` đầu hàm: xóa lỗi cũ trước mỗi lần thử.
- Đã đăng nhập mà vào trang login → tự chuyển sang trang chính.
- **`detectChanges()`**: Angular 22 chạy **zoneless** – không còn Zone.js tự phát hiện thay đổi. Gán `errorMessage` trong callback HTTP xong phải **báo thủ công** để vẽ lại; nếu không, banner lỗi chỉ hiện ở **lần bấm thứ hai** (lỗi em đã gặp và sửa).

### Bước 3 – Hiển thị trên giao diện (`login.html`)
```html
<div *ngIf="errorMessage" class="error-banner">{{ errorMessage }}</div>
```
**Vì sao `*ngIf`:** không có lỗi thì không hiện khung trống.

### Kiểm tra
- **Tắt backend** → đăng nhập → hiện ngay "Không thể kết nối đến máy chủ...".
- Bật backend, nhập **sai mật khẩu** → hiện ngay (lần bấm đầu tiên) "Tài khoản hoặc mật khẩu không chính xác!".

---

# TÓM TẮT – VÌ SAO TUẦN 1 LÀM NHƯ VẬY

| Yêu cầu | Làm gì | Vì sao (một câu) |
|---|---|---|
| Cấu hình | `appsettings.json` + `IConfiguration` | Đổi môi trường không phải sửa code; test ghi đè được |
| DTO | `SinhVienDto` + attribute + `[ApiController]` + `Select` | Không lộ cột nội bộ, chống over-posting, chặn dữ liệu sai từ cửa |
| BCrypt | `HashPassword` / `Verify` | Hash một chiều + salt: lộ DB cũng không lộ mật khẩu |
| JWT | Tạo token có chữ ký; `AddJwtBearer`; interceptor gắn `Bearer` | HTTP không nhớ; token chứng minh danh tính và không sửa được |
| async/await | Bản `...Async` + `await` | Không giữ luồng khi chờ DB → chịu nhiều request |
| Server sập / sai đăng nhập | `layThongBaoLoi` + `detectChanges()` | `status 0` = mất kết nối; zoneless phải tự vẽ lại giao diện |

**Câu chốt khi trình bày:** "Tuần 1 em xây nền móng theo nguyên tắc **không tin client** và **tách cấu hình khỏi code**: dữ liệu vào đều qua DTO có kiểm tra, mật khẩu chỉ lưu dạng băm, danh tính được chứng minh bằng JWT có chữ ký, mọi thao tác DB đều bất đồng bộ, và giao diện luôn báo lỗi rõ ràng."
