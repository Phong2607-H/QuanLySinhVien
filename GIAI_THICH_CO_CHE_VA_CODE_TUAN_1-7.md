# GIẢI THÍCH CƠ CHẾ VÀ CODE – TỪNG YÊU CẦU, TỪ TUẦN 1 ĐẾN TUẦN 7

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên
> Bản này viết theo **code hiện tại** của dự án (đã cập nhật: xóa mềm **chỉ** áp dụng cho Sinh viên; có RowVersion; có tầng Service).

## Cách đọc tài liệu

Mỗi yêu cầu được trình bày theo 4 mục cố định:

| Mục | Trả lời câu hỏi |
|---|---|
| **① Yêu cầu** | Mentor muốn gì? Vấn đề nếu không làm là gì? |
| **② Cơ chế** | Bên trong hệ thống, nó **chạy như thế nào**? |
| **③ Cách hiểu** | Ví von đời thường để nhớ lâu |
| **④ Code và "tại sao"** | Code em đã viết, và **vì sao có từng hàm / từng câu lệnh** |

---

# PHẦN 0 – HIỂU BỨC TRANH CHUNG TRƯỚC

## 0.1. Một request đi qua những đâu?

Ví dụ: Admin bấm **Lưu** khi sửa một sinh viên.

```
[Angular]
  sinh-vien.ts: luu()  →  SinhVienService.update()  →  HttpClient.put(...)
        ↓ jwtInterceptor      : gắn "Authorization: Bearer <token>"          (Tuần 1)
        ↓ (gửi qua mạng)
[ASP.NET Core – Program.cs, đi từ trên xuống]
  ExceptionMiddleware        : bọc try/catch quanh mọi thứ phía sau          (Tuần 3-4)
  UseStatusCodePages         : lỗi 401/403/404 không có body → thêm JSON      (Tuần 3-4)
  UseHttpsRedirection, UseCors, UseStaticFiles
  UseAuthentication          : đọc token → biết "bạn là ai"                  (Tuần 1)
  UseAuthorization           : xét [Authorize] → "bạn được làm gì"           (Tuần 2)
  [ApiController]            : kiểm tra DTO ([Required], [Range]...)          (Tuần 1)
  SinhVienController.Update  : gọi Service                                    (Tuần 5-6)
  SinhVienService.UpdateAsync: nghiệp vụ, RowVersion                          (Tuần 5-7)
  AppDbContext.SaveChanges   : Query Filter + AuditInterceptor                (Tuần 3-6)
  SQL Server
        ↓ trả về 204 hoặc JSON lỗi
[Angular]
  errorInterceptor           : nếu lỗi → Toast, 401 → login, 403 → trang chính (Tuần 2-4)
```

> Nhớ được sơ đồ này là trả lời được hầu hết câu hỏi "cái này nằm ở đâu, chạy lúc nào".

## 0.2. `Program.cs` – "bảng điều khiển" của backend

`Program.cs` có 2 nửa:

| Nửa | Câu lệnh tiêu biểu | Ý nghĩa |
|---|---|---|
| **Đăng ký dịch vụ** (trước `builder.Build()`) | `builder.Services.AddXxx(...)` | "Khai báo" cho hệ thống biết có những thành phần nào, để **Dependency Injection** tự tạo và đưa vào nơi cần |
| **Cấu hình pipeline** (sau `builder.Build()`) | `app.UseXxx(...)` | Sắp xếp **thứ tự** các "trạm kiểm soát" mà mọi request phải đi qua |

**Dependency Injection (DI) là gì?** Thay vì lớp tự `new` thứ nó cần, nó **khai báo trong constructor**, và ASP.NET tự đưa vào.
Ví dụ: `SinhVienController(AppDbContext context, IWebHostEnvironment env, SinhVienService service)` – Controller không tự tạo Service; ASP.NET thấy Controller cần `SinhVienService` → tạo Service → Service cần `AppDbContext` → tạo DbContext.
**Vì sao dùng DI:** dễ thay thế (test thì đưa DB giả vào), không phải quản lý vòng đời đối tượng bằng tay.

**Vòng đời đăng ký:**
| Kiểu | Sống bao lâu | Dùng cho |
|---|---|---|
| `AddScoped` | **Một request** | `AppDbContext`, `SinhVienService`, `AuditSaveChangesInterceptor` |
| `AddSingleton` | Suốt chương trình | (dự án không dùng) |
| `AddTransient` | Mỗi lần xin là tạo mới | (dự án không dùng) |

---

# TUẦN 1 – NỀN TẢNG

## 1.1. DTO và kiểm tra dữ liệu (Validation)

### ① Yêu cầu
Không trả thẳng **Entity** (lớp ánh xạ bảng DB) ra ngoài; dữ liệu gửi lên phải được kiểm tra.
Nếu không làm: lộ cột nội bộ (`IsDeleted`), client có thể gửi thêm trường để sửa thứ không được phép (**over-posting**), dữ liệu rác vào DB.

### ② Cơ chế
- **DTO** (Data Transfer Object) là lớp riêng chỉ để **vận chuyển dữ liệu qua API**. API nhận DTO, trả DTO; Entity chỉ dùng bên trong.
- **Model Binding**: ASP.NET tự đọc JSON trong request và đổ vào đối tượng `SinhVienDto`.
- **Model Validation**: ngay sau binding, ASP.NET đọc các attribute (`[Required]`, `[Range]`...) và kiểm tra. Nhờ `[ApiController]`, nếu sai thì **tự trả 400**, hàm trong Controller **không hề chạy**.

### ③ Cách hiểu
Entity là **hồ sơ gốc trong kho lưu trữ**. DTO là **phiếu photo** chỉ có những trang được phép đưa ra quầy. Các attribute là **ô "bắt buộc điền"** trên mẫu đơn – thiếu là quầy trả lại ngay, không chuyển vào trong.

### ④ Code và "tại sao"
`DTOs/SinhVienDto.cs`
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

| Câu lệnh | Tại sao có |
|---|---|
| `using System.ComponentModel.DataAnnotations;` | Chứa các attribute `[Required]`, `[EmailAddress]`, `[Range]` |
| `[Required(...)]` | Họ tên, email là thông tin bắt buộc của sinh viên |
| `[EmailAddress]` | Email sai định dạng sẽ không liên lạc được |
| `[Range(18, 99)]` | Ràng buộc nghiệp vụ: sinh viên từ 18 tuổi; chặn số âm, số vô lý |
| `ErrorMessage = "..."` | Mặc định câu lỗi tiếng Anh; viết tiếng Việt để người dùng hiểu |
| `= string.Empty;` | Dự án bật `Nullable`; gán mặc định để chuỗi không bao giờ `null` |
| `string? AvatarUrl` | Dấu `?` = được phép `null` – sinh viên mới có thể chưa có ảnh |
| `byte[]? RowVersion` | Thêm ở Tuần 7 để mang "số phiên bản" ra client |
| **Không có `IsDeleted`** | Client không nhìn thấy, cũng không gửi lên được cờ xóa |

Đọc dữ liệu – `SinhVienService.GetAllAsync`:
```csharp
.Select(s => new SinhVienDto { Id = s.Id, HoTen = s.HoTen, Email = s.Email, Tuoi = s.Tuoi, AvatarUrl = s.AvatarUrl, RowVersion = s.RowVersion })
```
**Tại sao `Select` (projection):** EF dịch thành `SELECT Id, HoTen, ...` – chỉ lấy đúng các cột cần, không lấy thừa, và đổi sang DTO ngay trong SQL.

Ghi dữ liệu – `SinhVienService.CreateAsync`:
```csharp
var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
```
**Tại sao gán từng trường:** chỉ những trường liệt kê ở đây mới đi vào DB. Đây là cách **chống over-posting**.

Tài khoản cũng dùng DTO riêng: `DangKyDto` (Username, Password, FullName), `DangNhapDto` (Username, Password) – **không bao giờ** có `PasswordHash`.

---

## 1.2. Không hardcode cấu hình

### ① Yêu cầu
Không viết cứng chuỗi kết nối, khóa bí mật trong code.
Nếu không làm: đổi máy / đổi môi trường phải sửa code, build lại; khóa bí mật nằm lẫn trong mã nguồn.

### ② Cơ chế
ASP.NET tự đọc `appsettings.json` (và `appsettings.Development.json`, biến môi trường…) thành một **kho cấu hình** chung, truy cập qua `IConfiguration`. Code chỉ **hỏi theo tên khóa**. Nguồn nào nạp sau thì **đè** nguồn trước – đây là lý do Tuần 7 ghi đè được chuỗi kết nối.

### ③ Cách hiểu
Như **bảng điện có công tắc**: muốn đổi đèn chỉ gạt công tắc (sửa file cấu hình), không phải đục tường đi lại dây (sửa code).

### ④ Code và "tại sao"
`appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=MSI\\SQLEXPRESS;Database=QLSINHVIEN;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": { "Secret": "<khóa bí mật>", "Issuer": "QuanLySinhVienAPI", "Audience": "QuanLySinhVienClient" }
}
```

| Thành phần | Tại sao có |
|---|---|
| `Server=MSI\\SQLEXPRESS` | Tên máy chủ SQL (dấu `\` phải viết `\\` trong JSON) |
| `Database=QLSINHVIEN` | Tên database |
| `Trusted_Connection=True` | Đăng nhập SQL bằng tài khoản Windows, không phải ghi mật khẩu vào chuỗi |
| `TrustServerCertificate=True` | SQL Express dùng chứng chỉ tự ký; không có dòng này kết nối bị từ chối |
| `JwtSettings` | Gom các giá trị JWT vào một nhóm để đọc một lần |

`Program.cs`
```csharp
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
```

| Câu lệnh | Tại sao có |
|---|---|
| `GetConnectionString("DefaultConnection")` | Hàm tắt để đọc `ConnectionStrings:DefaultConnection` |
| `GetSection("JwtSettings")` | Lấy cả nhóm, sau đó đọc `["Secret"]`, `["Issuer"]`... |
| `Encoding.UTF8.GetBytes(...)` | Thuật toán ký cần khóa dạng **byte**, không phải chuỗi |
| Dấu `!` | Báo trình biên dịch "chắc chắn không null" (nếu thiếu cấu hình thì lỗi ngay khi khởi động – dễ phát hiện) |

Trong Controller: `XacThucController(AppDbContext context, IConfiguration configuration)` – nhận `IConfiguration` qua DI để đọc `JwtSettings` khi tạo token.

---

## 1.3. Lưu mật khẩu an toàn với BCrypt

### ① Yêu cầu
Không lưu mật khẩu gốc.
Nếu không làm: ai đọc được DB (hacker, người quản trị) biết mật khẩu mọi người – mà người dùng hay dùng chung mật khẩu cho nhiều trang.

### ② Cơ chế
- **Băm (hash)** là phép biến đổi **một chiều**: từ mật khẩu ra chuỗi băm thì dễ, từ chuỗi băm ra mật khẩu thì không thể.
- **Salt**: chuỗi ngẫu nhiên trộn vào trước khi băm → hai người cùng mật khẩu `123456` vẫn có hai chuỗi băm khác nhau.
- **BCrypt cố tình chậm** (băm lặp 2^11 vòng với work factor 11) → kẻ tấn công thử hàng tỉ mật khẩu sẽ mất rất nhiều thời gian.
- Chuỗi kết quả `$2a$11$<salt><hash>` **chứa sẵn** salt và số vòng. Khi đăng nhập, `Verify` lấy salt từ chuỗi đó, băm lại mật khẩu vừa nhập rồi so sánh.

### ③ Cách hiểu
Như **máy xay sinh tố**: cho trái cây vào ra nước ép dễ, nhưng không thể biến nước ép trở lại thành trái cây. Muốn kiểm tra ai đó đưa đúng loại trái cây không, ta xay thử rồi so ly nước ép.

### ④ Code và "tại sao"
`XacThucController.DangKy`
```csharp
[HttpPost("dangky")]
public async Task<IActionResult> DangKy(DangKyDto dto)
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        throw new BadRequestException("Tài khoản và mật khẩu không được để trống!");

    if (await _context.NguoiDung.IgnoreQueryFilters().AnyAsync(u => u.Username == dto.Username))
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

| Câu lệnh | Tại sao có |
|---|---|
| `[HttpPost("dangky")]` | Đăng ký là **tạo mới** dữ liệu → dùng POST; URL `/api/XacThuc/dangky` |
| `IsNullOrWhiteSpace` | Chặn cả chuỗi rỗng **và** chuỗi toàn dấu cách |
| `throw new BadRequestException` | Không tự trả 400, chỉ ném lỗi – Middleware (Tuần 3-4) đổi thành JSON chuẩn |
| `AnyAsync(u => u.Username == ...)` | Kiểm tra trùng tên. `Any` chỉ hỏi "có hay không", nhanh hơn lấy cả bản ghi |
| `.IgnoreQueryFilters()` | Còn sót lại từ lúc tài khoản có xóa mềm; nay bảng NguoiDung không còn filter nên dòng này **không có tác dụng**, có thể bỏ |
| `ConflictException` (409) | "Trùng với dữ liệu đã có" đúng nghĩa mã 409 Conflict |
| `BCrypt.HashPassword(dto.Password)` | Băm + tự sinh salt. Chỉ chuỗi băm được lưu |
| Không gán `Role` | Model `NguoiDung` mặc định `Role = "GiangVien"` → tự đăng ký chỉ có quyền thấp |
| `SaveChangesAsync()` | Thực sự ghi xuống DB (trước đó `Add` mới chỉ "đánh dấu") |

`XacThucController.DangNhap`
```csharp
var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
if (nguoiDung == null) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, nguoiDung.PasswordHash);
if (!isPasswordCorrect) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

var token = GenerateJwtToken(nguoiDung);
return Ok(new { Token = token, FullName = nguoiDung.FullName, Role = nguoiDung.Role });
```

| Câu lệnh | Tại sao có |
|---|---|
| `FirstOrDefaultAsync` | Lấy tài khoản theo tên; không có thì trả `null` thay vì ném lỗi |
| `BCrypt.Verify(...)` | Không thể giải ngược hash, chỉ có thể **băm lại và so** |
| **Hai chỗ cùng một câu lỗi** | Nếu báo riêng "không có tài khoản" thì kẻ xấu dò ra được tài khoản nào tồn tại (*user enumeration*) |
| Trả `Token, FullName, Role` | Angular cần token để gọi API, tên để chào, role để ẩn/hiện nút |

---

## 1.4. Xác thực bằng JWT

### ① Yêu cầu
Sau khi đăng nhập, các request tiếp theo phải chứng minh được "tôi là ai, có quyền gì".
Vấn đề: HTTP **không nhớ** request trước – mỗi request là độc lập.

### ② Cơ chế
- Đăng nhập đúng → server tạo **JWT** gồm 3 phần `Header.Payload.Signature`:
  - **Payload** chứa *claims* (Id, Username, Role, FullName, hạn dùng). Chỉ **mã hóa Base64**, ai cũng đọc được.
  - **Signature** = băm(Header + Payload) bằng **khóa bí mật** chỉ server biết.
- Client gửi token ở header `Authorization: Bearer <token>`.
- Server **tính lại chữ ký**; khớp → tin nội dung, không khớp (bị sửa) → 401.
- Server **không lưu phiên** → gọi là **stateless**.

### ③ Cách hiểu
JWT như **vé xem phim có dấu mộc**. Ai cũng đọc được trên vé ghi phòng mấy, ghế nào (Payload), nhưng không ai tự sửa được vì dấu mộc (Signature) chỉ rạp có. Nhân viên chỉ cần soi dấu mộc là cho vào, không phải tra danh sách.

### ④ Code và "tại sao"
**Tạo token** – `XacThucController.GenerateJwtToken`
```csharp
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
```

| Câu lệnh | Tại sao có |
|---|---|
| `ClaimTypes.NameIdentifier` | Mã người dùng – định danh duy nhất |
| `ClaimTypes.Name` | Tên đăng nhập → sau này `User.Identity.Name` đọc được (Audit dùng) |
| `ClaimTypes.Role` | **Bắt buộc dùng đúng loại này** để `[Authorize(Roles = "Admin")]` nhận ra |
| `"FullName"` | Claim tự đặt, để hiển thị |
| `SymmetricSecurityKey` | "Đối xứng" = cùng một khóa để **ký** và để **kiểm tra** |
| `HmacSha256` | Thuật toán ký phổ biến, an toàn |
| `issuer` / `audience` | Ghi "ai phát hành" và "phát cho ai" → server kiểm tra lại để chặn token của hệ thống khác |
| `expires: AddHours(2)` | Token bị lộ cũng chỉ dùng được tối đa 2 giờ |
| `WriteToken(token)` | Đổi đối tượng thành chuỗi `eyJ...` để gửi cho client |

**Kiểm tra token** – `Program.cs`
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
        ValidateIssuer = true, ValidateAudience = true,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"], ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});
...
app.UseAuthentication();
app.UseAuthorization();
```

| Câu lệnh | Tại sao có |
|---|---|
| `DefaultAuthenticateScheme = JwtBearer` | Mặc định xác thực bằng JWT (không phải cookie) |
| `DefaultChallengeScheme = JwtBearer` | Khi chưa đăng nhập mà vào API cần quyền → trả **401** (thay vì chuyển hướng trang login như cookie) |
| 4 dòng `Validate... = true` | Kiểm tra đủ: đúng nơi phát hành, đúng đối tượng, còn hạn, đúng chữ ký |
| `IssuerSigningKey` | Cùng khóa đã dùng để ký – dùng để tính lại chữ ký |
| `UseAuthentication()` **trước** `UseAuthorization()` | Phải biết "là ai" rồi mới xét "được làm gì" |

**Angular lưu token** – `services/auth.ts`
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
```

| Câu lệnh | Tại sao có |
|---|---|
| `pipe(tap(...))` | `tap` = "làm thêm việc phụ" (lưu token) mà **không thay đổi** dữ liệu trả về cho component |
| `this.isBrowser` | Dự án có **SSR** (render trên server); trên server không có `localStorage` → kiểm tra trước để không lỗi |
| `localStorage.setItem` | Lưu lại để F5 trang vẫn còn đăng nhập |
| `res.token` (chữ thường) | ASP.NET tự đổi `Token` → `token` (camelCase) khi xuất JSON |

**Angular gắn token** – `interceptors/jwt.ts`
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

| Câu lệnh | Tại sao có |
|---|---|
| `HttpInterceptorFn` | "Trạm kiểm soát" mà **mọi** request HttpClient đi qua → không phải gắn token tay ở từng service |
| `typeof window !== 'undefined'` | Chặn chạy trên server (SSR) |
| `req.clone({...})` | Request trong Angular là **bất biến** (không sửa được) → phải tạo bản sao có thêm header |
| `` `Bearer ${token}` `` | Định dạng chuẩn mà `AddJwtBearer` chờ đọc |
| `next(...)` | Chuyển request sang trạm tiếp theo / gửi đi |

Đăng ký trong `app.config.ts`: `provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor]))` – thứ tự: gắn token trước, bắt lỗi sau.

---

## 1.5. Lập trình bất đồng bộ (async/await)

### ① Yêu cầu
Mọi thao tác DB phải bất đồng bộ.
Nếu không: mỗi request **giữ một luồng (thread)** đứng chờ DB; nhiều người truy cập cùng lúc là hết luồng, web chậm hoặc treo.

### ② Cơ chế
Khi gặp `await` một thao tác I/O (DB, file, mạng), luồng hiện tại được **trả về thread pool** để phục vụ request khác. Khi DB trả kết quả, một luồng (có thể khác) tiếp tục chạy phần code sau `await`.

### ③ Cách hiểu
**Phục vụ quán ăn**: đồng bộ = đứng cạnh bếp chờ món chín; bất đồng bộ = đưa phiếu cho bếp rồi đi phục vụ bàn khác, món xong quay lại bưng.

### ④ Code và "tại sao"
```csharp
public async Task<SinhVienDto> GetByIdAsync(int id)
{
    var sv = await TimHoacBaoLoiAsync(id);
    return ToDto(sv);
}
```

| Thành phần | Tại sao có |
|---|---|
| `async` | Cho phép dùng `await` bên trong |
| `Task<SinhVienDto>` | Hàm trả về một "lời hứa" sẽ có `SinhVienDto` khi xong |
| `await` | Chờ **không chặn luồng** |
| Đuôi `...Async` | Quy ước đặt tên: nhìn tên biết hàm bất đồng bộ |
| Dùng `ToListAsync`, `FindAsync`, `AnyAsync`, `CountAsync`, `SaveChangesAsync` | Các bản Async của EF Core – bản đồng bộ (`ToList`, `SaveChanges`) sẽ chặn luồng |

> Lưu ý: async **không làm một request nhanh hơn**; nó giúp server **chịu được nhiều request cùng lúc**.

---

## 1.6. Xử lý khi server sập / đăng nhập sai

### ① Yêu cầu
Khi backend tắt hoặc đăng nhập sai, giao diện phải báo rõ ràng, không treo, không im lặng.

### ② Cơ chế
- Khi trình duyệt **không nhận được phản hồi HTTP nào** (server tắt, sai địa chỉ, CORS chặn), Angular trả lỗi với `status = 0`.
- Khi server có phản hồi lỗi, body là JSON `{statusCode, message}` → lấy `message`.
- Angular 22 chạy **zoneless** (không có Zone.js tự phát hiện thay đổi) → gán biến trong callback HTTP xong phải **báo thủ công** để vẽ lại giao diện.

### ③ Cách hiểu
`status 0` như **gọi điện không ai bắt máy** (khác với bắt máy rồi nói "sai số"). Zoneless như **bảng tin không tự cập nhật** – viết tin mới xong phải bấm "làm mới".

### ④ Code và "tại sao"
`utils/error-message.ts`
```ts
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

| Câu lệnh | Tại sao có |
|---|---|
| Hàm riêng trong `utils/` | Dùng chung cho login, register, lịch sử, interceptor → mọi chỗ báo lỗi giống nhau |
| `err: unknown` | Lỗi có thể là bất kỳ thứ gì; buộc phải kiểm tra kiểu trước khi dùng |
| `instanceof HttpErrorResponse` | Chỉ xử lý lỗi HTTP; lỗi khác rơi xuống câu chung |
| `status === 0` | Không có phản hồi → báo mất kết nối |
| `body.message` | Đọc đúng câu backend gửi (định dạng chuẩn từ Tuần 3-4) |
| `typeof err.error === 'string'` | Phòng trường hợp backend trả chuỗi thuần |
| Câu cuối cùng | Không bao giờ để thông báo trống |

`login/login.ts`
```ts
error: (err) => {
  this.errorMessage = layThongBaoLoi(err);
  this.cdr.detectChanges(); // Cập nhật giao diện ngay (ứng dụng chạy zoneless)
}
```
**Tại sao `detectChanges()`:** không có dòng này, banner lỗi chỉ hiện ở **lần bấm thứ hai** (lỗi em từng gặp), vì Angular zoneless không tự biết `errorMessage` đã đổi.

---

# TUẦN 2 – TÍNH NĂNG CHÍNH

## 2.1. Phân trang, tìm kiếm, sắp xếp phía server

### ① Yêu cầu
Danh sách sinh viên phải phân trang, tìm kiếm, sắp xếp **ở phía server**.
Nếu làm phía client: mỗi lần mở trang phải tải **toàn bộ** bảng về trình duyệt → bảng 100.000 dòng là chậm, tốn mạng, tốn RAM.

### ② Cơ chế
- Angular gửi điều kiện lên URL: `?pageNumber=2&pageSize=5&keyword=an&sortBy=hoten&isDescending=true`.
- ASP.NET **model binding** đổ các tham số vào đối tượng `SinhVienQuery`.
- Service dựng truy vấn bằng **`IQueryable`**. Điểm mấu chốt là **thực thi trễ (deferred execution)**: `Where`, `OrderBy`, `Skip`, `Take` chỉ **ghép dần** điều kiện, **chưa chạy SQL**. SQL chỉ chạy khi gặp `CountAsync()` hoặc `ToListAsync()`. Vì vậy toàn bộ lọc, sắp xếp, cắt trang diễn ra **trong SQL Server**.
- `Skip/Take` được EF dịch thành `OFFSET x ROWS FETCH NEXT y ROWS ONLY`.

### ③ Cách hiểu
`IQueryable` giống **viết phiếu order**: ghi dần "lọc tên có chữ an", "xếp theo tên", "lấy trang 2". Viết xong mới **đưa phiếu cho bếp** (SQL Server) nấu một lần và bưng ra đúng 5 món. Không phải bưng cả bếp ra bàn rồi tự chọn.

### ④ Code và "tại sao"
`DTOs/SinhVienQuery.cs`
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
**Tại sao gom thành một lớp:** 5 tham số rời trong hàm sẽ rất dài; gom lại dễ đọc, dễ thêm tham số. Giá trị mặc định `= 1`, `= 5` để client không gửi vẫn chạy được.

`DTOs/PagedResult.cs`
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

| Câu lệnh | Tại sao có |
|---|---|
| `<T>` (generic) | Dùng lại cho bất kỳ loại dữ liệu nào cần phân trang, không chỉ sinh viên |
| `TotalCount` | Angular cần tổng số dòng để hiện "Tổng cộng N sinh viên" và tính số trang |
| `TotalPages => ...` | Thuộc tính **tính toán**, không lưu; 11 dòng / 5 = 2,2 → `Ceiling` làm tròn lên 3 trang |
| `(double)` | Nếu chia số nguyên 11/5 = 2 (mất phần lẻ) → ép kiểu để chia số thực |

Controller: `GetAll([FromQuery] SinhVienQuery query)` – `[FromQuery]` báo ASP.NET lấy dữ liệu từ **chuỗi truy vấn trên URL**.

`SinhVienService.GetAllAsync`
```csharp
if (query.PageNumber < 1) query.PageNumber = 1;
if (query.PageSize < 1 || query.PageSize > 50) query.PageSize = 5;

var queryable = _context.SinhVien.AsNoTracking();

if (!string.IsNullOrWhiteSpace(query.Keyword))
{
    var keyword = query.Keyword.Trim().ToLower();
    queryable = queryable.Where(s =>
        s.HoTen.ToLower().Contains(keyword) || s.Email.ToLower().Contains(keyword));
}

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

var totalCount = await queryable.CountAsync();

var items = await queryable
    .Skip((query.PageNumber - 1) * query.PageSize)
    .Take(query.PageSize)
    .Select(s => new SinhVienDto { ... })
    .ToListAsync();

return new PagedResult<SinhVienDto> { Items = items, TotalCount = totalCount, PageNumber = query.PageNumber, PageSize = query.PageSize };
```

| Câu lệnh | Tại sao có |
|---|---|
| Hai dòng `if` đầu | **Không tin client**: trang 0, trang âm, `pageSize=100000` đều bị sửa về giá trị an toàn → không ai tải được cả bảng |
| `AsNoTracking()` | Chỉ đọc để hiển thị → không cần EF theo dõi thay đổi → nhanh và nhẹ hơn |
| `var queryable = ...` | Đây là `IQueryable` – **chưa chạy SQL** |
| `Trim().ToLower()` | Bỏ dấu cách thừa, không phân biệt hoa thường (" AN " vẫn tìm ra "An") |
| `Contains(keyword)` | EF dịch thành `LIKE '%an%'` |
| `\|\|` (hoặc) | Tìm trong cả họ tên **lẫn** email |
| `switch` trên **tuple** `(SortBy, IsDescending)` | Mỗi cặp ứng với một kiểu sắp xếp; gọn hơn chuỗi `if/else` lồng nhau |
| `SortBy?.` | `?.` = nếu `SortBy` là null thì không gọi `Trim()` (tránh lỗi null) |
| Chỉ chấp nhận cột cố định | Client không thể yêu cầu sắp xếp theo cột lạ / cột nội bộ |
| `(_, true)` và `_` | `_` = "bất kỳ giá trị nào" → mặc định sắp theo Id |
| **Luôn có OrderBy** | SQL Server **bắt buộc** có `ORDER BY` khi dùng `OFFSET`; không sắp xếp thì thứ tự giữa các trang không ổn định |
| `CountAsync()` **trước** `Skip/Take` | Đếm **tổng** dòng thỏa điều kiện (chứ không phải đếm 5 dòng của trang) |
| `Skip((page - 1) * size)` | Trang 2, mỗi trang 5 → bỏ qua 5 dòng đầu |
| `Take(size)` | Lấy đúng `size` dòng |
| `Select(...)` + `ToListAsync()` | Chọn cột, đổi sang DTO, rồi **lúc này mới chạy SQL** |

`services/sinh-vien.ts` (Angular)
```ts
getAll(page: number, size: number, keyword: string, sortBy: string, isDesc: boolean): Observable<any> {
  let params = `?pageNumber=${page}&pageSize=${size}`;
  if (keyword) params += `&keyword=${encodeURIComponent(keyword)}`;
  if (sortBy) params += `&sortBy=${sortBy}&isDescending=${isDesc}`;
  return this.http.get<any>(`${this.apiUrl}${params}`);
}
```
**Tại sao `encodeURIComponent`:** từ khóa có dấu tiếng Việt, dấu cách, `&`… sẽ làm hỏng URL nếu không mã hóa.

`sinh-vien/sinh-vien.ts` (Angular)

| Hàm | Làm gì | Tại sao |
|---|---|---|
| `taiDanhSach()` | Gọi `getAll`, gán `danhSachSinhVien`, `tongSoDong`, `tongSoTrang`, rồi `detectChanges()` | Một hàm duy nhất để tải lại – mọi thao tác (thêm, sửa, xóa, chuyển trang) đều gọi nó |
| `timKiem()` | Đặt `trangHienTai = 1` rồi tải | Kết quả tìm kiếm có thể ít trang hơn; đang ở trang 5 mà kết quả chỉ 1 trang sẽ thấy rỗng |
| `thayDoiSapXep(cot)` | Bấm cùng cột thì đảo chiều, cột khác thì tăng dần | Thói quen người dùng ở mọi bảng dữ liệu |
| `chuyenTrang(trang)` | Chặn trang < 1 hoặc > `tongSoTrang` | Không gửi request vô nghĩa |
| Trong `xoa()` | Nếu xóa dòng cuối của trang (không phải trang 1) thì lùi một trang | Tránh hiện trang trống |

---

## 2.2. Phân quyền 3 lớp

### ① Yêu cầu
Admin được Thêm / Sửa / Xóa và xem Lịch sử; Giảng viên chỉ xem (và đổi ảnh).
Nếu chỉ ẩn nút: người dùng vẫn gõ URL hoặc dùng Postman gọi thẳng API.

### ② Cơ chế
| Lớp | Chạy ở đâu | Bảo mật thật? |
|---|---|---|
| 1. Giao diện (`*ngIf`) | Trình duyệt | ❌ Chỉ để gọn giao diện |
| 2. Route Guard | Trình duyệt | ❌ Chặn gõ URL, nhưng vẫn sửa được code phía client |
| 3. **`[Authorize(Roles)]` trên API** | **Server** | ✅ Role lấy từ **JWT có chữ ký**, không giả được |

- Không có / token sai → **401 Unauthorized** ("bạn là ai?").
- Có token nhưng sai role → **403 Forbidden** ("biết bạn là ai, nhưng không được phép").

### ③ Cách hiểu
Tòa nhà công ty: **biển chỉ dẫn** không ghi phòng giám đốc (lớp 1), **lễ tân** hỏi khi bạn đi về hướng đó (lớp 2), nhưng thứ thật sự chặn là **cửa quẹt thẻ** (lớp 3). Biển và lễ tân giúp tiện, khóa cửa mới là an ninh.

### ④ Code và "tại sao"
**Lớp 3 – API** (`SinhVienController.cs`)
```csharp
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SinhVienController : ControllerBase
{
    [HttpGet] ...                                     // Admin + GiangVien
    [HttpPost]   [Authorize(Roles = "Admin")] ...
    [HttpPut("{id}")]    [Authorize(Roles = "Admin")] ...
    [HttpDelete("{id}")] [Authorize(Roles = "Admin")] ...
    [HttpPost("upload-avatar/{id}")] [Authorize(Roles = "Admin,GiangVien")] ...
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `[Route("api/[controller]")]` | `[controller]` tự thay bằng tên lớp bỏ chữ "Controller" → `/api/SinhVien` |
| `[ApiController]` | Bật tự kiểm tra DTO (trả 400), tự đọc JSON body |
| `[Authorize]` ở **lớp** | Mọi hàm trong controller đều phải đăng nhập |
| `[Authorize(Roles = "Admin")]` ở **hàm** | Thêm yêu cầu role cho riêng hàm đó |
| `"Admin,GiangVien"` | Dấu phẩy = **hoặc** (một trong hai role) |
| `AuditLogController` có `[Authorize(Roles = "Admin")]` ở lớp | Toàn bộ lịch sử chỉ Admin xem |

**Lớp 2 – Route Guard** (`guards/role.ts`)
```ts
export const roleGuard = (allowedRoles: string[]): CanActivateFn => {
  return () => {
    const platformId = inject(PLATFORM_ID);
    if (!isPlatformBrowser(platformId)) return true;

    const authService = inject(AuthService);
    const router = inject(Router);
    const toastService = inject(ToastService);

    if (!authService.isLoggedIn()) return router.parseUrl('/login');
    if (allowedRoles.includes(authService.getRole())) return true;

    toastService.showError('Không thể truy cập dưới quyền Admin.');
    return router.parseUrl('/sinh-vien');
  };
};
```

| Câu lệnh | Tại sao có |
|---|---|
| Hàm nhận `allowedRoles` rồi **trả về** guard | Dùng lại được: `roleGuard(['Admin'])`, `roleGuard(['Admin','GiangVien'])` |
| `CanActivateFn` | Kiểu hàm Angular gọi **trước** khi vào một route; trả `true` thì cho vào |
| `inject(...)` | Lấy service trong hàm (không có constructor) |
| `isPlatformBrowser` → `return true` | Trên server (SSR) không có localStorage → để trình duyệt kiểm tra lại |
| `router.parseUrl('/login')` | Trả về **UrlTree** = "đừng vào đây, chuyển sang trang kia" – Angular hủy điều hướng cũ và chuyển trong một bước |

`app.routes.ts`: `{ path: 'lich-su', component: LichSuComponent, canActivate: [roleGuard(['Admin'])] }` và `/sinh-vien` dùng `authGuard` (chỉ cần đăng nhập).

**Lớp 1 – Giao diện** (`sinh-vien.html`)
```html
<button *ngIf="authService.hasRole('Admin')" routerLink="/lich-su">Lịch sử</button>
<div class="form-box" *ngIf="authService.hasRole('Admin')"> ... </div>
<th *ngIf="authService.hasRole('Admin')">Thao tác</th>
```
**Tại sao:** `*ngIf` = false thì phần tử **không được tạo** trong trang → Giảng viên không thấy form, nút Sửa/Xóa.

**Chống sửa role trong localStorage** (`services/auth.ts` – `getRole()`)
```ts
const payloadBase64 = token.split('.')[1];
const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
const payload = JSON.parse(decodeURIComponent(escape(decodedJson)));
const tokenRole = payload['role'] || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';

const localRole = localStorage.getItem('role');
if (localRole && tokenRole && localRole !== tokenRole) {
  localStorage.setItem('role', tokenRole);
  this.toastService.showError('Không thể truy cập dưới quyền Admin.');
}
return tokenRole;
```

| Câu lệnh | Tại sao có |
|---|---|
| `split('.')[1]` | Lấy phần Payload (phần giữa) của JWT |
| `replace(-→+, _→/)` | JWT dùng **Base64URL**; `atob` chỉ hiểu Base64 thường |
| `decodeURIComponent(escape(...))` | Giải mã đúng tiếng Việt (UTF-8) trong FullName |
| Tên claim dài `http://schemas.microsoft.com/.../role` | .NET ghi `ClaimTypes.Role` vào token bằng tên đầy đủ này |
| So `localRole` với `tokenRole` | Role trong localStorage **sửa được** bằng F12, role trong token **không sửa được** (sửa là hỏng chữ ký) → luôn tin token |

---

## 2.3. Audit Log và trang Lịch sử

### ① Yêu cầu
Ghi lại **ai** đã Thêm / Sửa / Xóa **gì**, **lúc nào**, giá trị **cũ** và **mới**; Admin xem được.

### ② Cơ chế
Mỗi thao tác thay đổi dữ liệu sinh ra một dòng trong bảng `AuditLogs`. API `GET /api/AuditLog` đọc bảng này (mới nhất trước) và trả cho trang `/lich-su`. *(Việc ghi log được làm tự động ở Tuần 3-4.)*

### ③ Cách hiểu
Như **sổ giao ca** ở cơ quan: mỗi lần ai thay đổi gì đều ký tên, ghi giờ, ghi "trước – sau".

### ④ Code và "tại sao"
`Models/AuditLog.cs`
```csharp
public class AuditLog
{
    public int Id { get; set; }
    [MaxLength(100)] public string Username { get; set; } = "Anonymous";
    [MaxLength(20)]  public string Action { get; set; } = string.Empty;    // Thêm / Sửa / Xóa
    [MaxLength(50)]  public string TableName { get; set; } = string.Empty;
    public string? OldValues { get; set; }   // JSON
    public string? NewValues { get; set; }   // JSON
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `[MaxLength(...)]` | Khớp độ dài cột trong DB (`NVARCHAR(100)`...) |
| `= "Anonymous"` | Thao tác không có người đăng nhập (ví dụ tự đăng ký) vẫn có giá trị |
| `OldValues`/`NewValues` kiểu chuỗi JSON | Mỗi bảng có cột khác nhau; lưu dạng JSON thì **một bảng log dùng cho mọi bảng** |
| `string?` | Thêm mới thì không có giá trị cũ; xóa thì không có giá trị mới |

`AuditLogController.GetAll`
```csharp
var logs = await _context.AuditLogs
    .AsNoTracking()
    .OrderByDescending(l => l.Timestamp)
    .Select(l => new AuditLogDto { Id = l.Id, Username = l.Username, Action = l.Action, TableName = l.TableName, OldValues = l.OldValues, NewValues = l.NewValues, Timestamp = l.Timestamp })
    .ToListAsync();
return Ok(logs);
```
**Tại sao:** `AsNoTracking` vì chỉ đọc; `OrderByDescending` để việc mới nhất lên đầu; `Select` sang `AuditLogDto` theo đúng nguyên tắc DTO của Tuần 1.

`lich-su/lich-su.ts`: `ngOnInit` → chỉ gọi `taiLichSu()` khi `typeof window !== 'undefined'` (tránh gọi API lúc render trên server); lỗi thì dùng `layThongBaoLoi(err)` + `detectChanges()`.

---

## 2.4. Upload ảnh đại diện

### ① Yêu cầu
Cho phép tải ảnh đại diện có kiểm tra định dạng, dung lượng; hiện tiến trình tải.

### ② Cơ chế
- Angular gửi file dạng **`multipart/form-data`** (định dạng chuyên để gửi file qua HTTP).
- ASP.NET nhận file thành đối tượng **`IFormFile`**.
- File được lưu vào thư mục `wwwroot/avatars`; **DB chỉ lưu đường dẫn**. `app.UseStaticFiles()` cho phép trình duyệt tải thẳng file trong `wwwroot` qua URL `/avatars/...`.
- Angular bật `reportProgress` để nhận các sự kiện "đã gửi được bao nhiêu byte".

### ③ Cách hiểu
Như **gửi hàng qua bưu điện**: nhân viên kiểm tra loại hàng và cân nặng trước (đuôi file, ≤ 2MB), dán **mã vận đơn mới** (tên Guid) chứ không dùng tên người gửi tự ghi, cất hàng vào kho (`wwwroot`) và chỉ ghi **mã kệ** vào sổ (DB).

### ④ Code và "tại sao"
`SinhVienController.UploadAvatar` (phần kiểm tra và lưu)
```csharp
var sinhVien = await _context.SinhVien.FindAsync(id);
if (sinhVien == null) throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");

if (file == null || file.Length == 0) throw new BadRequestException("Vui lòng chọn một file ảnh!");

var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
var extension = Path.GetExtension(file.FileName).ToLower();
if (!allowedExtensions.Contains(extension))
    throw new BadRequestException("Định dạng file không hợp lệ! ...");

if (file.Length > 2 * 1024 * 1024)
    throw new BadRequestException("Dung lượng file quá lớn! ...");

var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

var uniqueFileName = $"{Guid.NewGuid()}{extension}";
var filePath = Path.Combine(uploadsFolder, uniqueFileName);
using (var stream = new FileStream(filePath, FileMode.Create))
{
    await file.CopyToAsync(stream);
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `IFormFile file` (tham số) | Kiểu ASP.NET dùng để nhận file; tên `file` phải trùng tên Angular `formData.append('file', ...)` |
| Kiểm tra sinh viên **trước** | Không có sinh viên thì không lưu file làm gì |
| `file.Length == 0` | Chặn file rỗng |
| `Path.GetExtension(...).ToLower()` | Lấy đuôi file; `ToLower` để `.PNG` cũng hợp lệ |
| Danh sách đuôi cho phép (**whitelist**) | Chỉ cho phép thứ biết là an toàn, thay vì cố liệt kê thứ bị cấm |
| `2 * 1024 * 1024` | 2 MB tính bằng byte; viết dạng phép nhân cho dễ đọc |
| `_env.WebRootPath` | Đường dẫn thật tới `wwwroot` trên máy chủ – không viết cứng `D:\...` |
| `Path.Combine` | Ghép đường dẫn đúng dấu `\` / `/` theo hệ điều hành |
| `Directory.CreateDirectory` | Lần đầu chưa có thư mục thì tạo |
| `Guid.NewGuid()` làm tên | Không trùng, không ghi đè ảnh người khác, không đoán được, không dùng tên client gửi (có thể chứa `../`, ký tự lạ) |
| `using (var stream = ...)` | Đảm bảo file được **đóng** sau khi ghi, kể cả khi lỗi |
| `CopyToAsync` | Ghi file bất đồng bộ (I/O) |

`services/sinh-vien.ts` (Angular)
```ts
uploadAvatar(id: number, file: File): Observable<any> {
  const formData = new FormData();
  formData.append('file', file);
  return this.http.post(`${this.apiUrl}/upload-avatar/${id}`, formData, {
    reportProgress: true,
    observe: 'events'
  });
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `FormData` | Đối tượng trình duyệt dùng để đóng gói file thành `multipart/form-data` |
| `reportProgress: true` | Bật sự kiện tiến trình |
| `observe: 'events'` | Nhận **mọi sự kiện** (không chỉ kết quả cuối) |

Trong component: `HttpEventType.UploadProgress` → `Math.round(100 * event.loaded / event.total)` để hiện %; `HttpEventType.Response` → xong, tải lại danh sách và hiện Toast.

---

## 2.5. Chuẩn hóa lỗi + Toast

### ① Yêu cầu
Lỗi hiển thị thống nhất bằng thông báo nổi (Toast); hết phiên thì về trang đăng nhập.

### ② Cơ chế
`errorInterceptor` đứng giữa **mọi** response trả về. Khi response lỗi, RxJS `catchError` bắt được → hiện Toast → điều hướng theo mã lỗi → **ném tiếp** lỗi để component vẫn biết request thất bại.
`ToastService` dùng **RxJS `Subject`**: service "phát" thông báo, `ToastComponent` (đặt một lần ở `app.html`) "nghe" và hiển thị.

### ③ Cách hiểu
Interceptor như **tổng đài viên** nhận mọi cuộc gọi báo sự cố: thông báo cho người dùng một cách thống nhất, cuộc nào "hết hạn thẻ" thì mời ra quầy đăng nhập lại. `Subject` như **loa phát thanh**: ai bật loa (subscribe) thì nghe thấy.

### ④ Code và "tại sao"
`interceptors/error.ts`
```ts
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

| Câu lệnh | Tại sao có |
|---|---|
| `next(req).pipe(catchError(...))` | Cho request đi, rồi "bắt" nếu response là lỗi |
| `typeof window === 'undefined'` → bỏ qua | Đang render trên server thì không có giao diện để hiện Toast |
| `console.error('[API details]', ...)` | Chỉ khi backend gửi `details` (môi trường Development) → in ra Console để debug. **Dòng đỏ trong Console là cố ý, không phải lỗi mới** |
| `laApiXacThuc` | Trang đăng nhập/đăng ký đã có banner lỗi riêng → không Toast trùng; và sai mật khẩu (401 ở trang login) không bị "đăng xuất" vô lý |
| 401 → `logout()` + về `/login` | Token hết hạn / hỏng → xóa token cũ, mời đăng nhập lại |
| 403 → về `/sinh-vien` | Không đủ quyền → đưa về trang được phép |
| `return throwError(() => error)` | **Ném tiếp** để component tắt loading, giữ form, hoặc xử lý riêng (ví dụ 409 đụng độ ở Tuần 7) |

`services/toast.ts`
```ts
toastState = new Subject<ToastMessage>();
showError(msg: string) { this.emit({ message: msg, type: 'error' }); }

private emit(toast: ToastMessage) {
  if (this.toastState.observed) {
    this.toastState.next(toast);
    sessionStorage.removeItem('flashToast');
  } else {
    sessionStorage.setItem('flashToast', JSON.stringify(toast));
  }
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `Subject` | Một nơi phát – nhiều nơi nghe; service không cần biết component nào hiển thị |
| `showSuccess / showError / showWarning` | Ba loại màu thông báo |
| `toastState.observed` | Kiểm tra **có ai đang nghe** không |
| `sessionStorage` (`flashToast`) | Lúc đang chuyển trang chưa có ai nghe → cất tạm, trang mới mở ra sẽ đọc và hiện → thông báo **không bị mất** |

---

# TUẦN 3-4 – XỬ LÝ LỖI TẬP TRUNG, AUDIT TỰ ĐỘNG, DỌN FILE

## 3.1. Dứt điểm Exception Handling

### ① Yêu cầu
Mọi lỗi của API trả về **cùng một định dạng JSON** `{ statusCode, message, details }`, và Angular đọc được định dạng đó.
Trước đây: mỗi chỗ trả lỗi một kiểu (`NotFound()` body rỗng, `BadRequest("chuỗi")`, object khác tên trường), lỗi bất ngờ thì lộ stack trace.

### ② Cơ chế
1. **Phân loại lỗi bằng exception riêng.** Code nghiệp vụ gặp lỗi chỉ việc `throw new NotFoundException(...)`. Mỗi exception mang sẵn mã HTTP.
2. **Middleware bắt lỗi đặt ĐẦU TIÊN pipeline.** Middleware là "lớp vỏ" bọc quanh mọi thứ phía sau. Nó gọi `await _next(context)` trong `try`; exception ném ra ở **bất kỳ tầng nào phía sau** (Controller, Service, EF) đều "nổi" ngược lên và rơi vào `catch` của nó. Đặt đầu tiên thì nó bọc được nhiều tầng nhất.
3. **Đổi exception thành JSON** bằng *pattern matching* `switch` theo kiểu exception.
4. **Bắt nốt lỗi không phải exception:** lỗi validation (400) do `[ApiController]` tự trả, và 401/403/404 do hệ thống tự trả **không có body** → cấu hình thêm để cũng ra cùng định dạng.

### ③ Cách hiểu
Như **phòng tiếp nhận khiếu nại duy nhất** ở cửa ra của tòa nhà: phòng ban nào gặp sự cố chỉ cần gửi phiếu có mã loại sự cố (exception). Phòng tiếp nhận viết thư trả lời khách theo **một mẫu thống nhất**, và không bao giờ đưa khách xem biên bản nội bộ (stack trace) – trừ khi là nhân viên nội bộ (môi trường Development).

### ④ Code và "tại sao"
**Bước 1 – Exception riêng** (`Exceptions/AppException.cs`)
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

| Câu lệnh | Tại sao có |
|---|---|
| `abstract class AppException : Exception` | Lớp cha chung → Middleware chỉ cần bắt **một** kiểu `AppException` là xử lý được mọi lỗi nghiệp vụ. `abstract` = không được `new` trực tiếp, phải dùng lớp con cụ thể |
| `int StatusCode { get; }` | Mỗi lỗi **mang sẵn mã HTTP** của nó; chỉ đọc, không sửa được sau khi tạo |
| `protected` constructor | Chỉ lớp con được gọi |
| `: base(message)` | Đưa câu lỗi lên lớp `Exception` gốc → đọc lại bằng `ex.Message` |
| 4 lớp con, mỗi lớp 1 dòng | Thêm loại lỗi mới chỉ cần 1 dòng, Middleware không phải sửa |

**Bước 2 – Khuôn JSON lỗi** (`DTOs/ErrorResponse.cs`)
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

| Câu lệnh | Tại sao có |
|---|---|
| `StatusCode` | Mã lỗi nằm luôn trong body, client đọc dễ |
| `Message` | Câu tiếng Việt cho người dùng |
| `Details` | Stack trace – chỉ có ở Development, để lập trình viên debug |
| `Errors` + `[JsonIgnore(WhenWritingNull)]` | Danh sách lỗi theo từng trường (chỉ có ở lỗi validation); khi `null` thì **không xuất hiện** trong JSON cho gọn |

**Bước 3 – Middleware** (`Middleware/ExceptionMiddleware.cs`)
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
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `RequestDelegate _next` | "Trạm tiếp theo" trong pipeline – Middleware gọi nó để cho request đi tiếp |
| `ILogger` | Ghi log ra Console / file để người vận hành biết có lỗi |
| `IHostEnvironment` | Biết đang chạy Development hay Production |
| `InvokeAsync(HttpContext)` | Tên hàm **bắt buộc** theo quy ước – ASP.NET tự gọi cho mỗi request |
| `try { await _next(context); }` | Bọc toàn bộ phần phía sau; exception từ Controller/Service/EF đều rơi vào đây |
| `Response.HasStarted` → `throw;` | Nếu đã gửi một phần response về client thì **không thể** đổi mã lỗi/ghi đè nữa → chỉ ghi log và ném tiếp |
| `LogWarning` vs `LogError` | Lỗi người dùng (4xx – nhập sai) là **cảnh báo**; lỗi hệ thống (5xx) mới là **lỗi** cần xử lý → log không bị "báo động giả" |
| `ContentType = "application/json"` | Báo client đây là JSON |
| `IsDevelopment() ? StackTrace : null` | **Bảo mật**: stack trace lộ cấu trúc code, tên file → chỉ hiện khi phát triển |
| `exception switch { ... }` | **Pattern matching**: chọn mã và câu lỗi theo **kiểu** exception, gọn hơn nhiều `if` |
| `AppException appEx => (appEx.StatusCode, appEx.Message)` | Lỗi nghiệp vụ dùng đúng mã và câu của nó |
| `UnauthorizedAccessException` → 401 | Exception có sẵn của .NET về quyền truy cập |
| `ArgumentException or BadHttpRequestException` → 400 | Tham số sai / request sai định dạng |
| `DbUpdateConcurrencyException` → 409 | **Lưới an toàn** cho đụng độ dữ liệu (Service Tuần 7 đã tự đổi sang `ConflictException`, dòng này phòng chỗ khác quên bắt) |
| `DbUpdateException` → 409 | Vi phạm ràng buộc DB (ví dụ trùng khóa unique) |
| `_ => 500` | Mọi lỗi còn lại: câu chung, **không lộ** chi tiết |
| `CamelCase` | Xuất `statusCode`, `message` (chữ thường đầu) đúng chuẩn JSON mà Angular đọc |

**Bước 4 – Đăng ký và bắt nốt lỗi không phải exception** (`Program.cs`)
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
...
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
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

| Câu lệnh | Tại sao có |
|---|---|
| `InvalidModelStateResponseFactory` | Lỗi validation **không phải exception** – `[ApiController]` tự trả 400 theo mẫu riêng của Microsoft (ProblemDetails). Ghi đè để trả `ErrorResponse` của mình |
| `ModelState.Where(...).ToDictionary(...)` | Gom lỗi theo từng trường: `{ "Tuoi": ["Tuổi phải là..."] }` |
| `Message = ...FirstOrDefault(...)` | Lấy **câu lỗi đầu tiên** làm `message` → Toast hiện đúng câu cụ thể thay vì câu chung |
| `SuppressMapClientErrors = true` | Tắt việc ASP.NET tự gắn body ProblemDetails cho các mã 4xx → để định dạng của mình không bị trộn |
| `UseMiddleware<ExceptionMiddleware>()` **đầu tiên** | Bọc toàn bộ pipeline |
| `UseStatusCodePages(...)` | 401/403 (do `[Authorize]`) và 404 (sai URL) được trả **không có body**; hàm này chèn JSON vào các response như vậy |

**Bước 5 – Angular đọc** – đã có ở 1.6 (`layThongBaoLoi` đọc `message`) và 2.5 (`errorInterceptor` hiện Toast, in `details`). Model `models/api-error.ts` mô tả kiểu `{ statusCode, message, details?, errors? }` để TypeScript gợi ý đúng tên trường.

---

## 3.2. Audit Logging bằng SaveChangesInterceptor

### ① Yêu cầu
Ghi lịch sử **tự động** bằng `SaveChangesInterceptor`, không viết code ghi log trong từng hàm Controller.
Trước đây: mỗi hàm Thêm/Sửa/Xóa tự gọi lệnh ghi log → dài, dễ quên, sót chỗ.

### ② Cơ chế
- **Interceptor** là "móc" (hook) mà EF Core gọi vào ở những thời điểm nhất định. `SaveChangesInterceptor` được gọi **ngay trước** khi EF ghi xuống DB.
- Lúc đó, **ChangeTracker** của EF biết chính xác: bản ghi nào **thêm** (`Added`), **sửa** (`Modified`), **xóa** (`Deleted`), và với mỗi cột, **giá trị gốc** (`OriginalValue`) lẫn **giá trị mới** (`CurrentValue`), cột nào thật sự đổi (`IsModified`).
- Interceptor đọc thông tin đó, tạo các dòng `AuditLog`, **thêm vào cùng lần lưu** → dữ liệu và log được ghi cùng nhau.
- Tên người thực hiện lấy từ **JWT của request hiện tại** qua `IHttpContextAccessor`.

### ③ Cách hiểu
Như **camera ở cửa kho**: không cần nhắc từng nhân viên ghi sổ, ai mang gì ra vào cửa là camera tự ghi lại – vì mọi thứ đều phải đi qua đúng một cửa (`SaveChanges`).

### ④ Code và "tại sao"
`Data/AuditSaveChangesInterceptor.cs`
```csharp
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HashSet<string> SensitiveProperties = new() { "PasswordHash", "RowVersion" };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context != null) await OnBeforeSaveChanges(context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task OnBeforeSaveChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();
        var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry(entry) { TableName = entry.Metadata.GetTableName() ?? "Unknown", Username = username };
            auditEntries.Add(auditEntry);

            var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            bool isSoftDelete = isDeletedProp != null && Equals(isDeletedProp.CurrentValue, true) /* ... */;

            if (isSoftDelete)                          auditEntry.Action = "Xóa";
            else if (entry.State == EntityState.Added)    auditEntry.Action = "Thêm";
            else if (entry.State == EntityState.Deleted)  auditEntry.Action = "Xóa";
            else if (entry.State == EntityState.Modified) auditEntry.Action = "Sửa";

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                if (SensitiveProperties.Contains(propertyName)) continue;
                if (property.Metadata.IsPrimaryKey()) { auditEntry.KeyValues[propertyName] = property.CurrentValue ?? ""; continue; }

                switch (entry.State)
                {
                    case EntityState.Added:    auditEntry.NewValues[propertyName] = property.CurrentValue ?? ""; break;
                    case EntityState.Deleted:  auditEntry.OldValues[propertyName] = property.OriginalValue ?? ""; break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propertyName] = property.OriginalValue ?? "";
                            auditEntry.NewValues[propertyName] = property.CurrentValue ?? "";
                        }
                        break;
                }
            }
        }
        foreach (var auditEntry in auditEntries) context.Set<AuditLog>().Add(auditEntry.ToAudit());
    }
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `: SaveChangesInterceptor` | Lớp có sẵn của EF; kế thừa rồi ghi đè hàm cần "móc" vào |
| `IHttpContextAccessor` | Interceptor không nằm trong Controller nên không có `User`; dịch vụ này cho phép đọc request hiện tại. Phải đăng ký `AddHttpContextAccessor()` trong Program.cs |
| `static HashSet SensitiveProperties` | Danh sách cột **không ghi vào log**: mật khẩu băm (bảo mật), RowVersion (dữ liệu kỹ thuật gây nhiễu). `HashSet` tra cứu nhanh |
| `override SavingChangesAsync` | Hàm EF gọi **trước** khi lưu (bản "-ing"). Phải là bản Async vì code dùng `SaveChangesAsync` |
| `base.SavingChangesAsync(...)` | Trả quyền lại cho EF để tiếp tục lưu bình thường |
| `DetectChanges()` | Bắt EF quét lại để trạng thái `Added/Modified/Deleted` chính xác trước khi đọc |
| `User.Identity.Name ?? "Anonymous"` | Lấy claim `Name` trong JWT; không đăng nhập (ví dụ đăng ký) thì ghi "Anonymous" |
| `Entries().ToList()` | Chụp danh sách trước khi duyệt; vì lát nữa ta **thêm** AuditLog vào context, duyệt trực tiếp sẽ lỗi "collection was modified" |
| Bỏ qua `AuditLog` | Không ghi log cho chính dòng log (tránh vòng lặp vô tận) |
| Bỏ qua `Detached`, `Unchanged` | Không có thay đổi thì không ghi |
| `Metadata.GetTableName()` | Lấy tên bảng thật trong DB (ví dụ `SinhVien`, `Users`) |
| Kiểm tra `IsDeleted = true` **trước** | Xóa mềm thực chất là **UPDATE** (trạng thái Modified); kiểm tra cờ này để ghi đúng hành động là **"Xóa"** thay vì "Sửa" |
| `IsPrimaryKey()` → `KeyValues` | Thu thập khóa chính riêng (hiện chưa được lưu vào bảng log – điểm có thể cải thiện) |
| `Added` → chỉ `NewValues` | Thêm mới không có giá trị cũ |
| `Deleted` → chỉ `OldValues` | Xóa cứng không có giá trị mới |
| `Modified` + `IsModified` | Chỉ ghi **cột thật sự đổi** → log gọn, đọc là thấy ngay đổi gì |
| `?? ""` | Giá trị `null` ghi thành chuỗi rỗng để JSON không lỗi |
| `context.Set<AuditLog>().Add(...)` | Thêm log vào **cùng** lần SaveChanges → dữ liệu và log cùng thành công hoặc cùng thất bại |

`AuditEntry.ToAudit()`: chuyển `OldValues`/`NewValues` (Dictionary) thành chuỗi JSON bằng `JsonSerializer.Serialize`; dictionary rỗng thì để `null`.

**Đăng ký** (`Program.cs`)
```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    var auditInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
    options.AddInterceptors(auditInterceptor);
});
```

| Câu lệnh | Tại sao có |
|---|---|
| `AddHttpContextAccessor()` | Để interceptor nhận được `IHttpContextAccessor` |
| `AddScoped<AuditSaveChangesInterceptor>()` | Mỗi request một interceptor, cùng vòng đời với DbContext |
| `AddDbContext((serviceProvider, options) => ...)` | Dạng có `serviceProvider` để **xin** interceptor từ DI |
| `options.AddInterceptors(...)` | "Gắn camera" vào DbContext |

---

## 3.3. Xóa file ảnh cũ khi đổi ảnh

### ① Yêu cầu
Khi cập nhật ảnh đại diện, xóa file ảnh cũ để không để lại rác trên ổ cứng.

### ② Cơ chế – thứ tự an toàn
1. Nhớ đường dẫn ảnh cũ.
2. Ghi file mới ra ổ cứng.
3. Cập nhật DB sang đường dẫn mới.
   - **Lưu DB lỗi** → xóa **file mới** vừa ghi (vì DB vẫn trỏ ảnh cũ) → ném tiếp lỗi.
   - **Lưu DB thành công** → **lúc này** mới xóa **file cũ**.

Nếu xóa file cũ **trước** khi lưu DB mà DB lỗi → sinh viên mất ảnh. Thứ tự trên đảm bảo **luôn còn đúng một ảnh hợp lệ**.

### ③ Cách hiểu
Như **chuyển nhà**: dọn đồ sang nhà mới, làm xong giấy tờ đổi địa chỉ, **rồi** mới trả nhà cũ. Giấy tờ trục trặc thì dọn đồ khỏi nhà mới, vẫn còn nhà cũ để ở.

### ④ Code và "tại sao"
`SinhVienController.UploadAvatar` (phần cuối)
```csharp
var oldAvatarUrl = sinhVien.AvatarUrl;
sinhVien.AvatarUrl = $"/avatars/{uniqueFileName}";
try
{
    await _context.SaveChangesAsync();
}
catch
{
    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
    throw;
}

if (!string.IsNullOrEmpty(oldAvatarUrl))
{
    var oldAbsoluteFilePath = Path.Combine(_env.WebRootPath, oldAvatarUrl.TrimStart('/'));
    if (System.IO.File.Exists(oldAbsoluteFilePath)) System.IO.File.Delete(oldAbsoluteFilePath);
}
return Ok(new { AvatarUrl = sinhVien.AvatarUrl });
```

| Câu lệnh | Tại sao có |
|---|---|
| `oldAvatarUrl = sinhVien.AvatarUrl` | Phải nhớ **trước** khi gán đường dẫn mới, nếu không sẽ mất dấu file cũ |
| `try { SaveChangesAsync } catch { ... throw; }` | Lưu DB lỗi → xóa file mới (dọn rác) → `throw;` ném **nguyên lỗi** cho Middleware trả JSON |
| `System.IO.File` (ghi đầy đủ) | Trong Controller, `File(...)` là hàm trả file của ASP.NET → phải ghi rõ để không nhầm |
| `File.Exists` trước `Delete` | Tránh lỗi nếu file đã không còn |
| `IsNullOrEmpty(oldAvatarUrl)` | Sinh viên lần đầu có ảnh thì không có gì để xóa |
| `TrimStart('/')` | DB lưu `/avatars/x.png`; bỏ `/` đầu để `Path.Combine` không hiểu nhầm là đường dẫn gốc ổ đĩa |
| `Path.Combine(_env.WebRootPath, ...)` | Đổi đường dẫn web thành đường dẫn thật trên ổ cứng |

---

# TUẦN 5-6 – XÓA MỀM, TẦNG SERVICE, UNIT TEST

## 4.1. Xóa mềm (Soft Delete) + Global Query Filter

### ① Yêu cầu
Xóa sinh viên thì **không xóa khỏi DB**, chỉ đánh dấu; mọi truy vấn tự động ẩn bản ghi đã đánh dấu. Phạm vi: **chỉ áp dụng cho Sinh viên**, không áp dụng cho tài khoản.
Trước đây: `Remove()` → `DELETE` → mất vĩnh viễn, không khôi phục được, mất lịch sử.

### ② Cơ chế
1. Thêm cột cờ `IsDeleted`. "Xóa" = đổi cờ thành `true` → EF sinh **`UPDATE`**, không phải `DELETE`.
2. **Global Query Filter**: khai báo **một lần** trong `OnModelCreating`. Từ đó, **mọi** câu LINQ trên `SinhVien` (`ToList`, `Count`, `Any`, `Find`, `Where`…) đều được EF **tự gắn thêm** `WHERE IsDeleted = 0` khi dịch sang SQL.
3. Khi thật sự cần thấy dòng đã xóa: `.IgnoreQueryFilters()` tắt bộ lọc cho **riêng** câu truy vấn đó.

### ③ Cách hiểu
Như **Thùng rác trên máy tính**: xóa file thì file chỉ chuyển vào thùng rác (đổi cờ), File Explorer mặc định **không hiện** thùng rác (query filter); muốn xem thì mở riêng thùng rác (`IgnoreQueryFilters`).

### ④ Code và "tại sao"
**DB** (SSMS):
```sql
ALTER TABLE SinhVien ADD IsDeleted BIT NOT NULL DEFAULT 0;
```
| Phần | Tại sao |
|---|---|
| `BIT` | Kiểu đúng/sai của SQL Server (C# là `bool`) |
| `NOT NULL DEFAULT 0` | Các dòng **đã có sẵn** tự nhận "chưa xóa"; dòng mới không cần gán |

**Model** (`Models/SinhVien.cs`): `public bool IsDeleted { get; set; } = false;`

**Bộ lọc** (`Data/AppDbContext.cs`)
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Global Query Filter: chỉ áp dụng xóa mềm cho Sinh viên (tài khoản không xóa mềm)
    modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted);
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `override OnModelCreating` | Hàm EF gọi **một lần** khi dựng mô hình dữ liệu – nơi cấu hình bảng, khóa, bộ lọc |
| `base.OnModelCreating(...)` | Giữ cấu hình mặc định của lớp cha |
| `Entity<SinhVien>()` | Chỉ cấu hình cho bảng SinhVien |
| `HasQueryFilter(s => !s.IsDeleted)` | "Chỉ lấy dòng chưa xóa" – áp dụng **tự động ở mọi nơi** |
| Không có filter cho `NguoiDung` | Quyết định phạm vi: tài khoản không xóa mềm (thuộc tính `IsDeleted` đã gỡ khỏi Model `NguoiDung`) |

**Xóa** (`SinhVienService.DeleteAsync`)
```csharp
public async Task DeleteAsync(int id)
{
    var sv = await TimHoacBaoLoiAsync(id);
    sv.IsDeleted = true; // xóa mềm
    await _context.SaveChangesAsync();
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `TimHoacBaoLoiAsync(id)` | Tìm; không có thì 404. Vì `FindAsync` **cũng bị filter**, xóa lần hai một sinh viên đã xóa sẽ nhận 404 – đúng mong đợi |
| `sv.IsDeleted = true` | Thay cho `Remove(sv)` cũ |
| `SaveChangesAsync()` | EF thấy entity bị **sửa** → `UPDATE SinhVien SET IsDeleted = 1 WHERE Id = ...`; Interceptor ghi log hành động **"Xóa"** |

**Angular không phải sửa gì**: vẫn gọi `DELETE /api/SinhVien/5`, vẫn nhận 204. Cách xóa đổi **bên trong** backend, "hợp đồng" API giữ nguyên.

---

## 4.2. Tách tầng Service + Unit Test

### ① Yêu cầu
Đưa nghiệp vụ ra khỏi Controller sang tầng Service, và viết Unit Test cho Service.
Trước đây: Controller "béo" – vừa xử lý HTTP vừa chứa nghiệp vụ → khó đọc, khó test (muốn test logic email trùng phải dựng cả HTTP, JWT…).

### ② Cơ chế
**Chia trách nhiệm (Separation of Concerns):**

| Tầng | Lo việc gì | Không làm |
|---|---|---|
| Controller | Nhận HTTP, `[Authorize]`, gọi Service, trả mã HTTP | Không chứa nghiệp vụ |
| **Service** | Kiểm tra, tính toán, đọc/ghi DB, **ném exception** khi sai | **Không biết gì về HTTP** |
| DbContext | Nói chuyện với DB | Không chứa nghiệp vụ |

Vì Service **không phụ thuộc HTTP**, trong test chỉ cần `new SinhVienService(db)` với một DB giả (**EF Core InMemory** – DB nằm trong RAM) rồi gọi hàm như hàm C# bình thường → đó là **Unit Test**.

### ③ Cách hiểu
Nhà hàng: **phục vụ** (Controller) nhận order, đưa món; **bếp** (Service) nấu. Muốn thử món mới, chỉ cần vào bếp nấu thử (unit test) – không cần mở cửa đón khách.

### ④ Code và "tại sao"
**Service** (`Services/SinhVienService.cs`)
```csharp
public class SinhVienService
{
    private readonly AppDbContext _context;
    public SinhVienService(AppDbContext context) => _context = context;

    public async Task<PagedResult<SinhVienDto>> GetAllAsync(SinhVienQuery query) { ... }   // xem 2.1
    public async Task<SinhVienDto> GetByIdAsync(int id) { var sv = await TimHoacBaoLoiAsync(id); return ToDto(sv); }

    public async Task<SinhVienDto> CreateAsync(SinhVienDto dto)
    {
        if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower()))
            throw new ConflictException("Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác.");
        var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
        _context.SinhVien.Add(sv);
        await _context.SaveChangesAsync();
        return ToDto(sv);
    }

    public async Task UpdateAsync(int id, SinhVienDto dto) { ... }   // xem 5.3
    public async Task DeleteAsync(int id) { ... }                     // xem 4.1

    private async Task<SinhVien> TimHoacBaoLoiAsync(int id) =>
        await _context.SinhVien.FindAsync(id)
        ?? throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");

    private static SinhVienDto ToDto(SinhVien s) => new()
    { Id = s.Id, HoTen = s.HoTen, Email = s.Email, Tuoi = s.Tuoi, AvatarUrl = s.AvatarUrl, RowVersion = s.RowVersion };
}
```

| Câu lệnh | Tại sao có |
|---|---|
| Constructor nhận `AppDbContext` | **DI**: chạy thật nhận DB SQL Server; test nhận DB InMemory – Service không cần sửa |
| `Email.ToLower() == dto.Email.ToLower()` | Email không phân biệt hoa thường: `A@X.COM` trùng `a@x.com` |
| `throw new ConflictException` | Service **không trả mã HTTP**, chỉ nói "lỗi gì"; Middleware đổi thành 409 |
| `return ToDto(sv)` sau khi lưu | Lúc này `sv.Id` đã có (DB vừa sinh) → Controller dùng Id để trả `201 Created` kèm đường dẫn |
| `TimHoacBaoLoiAsync` (private) | GetById, Update, Delete đều cần "tìm, không có thì 404" → viết **một lần** |
| `?? throw` | Nếu `FindAsync` trả `null` thì ném lỗi ngay trong một dòng |
| `ToDto` (private static) | Đổi Entity → DTO ở một chỗ; `static` vì không dùng dữ liệu của đối tượng |

**Controller mỏng** (`Controllers/SinhVienController.cs`)
```csharp
[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
{
    var created = await _service.CreateAsync(sinhVienDto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
}

[HttpPut("{id}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Update(int id, SinhVienDto sinhVienDto)
{
    await _service.UpdateAsync(id, sinhVienDto);
    return NoContent();
}
```

| Câu lệnh | Tại sao có |
|---|---|
| Mỗi hàm 1–2 dòng | Controller chỉ lo HTTP; không `if`, không `try/catch` (Middleware lo lỗi) |
| `CreatedAtAction(nameof(GetById), ...)` | Chuẩn REST: tạo mới trả **201** kèm header `Location` trỏ tới `/api/SinhVien/{id}` |
| `NoContent()` | Sửa / xóa thành công không cần trả dữ liệu → **204** |
| `_context` vẫn còn trong Controller | Chỉ dùng cho `UploadAvatar` (làm việc với file, `wwwroot` – phần hạ tầng web) |

**Đăng ký** (`Program.cs`): `builder.Services.AddScoped<QuanLySinhVien.Services.SinhVienService>();`
**Tại sao `Scoped`:** Service dùng `AppDbContext` (Scoped). Service phải sống **ngắn bằng hoặc ngắn hơn** thứ nó dùng; nếu Singleton sẽ giữ một DbContext đã hết hạn → lỗi.

**Unit Test** (`QuanLySinhVien.Tests/SinhVienServiceTests.cs`)
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

| Câu lệnh | Tại sao có |
|---|---|
| `UseInMemoryDatabase(...)` | DB trong RAM – không cần SQL Server, chạy rất nhanh |
| Tên `Guid.NewGuid()` | Mỗi test **một DB riêng** → test này không làm bẩn dữ liệu test kia |
| 3 sinh viên chọn **có chủ ý** | Tên, email, tuổi được sắp sao cho mỗi kiểu sắp xếp cho ra **người đứng đầu khác nhau** → kiểm tra được từng nhánh |
| `RowVersion = { 1 }` | InMemory không tự sinh rowversion như SQL Server → gán sẵn để test Update qua được bước kiểm tra (Tuần 7) |
| `ChangeTracker.Clear()` | Quên các đối tượng vừa thêm → Service phải đọc lại từ DB, giống tình huống thật |

```csharp
[Theory]
[InlineData("hoten", false, 2)]   // An
[InlineData("hoten", true, 3)]    // Cuong
...
[InlineData(null, false, 1)]
public async Task GetAll_SapXep_DungThuTu(string? sortBy, bool desc, int idDauTien)
{
    var service = new SinhVienService(TaoDb());                                              // Arrange
    var result = await service.GetAllAsync(new SinhVienQuery { SortBy = sortBy, IsDescending = desc });  // Act
    Assert.Equal(idDauTien, result.Items[0].Id);                                             // Assert
}

[Fact]
public async Task Create_EmailTrung_KhongPhanBietHoaThuong_NemConflict()
{
    await Assert.ThrowsAsync<ConflictException>(() => new SinhVienService(TaoDb())
        .CreateAsync(new SinhVienDto { HoTen = "X", Email = "A@X.COM", Tuoi = 19 }));
}

[Fact]
public async Task Delete_LaXoaMem()
{
    var db = TaoDb();
    await new SinhVienService(db).DeleteAsync(1);
    var sv = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == 1);
    Assert.True(sv.IsDeleted);
    Assert.Equal(2, await db.SinhVien.CountAsync());
}
```

| Thành phần | Tại sao có |
|---|---|
| `[Fact]` | Một test cố định |
| `[Theory]` + `[InlineData]` | Một hàm test chạy **nhiều lần** với nhiều bộ dữ liệu → 9 dòng phủ hết 9 nhánh `switch` sắp xếp |
| **Arrange – Act – Assert** | Chuẩn bị → Hành động → Kiểm tra: mỗi test đọc là hiểu |
| `Assert.Equal(mongDoi, thucTe)` | Sai thì test đỏ, hiện cả hai giá trị |
| `Assert.ThrowsAsync<ConflictException>` | Kiểm tra "**phải** ném đúng loại lỗi này" – không ném hoặc ném loại khác đều đỏ |
| `IgnoreQueryFilters()` trong `Delete_LaXoaMem` | Chứng minh dòng **vẫn còn** trong DB… |
| `CountAsync() == 2` | …nhưng truy vấn thường **không thấy** nó (filter hoạt động) |

Danh sách test hiện có: **21 ca** (GetAll 10, GetById 2, Create 2, Update 5, Delete 2).

**Test Middleware** (`ExceptionMiddlewareTests.cs`, 5 ca) dùng **Moq** để làm giả `ILogger` và `IHostEnvironment`:
```csharp
_mockEnv.Setup(m => m.EnvironmentName).Returns("Production");
RequestDelegate next = _ => throw new NotFoundException("Không tìm thấy sinh viên có Id = 99!");
var middleware = new ExceptionMiddleware(next, _mockLogger.Object, _mockEnv.Object);
var context = new DefaultHttpContext();
context.Response.Body = new MemoryStream();
await middleware.InvokeAsync(context);
Assert.Equal(404, context.Response.StatusCode);
```

| Câu lệnh | Tại sao có |
|---|---|
| `Mock<IHostEnvironment>` + `Setup(...).Returns("Production")` | Giả lập môi trường mà không cần chạy server thật → kiểm tra `details = null` ở Production |
| `RequestDelegate next = _ => throw ...` | Giả "trạm phía sau" ném lỗi để xem Middleware bắt thế nào |
| `DefaultHttpContext` + `MemoryStream` | HttpContext giả, ghi response vào bộ nhớ để đọc lại JSON |

---

# TUẦN 7 – KIỂM THỬ NÂNG CAO, CHẤT LƯỢNG CODE, ĐỤNG ĐỘ DỮ LIỆU

## 5.1. Integration Test với TestContainers

### ① Yêu cầu
Viết ít nhất 2 kịch bản gọi API **từ Controller xuyên xuống tận DB** và pass, dùng TestContainers.
Vấn đề của unit test: chỉ kiểm tra Service, trên DB giả; **không** kiểm tra được routing, JWT, `[Authorize]`, validation, Middleware, và hành vi riêng của SQL Server (rowversion, ràng buộc).

### ② Cơ chế
- **`WebApplicationFactory<Program>`**: chạy **toàn bộ** ứng dụng (đúng `Program.cs`: DI, middleware, JWT, controller) **trong bộ nhớ** của tiến trình test và cho một `HttpClient` gọi vào – không cần mở cổng, không cần F5.
- **TestContainers**: ra lệnh cho **Docker** bật một container **SQL Server 2022 thật**, sạch, cổng ngẫu nhiên; test xong tự xóa.
- **Ghi đè cấu hình**: chuỗi kết nối trong `appsettings.json` bị thay bằng chuỗi kết nối của container (nhờ Tuần 1 không hardcode).
- Test gửi **HTTP thật** → request đi qua đủ: Middleware → JWT → `[Authorize]` → validation → Controller → Service → EF → SQL Server trong Docker.

### ③ Cách hiểu
Unit test là **kiểm tra từng linh kiện** (động cơ, phanh). Integration test là **lắp cả chiếc xe, chạy thử trên đường thật** – nhưng đường này được **dựng tạm cho riêng buổi thử** (container) và **dỡ đi khi thử xong**, không chạy trên đường phố thật (DB `QLSINHVIEN`).

### ④ Code và "tại sao"
**`Program.cs` – dòng cuối**
```csharp
public partial class Program { }
```
**Tại sao:** `Program.cs` viết kiểu *top-level statements* (không có `class Program`). Trình biên dịch tự sinh lớp `Program` nhưng ở dạng `internal`. `WebApplicationFactory<Program>` nằm ở **project test** cần nhìn thấy nó. `partial` = "phần bổ sung của lớp tự sinh", `public` = cho project khác thấy.

**`IntegrationTests/ApiFactory.cs`**
```csharp
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
    }

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
}
```

| Câu lệnh | Tại sao có |
|---|---|
| Tên `ApiFactory` | "Nhà máy sản xuất API cho test" – kế thừa `WebApplicationFactory` (mẫu thiết kế Factory) |
| `: WebApplicationFactory<Program>` | Dựng toàn bộ API trong bộ nhớ, bắt đầu từ `Program` |
| `IAsyncLifetime` | Bật container và tạo bảng là việc **bất đồng bộ**, không làm trong constructor được; interface này cho xUnit gọi `InitializeAsync` trước và `DisposeAsync` sau |
| `new MsSqlBuilder("...2022-latest").Build()` | **Khai báo** container (chưa bật). Ghi rõ image để lần nào chạy cũng cùng phiên bản |
| `override ConfigureWebHost` | Chỗ được phép chỉnh cấu hình API **trước khi** nó khởi động |
| `UseSetting("ConnectionStrings:DefaultConnection", ...)` | **Ghi đè** chuỗi kết nối bằng chuỗi của container (cổng ngẫu nhiên, mật khẩu tự sinh) |
| `_db.StartAsync()` | Kéo image (lần đầu) + bật SQL Server + đợi sẵn sàng |
| `Services.CreateScope()` | `AppDbContext` là Scoped; ngoài request phải tự tạo scope mới xin được |
| Chốt `Contains("QLSINHVIEN")` | Nếu ghi đè thất bại, **dừng ngay** trước khi kịp ghi rác vào DB thật |
| `EnsureCreatedAsync()` | Container mới tinh chưa có bảng → tạo bảng theo các Model C# |
| `DisposeAsync` → `_db.DisposeAsync()` | Xóa container khi xong. `new` vì lớp cha cũng có hàm cùng tên |

**`IntegrationTests/SinhVienApiTest.cs` – helper**
```csharp
public class SinhVienApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SinhVienApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> TaoClientAsync(string role)
    {
        var username = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/XacThuc/dangky",
            new DangKyDto { Username = username, Password = "123456", FullName = "Test" })).EnsureSuccessStatusCode();

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

    private static SinhVienDto SinhVienMoi() => new()
    { HoTen = "Nguyễn Văn Test", Email = $"{Guid.NewGuid():N}@test.com", Tuoi = 20 };
```

| Câu lệnh | Tại sao có |
|---|---|
| `IClassFixture<ApiFactory>` | xUnit tạo **một** `ApiFactory` cho cả lớp → chỉ bật **một** container cho mọi test (bật container mất vài chục giây) |
| Constructor nhận `ApiFactory` | xUnit tự đưa fixture vào |
| Username `Guid` | Mỗi lần gọi một tài khoản mới, không bị "Tài khoản đã tồn tại" |
| Đăng ký qua **API thật** | Đi đúng đường người dùng (BCrypt, validation…) |
| `EnsureSuccessStatusCode()` | Nếu bước chuẩn bị hỏng thì báo lỗi ngay tại đây, dễ tìm nguyên nhân |
| Sửa `Role` trực tiếp trong DB | API đăng ký luôn cho `GiangVien`; muốn có Admin phải nâng trong DB. Làm **trước** khi đăng nhập vì role được ghi vào token lúc đăng nhập |
| Lấy `token` từ JSON | Đây là JWT thật, có chữ ký thật |
| `DefaultRequestHeaders.Authorization` | Mọi request sau của client này đều mang token – giống `jwtInterceptor` bên Angular |
| Email `Guid` trong `SinhVienMoi()` | Các test dùng **chung một DB**; email cố định sẽ làm test sau bị 409 "email trùng" oan |

**Các kịch bản** (6 – yêu cầu tối thiểu 2)

| Kịch bản | Tầng được chứng minh | Kỳ vọng |
|---|---|---|
| `TaoSinhVien_RoiDocLai_DungDuLieu` | JWT → Controller → Service → SQL | 201, đọc lại đúng |
| `TaoSinhVien_EmailTrung_Tra409` | Service ném lỗi → **Middleware** | 409 + JSON chuẩn |
| `XoaSinhVien_LaXoaMem` | **Query Filter** trên SQL thật | 204 → 404, DB vẫn còn `IsDeleted = true` |
| `PhanQuyen_401Va403` | **JWT + `[Authorize(Roles)]`** | 401 / 403 |
| `TaoSinhVien_TuoiKhongHopLe_Tra400` | **Validation** + `InvalidModelStateResponseFactory` | 400 + câu tiếng Việt |
| `HaiNguoiCungSua_NguoiSauBi409` | **RowVersion** trên SQL Server thật | 204 rồi 409 |

Mỗi kịch bản viết theo Arrange – Act – Assert; kiểm tra cả **mã HTTP** lẫn **nội dung** (JSON, dữ liệu trong DB).

---

## 5.2. Code Coverage ≥ 80% cho tầng Service

### ① Yêu cầu
Đo độ phủ test của tầng Service, đạt tối thiểu 80%.
Vấn đề: có nhiều test nhưng không biết **còn nhánh nào chưa được test**.

### ② Cơ chế
- **Coverlet** chèn **bộ đếm** vào từng dòng, từng nhánh của DLL dự án trước khi test chạy (*instrumentation*). Dòng nào được thực thi thì bộ đếm tăng. Kết quả ghi ra `coverage.cobertura.xml`.
- **ReportGenerator** đọc XML, vẽ báo cáo HTML: **xanh** = đã chạy, **đỏ** = chưa chạy, **vàng** = nhánh mới đi một phía.
- **Line coverage** đếm dòng; **Branch coverage** đếm nhánh rẽ (`if`, `switch`, `??`) – khắt khe hơn.
- Integration test cũng được tính, vì `WebApplicationFactory` chạy API **trong cùng tiến trình** với test.

### ③ Cách hiểu
Như **đội kiểm tra đi qua các phòng** của tòa nhà: coverage 85% = đã bước vào 85% số phòng. Nó chỉ ra **phòng nào chưa ai vào** (rủi ro), nhưng không đảm bảo phòng đã vào là an toàn – điều đó phụ thuộc vào việc kiểm tra kỹ hay không (các `Assert`).

### ④ Lệnh và "tại sao"
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```

| Thành phần | Tại sao có |
|---|---|
| Gói `coverlet.collector` (có sẵn trong project test) | Công cụ thu thập độ phủ |
| `--collect:"XPlat Code Coverage"` | Bật Coverlet khi chạy test ("XPlat" = chạy được trên mọi hệ điều hành) |
| `--results-directory .\TestResults` | Gom kết quả về một chỗ dễ tìm |
| `reportgenerator` (cài bằng `dotnet tool install -g dotnet-reportgenerator-globaltool`) | Đổi XML khó đọc thành HTML |
| `**` trong `-reports` | Mỗi lần chạy, Coverlet tạo thư mục tên Guid mới → tìm trong mọi thư mục con |
| `-classfilters:"+QuanLySinhVien.Services.*"` | `+` = **chỉ giữ** namespace Services: yêu cầu tính cho tầng nghiệp vụ, không để Program.cs, Model, DTO làm loãng con số |

**Kết quả lần đo gần nhất:** `SinhVienService` – Line **100%** (81/81), Branch **96,8%** (31/32). *(Đo trước lần sửa cuối của Service – nên bật Docker, xóa `TestResults`/`CoverageReport` cũ rồi đo lại.)*

**Test nào phủ phần nào:** 9 nhánh sắp xếp ← `[Theory]` 9 bộ dữ liệu; chặn phân trang ← `GetAll_TimKiem_VaChanPhanTrangSai`; các nhánh 400/404/409 ← các test `..._NemBadRequest/NotFound/Conflict`; khối `catch (DbUpdateConcurrencyException)` ← integration test `HaiNguoiCungSua_NguoiSauBi409` (cần SQL Server thật).

---

## 5.3. Optimistic Concurrency với RowVersion

### ① Yêu cầu
Hai người cùng sửa một sinh viên thì người lưu sau phải bị chặn, không âm thầm ghi đè.
Vấn đề **Lost Update**: A và B cùng mở form; A lưu đổi tên; B lưu đổi tuổi – nhưng form của B vẫn chứa **tên cũ** → tên A vừa sửa bị ghi đè mất, không ai biết.

### ② Cơ chế
- **Optimistic (lạc quan)**: không khóa dữ liệu khi mở form; **chỉ kiểm tra lúc lưu**. (Ngược lại, *Pessimistic* khóa dòng khi mở – không hợp với web vì người dùng có thể mở form rất lâu.)
- Cột **`rowversion`**: số 8 byte, **SQL Server tự tăng mỗi lần dòng bị UPDATE** – như "số phiên bản" của dòng.
- Client giữ phiên bản lúc **mở form**, gửi lại khi lưu. EF sinh câu:
  ```sql
  UPDATE SinhVien SET HoTen=..., Email=..., Tuoi=...
  WHERE Id = 5 AND RowVersion = <phiên bản lúc mở form>;
  ```
  - Khớp → cập nhật 1 dòng → thành công, SQL tự tăng phiên bản.
  - Đã có người lưu trước → không khớp → **0 dòng** → EF ném `DbUpdateConcurrencyException`.
- So sánh và ghi nằm trong **cùng một câu SQL** → không có khe hở giữa "đọc" và "ghi" (**nguyên tử**).

### ③ Cách hiểu
Như **sửa chung một file tài liệu có số phiên bản**: bạn mở bản v7 để sửa; khi nộp, hệ thống hỏi "bản gốc của bạn là v7, trên máy chủ còn là v7 không?". Nếu đồng nghiệp đã nộp v8 trước, bản của bạn bị từ chối, bạn phải mở v8 rồi sửa lại – thay vì đè mất phần của đồng nghiệp.

### ④ Code và "tại sao"
**DB**: `ALTER TABLE SinhVien ADD RowVersion ROWVERSION;` – SQL Server tự gán và tự tăng; không dùng cột ngày giờ vì có thể trùng và phải tự cập nhật.

**Model** (`Models/SinhVien.cs`)
```csharp
[Timestamp]
public byte[]? RowVersion { get; set; }
```

| Câu lệnh | Tại sao có |
|---|---|
| `byte[]` | rowversion là 8 byte nhị phân |
| `[Timestamp]` | Báo EF: (1) cột do DB sinh → không ghi vào; (2) là **concurrency token** → đưa vào `WHERE` khi UPDATE/DELETE |

**DTO**: `public byte[]? RowVersion { get; set; }`; Service trả ra trong `Select` của `GetAllAsync` và `ToDto`. JSON tự đổi `byte[]` thành chuỗi **Base64** (`"AAAAAAAAB9E="`), gửi lại thì ASP.NET tự đổi ngược.

**Service** (`SinhVienService.UpdateAsync`)
```csharp
public async Task UpdateAsync(int id, SinhVienDto dto)
{
    if (id != dto.Id)
        throw new BadRequestException("Id trong URL không khớp với Id trong dữ liệu gửi lên!");
    if (dto.RowVersion is null)
        throw new BadRequestException("Thiếu RowVersion! Vui lòng tải lại dữ liệu trước khi sửa.");

    var sv = await TimHoacBaoLoiAsync(id);

    if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower() && s.Id != id))
        throw new ConflictException("Email này đã được sử dụng bởi sinh viên khác!");

    _context.Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;

    sv.HoTen = dto.HoTen;
    sv.Email = dto.Email;
    sv.Tuoi = dto.Tuoi;

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        throw new ConflictException("Dữ liệu sinh viên đã bị người khác thay đổi. Vui lòng tải lại trang rồi sửa lại!");
    }
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `id != dto.Id` → 400 | URL nói sửa sinh viên 5 nhưng body là sinh viên 7 → mâu thuẫn, từ chối |
| `RowVersion is null` → 400 | Không có phiên bản thì không biết so với gì → từ chối để **không bao giờ lưu mù** |
| `TimHoacBaoLoiAsync` → 404 | Đọc bản ghi; lúc này OriginalValue của RowVersion là **phiên bản mới nhất trong DB** |
| `&& s.Id != id` trong kiểm tra email | Loại chính sinh viên đang sửa ra – giữ nguyên email của mình không bị tính là trùng |
| **`Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion`** | **Dòng quan trọng nhất.** EF dùng `OriginalValue` để viết `WHERE RowVersion = ...`. Nếu để nguyên, EF so phiên bản mới nhất với chính nó → **luôn khớp** → không bao giờ phát hiện đụng độ. Gán bằng phiên bản client cầm **lúc mở form** thì mới so đúng |
| `Entry(sv)` | Lấy "hồ sơ theo dõi" của `sv` trong ChangeTracker |
| `.Property(s => s.RowVersion)` | Chọn đúng thuộc tính cần chỉnh |
| Gán `HoTen`, `Email`, `Tuoi` | Không gán `AvatarUrl` (ảnh đổi qua API riêng), không gán `RowVersion` (DB tự quản lý) |
| `catch (DbUpdateConcurrencyException)` | EF ném khi UPDATE trúng 0 dòng |
| Đổi thành `ConflictException` | Trả **409** với câu dễ hiểu; nếu để nguyên, người dùng nhận thông báo kỹ thuật |
| Thứ tự kiểm tra | Lỗi rẻ (400, không cần DB) → 404 → 409 email → đụng độ (chỉ biết khi thật sự ghi) |

**Audit**: `SensitiveProperties = { "PasswordHash", "RowVersion" }` – RowVersion đổi sau mọi lần sửa, ghi vào lịch sử chỉ gây nhiễu.

**Angular**
```ts
// models/sinh-vien.ts
rowVersion?: string;

// sinh-vien.ts – sua(sv): mở form
this.sinhVien = { id: sv.id, hoTen: sv.hoTen, email: sv.email, tuoi: sv.tuoi, avatarUrl: sv.avatarUrl,
                  rowVersion: sv.rowVersion };

// sinh-vien.ts – luu(): lỗi khi sửa
error: (error) => {
  const laDungDo = error.status === 409 && error.error?.message?.includes('người khác thay đổi');
  if (laDungDo) {
    this.lamMoiForm();
    this.taiDanhSach();
  }
}
```

| Câu lệnh | Tại sao có |
|---|---|
| `rowVersion?: string` | Nhận chuỗi Base64; Angular không cần hiểu nội dung, chỉ giữ và gửi lại |
| `rowVersion: sv.rowVersion` trong `sua()` | Form là **bản sao** của dòng; thiếu dòng này thì bấm Lưu nhận 400 "Thiếu RowVersion" (lỗi em từng gặp). *Comment hiện tại ở dòng này ghi nhầm "giữ nguyên ảnh đại diện" – nên sửa lại* |
| `laDungDo` | Phân biệt **409 đụng độ** (dữ liệu trong form đã cũ → phải bỏ) với **409 email trùng** (form vẫn dùng được, chỉ sửa email) |
| `lamMoiForm()` + `taiDanhSach()` | Đóng form cũ, tải dữ liệu + phiên bản mới; người dùng bấm Sửa lại sẽ cầm phiên bản mới |
| Không tự gọi Toast | `errorInterceptor` (Tuần 2-4) đã tự hiện câu `message` |

**Test**
- Unit: `Update_ThieuRowVersion_NemBadRequest`; các test Update khác gửi `RowVersion = { 1 }`.
- Integration: `HaiNguoiCungSua_NguoiSauBi409` – A, B cùng đọc (cùng RowVersion) → A lưu **204** → B lưu **409** → DB vẫn là dữ liệu của A, RowVersion đã đổi. Phải chạy trên SQL Server thật vì chỉ SQL Server mới tự tăng rowversion; trên InMemory phiên bản không đổi nên B vẫn lưu được → không tái hiện được đụng độ.

---

# TỔNG KẾT – MỖI YÊU CẦU MỘT CÂU CƠ CHẾ

| Tuần | Yêu cầu | Cơ chế trong một câu |
|---|---|---|
| 1 | DTO + Validation | Lớp trung gian chỉ chứa trường được phép; `[ApiController]` tự kiểm tra attribute và trả 400 trước khi vào hàm |
| 1 | Cấu hình | `IConfiguration` đọc `appsettings.json`; nguồn nạp sau đè nguồn trước |
| 1 | BCrypt | Băm một chiều + salt; đăng nhập thì băm lại và so |
| 1 | JWT | Token có chữ ký; server tính lại chữ ký để tin nội dung, không lưu phiên |
| 1 | async/await | Chờ I/O mà không giữ luồng → server phục vụ được nhiều request |
| 1 | Server sập | `status 0` = không có phản hồi; zoneless phải `detectChanges()` |
| 2 | Phân trang server | `IQueryable` ghép điều kiện, chỉ chạy một câu SQL `OFFSET/FETCH` |
| 2 | Phân quyền 3 lớp | Giao diện và route chỉ để tiện; `[Authorize(Roles)]` đọc role từ JWT mới là bảo mật |
| 2 | Audit Log | Bảng log chung lưu giá trị cũ/mới dạng JSON |
| 2 | Upload ảnh | `multipart/form-data` → `IFormFile`; kiểm tra, đặt tên Guid, DB chỉ lưu đường dẫn |
| 2 | Lỗi + Toast | Interceptor bắt mọi lỗi; `Subject` phát thông báo tới ToastComponent |
| 3-4 | Exception Middleware | Middleware đầu pipeline bọc `try/catch`, đổi exception thành một định dạng JSON |
| 3-4 | Audit Interceptor | EF gọi interceptor trước khi lưu; đọc ChangeTracker để ghi log tự động |
| 3-4 | Xóa ảnh cũ | Chỉ xóa file cũ **sau khi** lưu DB thành công |
| 5-6 | Soft Delete | Xóa = UPDATE cờ; `HasQueryFilter` tự thêm `WHERE IsDeleted = 0` vào mọi truy vấn |
| 5-6 | Service + Unit Test | Service không phụ thuộc HTTP → test bằng DB InMemory, mẫu Arrange–Act–Assert |
| 7 | Integration Test | WebApplicationFactory chạy cả API, TestContainers cấp SQL Server thật dùng một lần |
| 7 | Code Coverage | Coverlet chèn bộ đếm vào code, ReportGenerator vẽ báo cáo; lọc tầng Service |
| 7 | Concurrency | `OriginalValue` = phiên bản lúc mở form → `UPDATE ... WHERE RowVersion = cũ`; 0 dòng → 409 |
