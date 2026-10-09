# TUẦN 1-2 – GIẢI THÍCH TƯỜNG TẬN: YÊU CẦU · CÁCH LÀM · TRÌNH BÀY

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên

Tuần 1-2 là **nền móng** của cả dự án: các tuần sau (Middleware, Interceptor, Service, Test, Concurrency) đều xây lên trên những thứ làm ở đây.

| Tuần | # | Yêu cầu | Một câu tóm tắt |
|---|---|---|---|
| **1 – Nền tảng** | 1 | **DTO** | Không trả thẳng Entity ra ngoài; chỉ gửi/nhận đúng các trường cần, kèm kiểm tra dữ liệu |
| | 2 | **Không hardcode cấu hình** | Connection string, khóa JWT nằm trong `appsettings.json`, không viết cứng trong code |
| | 3 | **Mật khẩu an toàn (BCrypt)** | Lưu mật khẩu dạng **băm** có salt, không bao giờ lưu mật khẩu gốc |
| | 4 | **Xác thực bằng JWT** | Đăng nhập nhận token; mọi request sau gửi kèm token để chứng minh "tôi là ai" |
| | 5 | **Bất đồng bộ (async/await)** | Mọi thao tác DB đều `await ...Async` để server không bị "đứng chờ" |
| | 6 | **Xử lý server sập / sai đăng nhập** | Giao diện báo lỗi rõ ràng, không treo, không im lặng |
| **2 – Tính năng** | 7 | **Phân trang, tìm kiếm, sắp xếp phía server** | SQL Server chỉ trả **đúng một trang**, không tải cả bảng |
| | 8 | **Phân quyền 3 lớp** | Admin được Thêm/Sửa/Xóa; Giảng viên chỉ xem — chặn ở giao diện, route **và API** |
| | 9 | **Audit Log** | Ghi lại ai đã làm gì, lúc nào, giá trị cũ/mới; Admin xem ở trang Lịch sử |
| | 10 | **Upload ảnh đại diện** | Upload ảnh có kiểm tra định dạng, dung lượng, hiện % tiến trình |
| | 11 | **Chuẩn hóa lỗi + Toast** | Mọi lỗi có chung một dạng JSON, Angular hiện thông báo nổi (toast) thống nhất |

Mỗi mục trình bày: **① Vấn đề → ② Ý tưởng → ③ Cách làm (code thật) → ④ Kiểm tra → ⑤ Câu hỏi vấn đáp**. Cuối file có **sơ đồ đường đi của một request**, **tổng hợp** và **kịch bản trình bày**.

---

# PHẦN A – TUẦN 1: NỀN TẢNG

## 1. DTO (Data Transfer Object)

### ① Vấn đề
Nếu Controller nhận/trả thẳng **Entity** `SinhVien` (lớp ánh xạ bảng DB):
- Lộ các trường nội bộ ra ngoài (`IsDeleted`, với tài khoản là `PasswordHash`).
- Client có thể gửi lên trường mà nó **không được phép sửa** (gọi là **Over-posting**), ví dụ tự đặt `IsDeleted = true`.
- Đổi cấu trúc bảng là API đổi theo, Angular bị vỡ.

### ② Ý tưởng
Tạo một lớp riêng chỉ để **vận chuyển dữ liệu qua API** – DTO. Entity là "hồ sơ gốc trong kho", DTO là "bản photo chỉ có các trang được phép xem". DTO cũng là nơi đặt **luật kiểm tra dữ liệu**.

### ③ Cách làm
`DTOs/SinhVienDto.cs`:

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
    public byte[]? RowVersion { get; set; }   // thêm ở Tuần 7
}
```

| Thành phần | Ý nghĩa |
|---|---|
| `[Required]` | Không được để trống |
| `[EmailAddress]` | Phải đúng định dạng email |
| `[Range(18, 99)]` | Tuổi trong khoảng 18–99 |
| `ErrorMessage` | Câu lỗi tiếng Việt trả về cho người dùng |
| **Không có `IsDeleted`** | Client không thể gửi lên hay nhìn thấy trường nội bộ |

Nhờ `[ApiController]` trên Controller, ASP.NET **tự kiểm tra** các attribute này trước khi vào hàm; sai thì trả **400** ngay, hàm Controller không phải viết `if`.

**Đọc dữ liệu** – đổi Entity sang DTO ngay trong câu SQL (gọi là **Projection**):

```csharp
.Select(s => new SinhVienDto { Id = s.Id, HoTen = s.HoTen, Email = s.Email, Tuoi = s.Tuoi, AvatarUrl = s.AvatarUrl, ... })
```

EF chỉ `SELECT` đúng các cột này, không lấy thừa.

**Ghi dữ liệu** – nhận DTO, tự gán từng trường được phép sang Entity:

```csharp
var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
```

Tương tự cho tài khoản: `DangKyDto`, `DangNhapDto` – không bao giờ có `PasswordHash`.

### ④ Kiểm tra
Swagger/Postman gọi `GET /api/SinhVien` → JSON chỉ có `id, hoTen, email, tuoi, avatarUrl, rowVersion`, **không** có `isDeleted`. Thêm sinh viên tuổi 10 → **400** "Tuổi phải là số dương từ 18 đến 99!".

### ⑤ Vấn đáp
**H: DTO khác Entity thế nào?** – Entity ánh xạ bảng DB, dùng bên trong. DTO là "hợp đồng" giữa API và client, chỉ chứa trường được phép trao đổi.
**H: Over-posting là gì?** – Client gửi thêm trường không được phép (ví dụ `isDeleted`, `role`) và server lỡ lưu vào. DTO không có trường đó nên không thể xảy ra.
**H: Validation đặt ở DTO có đủ không?** – Đủ cho kiểm tra **định dạng** (rỗng, email, khoảng số). Kiểm tra **nghiệp vụ** cần đọc DB (email trùng) thì nằm trong Service.

---

## 2. Không hardcode cấu hình

### ① Vấn đề
Viết cứng `"Server=MSI\\SQLEXPRESS;Database=QLSINHVIEN..."` hay khóa bí mật JWT trong code thì: đổi máy/đổi môi trường phải sửa code và build lại; khóa bí mật nằm lẫn trong mã nguồn.

### ② Ý tưởng
Đưa mọi giá trị "phụ thuộc môi trường" ra file cấu hình; code chỉ **đọc theo tên**.

### ③ Cách làm
`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=MSI\\SQLEXPRESS;Database=QLSINHVIEN;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Secret": "<khóa bí mật dài>",
    "Issuer": "QuanLySinhVienAPI",
    "Audience": "QuanLySinhVienClient"
  }
}
```

`Program.cs` đọc ra:

```csharp
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);
```

Trong Controller, nhận `IConfiguration` qua constructor (Dependency Injection) rồi đọc `_configuration.GetSection("JwtSettings")`.

| Thành phần | Ý nghĩa |
|---|---|
| `GetConnectionString("DefaultConnection")` | Đọc mục `ConnectionStrings:DefaultConnection` |
| `GetSection("JwtSettings")["Secret"]` | Đọc khóa con trong một nhóm cấu hình |
| `Trusted_Connection=True` | Đăng nhập SQL bằng tài khoản Windows, không cần mật khẩu trong chuỗi |

**Lợi ích thấy rõ ở Tuần 7:** integration test **ghi đè** `ConnectionStrings:DefaultConnection` sang SQL Server trong Docker mà **không sửa một dòng code nào**.

### ④ Kiểm tra
Đổi tên DB trong `appsettings.json` thành DB không tồn tại → chạy API → lỗi kết nối (chứng minh code thật sự đọc từ file). Đổi lại → chạy bình thường.

### ⑤ Vấn đáp
**H: Vì sao không hardcode?** – Đổi môi trường (máy em, máy mentor, server thật, test) chỉ cần đổi cấu hình, không build lại; tách bí mật khỏi logic.
**H: Để khóa JWT trong `appsettings.json` đã an toàn chưa?** – Đủ cho môi trường học/phát triển. Thực tế nên đặt ở **User Secrets** (máy dev) hoặc **biến môi trường / Key Vault** (server) để không đưa khóa lên Git.

---

## 3. Lưu mật khẩu an toàn với BCrypt

### ① Vấn đề
Lưu mật khẩu dạng chữ thường: ai đọc được DB (hacker, người quản trị) là biết mật khẩu của tất cả, mà người dùng thường dùng **chung** mật khẩu cho nhiều trang.

### ② Ý tưởng
Lưu **giá trị băm (hash)** – một chiều, không giải ngược được. Khi đăng nhập, băm mật khẩu vừa gõ rồi so với hash đã lưu.
- **Salt**: chuỗi ngẫu nhiên trộn vào trước khi băm → hai người cùng mật khẩu `123456` vẫn có hash khác nhau, chống tra bảng hash dựng sẵn (rainbow table).
- **BCrypt cố tình chậm** (băm nhiều vòng) → kẻ tấn công thử hàng tỉ mật khẩu sẽ mất rất nhiều thời gian.

### ③ Cách làm
`Controllers/XacThucController.cs` – **Đăng ký**:

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
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),   // ← băm + salt tự động
        FullName = dto.FullName
    };
    _context.NguoiDung.Add(nguoiDung);
    await _context.SaveChangesAsync();
    return Ok(new { message = "Đăng ký thành công!" });
}
```

**Đăng nhập**:

```csharp
var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
if (nguoiDung == null) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");

bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, nguoiDung.PasswordHash);
if (!isPasswordCorrect) throw new BadRequestException("Tài khoản hoặc mật khẩu không chính xác!");
```

| Dòng | Ý nghĩa |
|---|---|
| `HashPassword(password)` | Tự sinh salt, băm nhiều vòng; kết quả dạng `$2a$11$...` chứa luôn salt và số vòng |
| `Verify(password, hash)` | Lấy salt từ chính chuỗi hash, băm lại mật khẩu vừa nhập rồi so sánh |
| **Cùng một câu lỗi** cho "sai tài khoản" và "sai mật khẩu" | Không tiết lộ tài khoản nào tồn tại → kẻ xấu không dò được danh sách username |
| Role mặc định `"GiangVien"` (trong Model `NguoiDung`) | Người tự đăng ký chỉ có quyền thấp nhất; Admin được cấp riêng |

> Ghi chú dọn code: dòng kiểm tra trùng tài khoản hiện còn `.IgnoreQueryFilters()` — từ khi bỏ xóa mềm cho tài khoản thì không còn cần, có thể xóa cho gọn (không ảnh hưởng kết quả).

### ④ Kiểm tra
Đăng ký tài khoản mới → mở SSMS: `SELECT Username, PasswordHash FROM Users` → cột mật khẩu là chuỗi `$2a$11$...`, không thấy mật khẩu gốc. Đăng ký 2 tài khoản cùng mật khẩu → hai hash khác nhau (nhờ salt).

### ⑤ Vấn đáp
**H: Hash khác mã hóa (encrypt)?** – Mã hóa giải ngược được nếu có khóa; hash là một chiều. Mật khẩu chỉ cần **so khớp**, không bao giờ cần giải ngược, nên dùng hash.
**H: Salt để làm gì?** – Làm cho cùng một mật khẩu cho ra hash khác nhau, vô hiệu hóa bảng hash dựng sẵn.
**H: Vì sao không dùng SHA256?** – SHA256 rất nhanh, kẻ tấn công thử được hàng tỉ lần mỗi giây. BCrypt cố ý chậm và có salt sẵn.

---

## 4. Xác thực bằng JWT (JSON Web Token)

### ① Vấn đề
HTTP **không nhớ** request trước. Đăng nhập xong, request tiếp theo server không biết "đây là ai, có quyền gì".

### ② Ý tưởng
Đăng nhập đúng → server phát một **"thẻ ra vào" có chữ ký** (token). Client gửi kèm thẻ ở mọi request. Server chỉ cần **kiểm tra chữ ký** là tin thông tin trong thẻ, không cần lưu phiên (session) → gọi là **stateless**.

Token có 3 phần ngăn bởi dấu chấm: `Header.Payload.Signature`
- **Payload** chứa các *claim* (Id, Username, Role…) – chỉ **mã hóa Base64**, ai cũng đọc được, **không phải bí mật**.
- **Signature** = băm (Header + Payload) bằng khóa bí mật của server. Sửa một chữ trong Payload là chữ ký không còn khớp → server từ chối.

### ③ Cách làm

**Backend – tạo token** (`XacThucController.GenerateJwtToken`):

```csharp
var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, nguoiDung.Id.ToString()),
    new Claim(ClaimTypes.Name, nguoiDung.Username),
    new Claim(ClaimTypes.Role, nguoiDung.Role),       // backend phân quyền API
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

Đăng nhập trả về: `{ token, fullName, role }`.

**Backend – kiểm tra token** (`Program.cs`):

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme ...)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,            // đúng nơi phát hành
        ValidateAudience = true,          // đúng đối tượng sử dụng
        ValidateLifetime = true,          // chưa hết hạn
        ValidateIssuerSigningKey = true,  // chữ ký đúng khóa
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});
...
app.UseAuthentication();   // đọc token → biết "là ai"
app.UseAuthorization();    // xét [Authorize] → "được làm gì"
```

Thứ tự quan trọng: **Authentication trước Authorization** (phải biết là ai thì mới xét quyền).

**Angular – lưu token** (`services/auth.ts`):

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

`isBrowser` cần thiết vì dự án có **SSR**: khi render trên server không có `localStorage`.

**Angular – tự gắn token vào mọi request** (`interceptors/jwt.ts`):

```ts
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  if (typeof window !== 'undefined' && typeof localStorage !== 'undefined') {
    const token = localStorage.getItem('token');
    if (token) {
      return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
    }
  }
  return next(req);
};
```

Đăng ký trong `app.config.ts`: `provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor]))`. Interceptor = "trạm kiểm soát" mà **mọi** request HTTP đều đi qua → không phải gắn token tay ở từng service.

`req.clone(...)`: request trong Angular là **bất biến**, muốn thêm header phải tạo bản sao.

### ④ Kiểm tra
Đăng nhập → F12 → **Application → Local Storage** thấy `token`. Tab **Network** → request bất kỳ có header `Authorization: Bearer eyJ...`. Dán token vào **jwt.io** → thấy Payload có role, hạn dùng. Xóa token rồi tải lại → bị đưa về `/login`; gọi API không token → **401**.

### ⑤ Vấn đáp
**H: Payload JWT có bị mã hóa không?** – Không, chỉ Base64. Vì vậy **không** đưa mật khẩu hay dữ liệu nhạy cảm vào token. An toàn nằm ở **chữ ký**: không sửa được nội dung.
**H: Vì sao gọi là stateless?** – Server không lưu phiên; mọi thông tin cần thiết nằm trong token, chỉ cần kiểm tra chữ ký.
**H: Token hết hạn thì sao?** – `ValidateLifetime` làm API trả **401**; `errorInterceptor` đăng xuất và đưa về trang đăng nhập. (Lưu ý: mặc định .NET cho phép lệch giờ 5 phút – `ClockSkew`.)
**H: Lưu token ở localStorage có rủi ro gì?** – Nếu trang bị lỗi XSS, mã độc đọc được token. Cách mạnh hơn là cookie `HttpOnly`; với phạm vi dự án, localStorage là chấp nhận được.

---

## 5. Lập trình bất đồng bộ (async/await)

### ① Vấn đề
Truy vấn DB mất thời gian (vài ms đến vài giây). Nếu viết đồng bộ, **luồng (thread)** xử lý request **đứng chờ** suốt thời gian đó. Server có số luồng giới hạn → nhiều người truy cập cùng lúc là hết luồng, request xếp hàng, web chậm.

### ② Ý tưởng
Dùng `await` với các hàm `...Async`: trong lúc chờ DB, luồng được **trả về** cho server phục vụ request khác; khi DB trả kết quả thì tiếp tục.

> Ví von: phục vụ quán ăn. Đồng bộ = đứng cạnh bếp chờ món chín. Bất đồng bộ = gửi phiếu cho bếp rồi đi phục vụ bàn khác, món xong thì quay lại.

### ③ Cách làm
Mọi hàm có truy cập DB đều `async Task<...>` và dùng phiên bản Async:

```csharp
public async Task<IActionResult> DangNhap(DangNhapDto dto)
{
    var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
    ...
}
// Tương tự: ToListAsync, CountAsync, AnyAsync, FindAsync, SaveChangesAsync
```

### ④ Kiểm tra
Tìm trong project: không còn `.ToList()`, `.SaveChanges()` đồng bộ ở Controller/Service (trừ code dựng dữ liệu mẫu trong test).

### ⑤ Vấn đáp
**H: Async có làm 1 request chạy nhanh hơn không?** – Không. Nó giúp server **chịu được nhiều request cùng lúc** hơn, vì luồng không bị giữ khi chờ I/O.
**H: Quên `await` thì sao?** – Hàm chạy tiếp khi DB chưa xong; có thể trả kết quả sai, lỗi không được bắt, hoặc DbContext bị dùng song song gây exception.

---

## 6. Xử lý khi server sập / sai đăng nhập

### ① Vấn đề
Backend tắt hoặc sai mật khẩu mà giao diện **không báo gì** hoặc báo khó hiểu → người dùng tưởng web bị treo.

### ② Ý tưởng
- Lỗi không kết nối được: trình duyệt trả `status = 0` → hiện câu "không kết nối được máy chủ".
- Lỗi từ server: đọc `message` trong JSON lỗi để hiện đúng câu.
- Gom thành **một hàm dùng chung** để mọi chỗ báo lỗi giống nhau.

### ③ Cách làm
`utils/error-message.ts`:

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

| Trường hợp | Kết quả |
|---|---|
| `status === 0` | Backend tắt / sai địa chỉ / CORS chặn → câu "không thể kết nối" |
| Body có `message` | Dùng đúng câu backend gửi |
| Body là chuỗi | Dùng chuỗi đó |
| Còn lại | Câu chung chung, không để trống |

`login/login.ts`:

```ts
error: (err) => {
  this.errorMessage = layThongBaoLoi(err);
  this.cdr.detectChanges(); // Cập nhật giao diện ngay (ứng dụng chạy zoneless)
}
```

**Vì sao cần `detectChanges()`?** Angular 22 chạy **zoneless**: không còn Zone.js tự phát hiện mọi thay đổi. Gán `errorMessage` trong callback HTTP xong phải **báo thủ công** cho Angular vẽ lại; nếu không, banner lỗi chỉ hiện khi người dùng bấm thêm một lần nữa.

### ④ Kiểm tra
Tắt backend → đăng nhập → hiện ngay "Không thể kết nối đến máy chủ...". Bật backend, nhập sai mật khẩu → hiện ngay "Tài khoản hoặc mật khẩu không chính xác!".

### ⑤ Vấn đáp
**H: `status 0` nghĩa là gì?** – Request không nhận được phản hồi HTTP nào: server tắt, mất mạng, hoặc bị CORS chặn.
**H: Zoneless là gì?** – Angular không dùng Zone.js để tự động phát hiện thay đổi; component phải dùng signal hoặc gọi `detectChanges()` khi dữ liệu đổi trong callback bất đồng bộ.

---

# PHẦN B – TUẦN 2: TÍNH NĂNG CHÍNH

## 7. Phân trang, tìm kiếm, sắp xếp phía server

### ① Vấn đề
Trả về **toàn bộ** sinh viên rồi để Angular tự chia trang: bảng có 100.000 dòng là tải 100.000 dòng mỗi lần mở trang → chậm, tốn băng thông, tốn RAM trình duyệt.

### ② Ý tưởng
Client gửi **"tôi muốn trang mấy, mỗi trang bao nhiêu, tìm gì, sắp xếp theo gì"**; SQL Server lọc, sắp xếp và chỉ trả **đúng một trang** kèm **tổng số dòng** để tính số trang.

### ③ Cách làm

**DTO nhận tham số** – `DTOs/SinhVienQuery.cs`:

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

Controller: `GetAll([FromQuery] SinhVienQuery query)` → ASP.NET tự đổ `?pageNumber=2&keyword=an...` vào object (**model binding**).

**DTO trả về** – `DTOs/PagedResult.cs`:

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

`<T>` (generic) → dùng lại được cho bất kỳ loại dữ liệu nào cần phân trang.

**Xử lý** (hiện nằm trong `SinhVienService.GetAllAsync`, Tuần 5-6 đã chuyển từ Controller sang):

```csharp
if (query.PageNumber < 1) query.PageNumber = 1;
if (query.PageSize < 1 || query.PageSize > 50) query.PageSize = 5;

var queryable = _context.SinhVien.AsNoTracking();               // 1. Chưa chạy SQL

if (!string.IsNullOrWhiteSpace(query.Keyword))                   // 2. Lọc
{
    var keyword = query.Keyword.Trim().ToLower();
    queryable = queryable.Where(s => s.HoTen.ToLower().Contains(keyword) || s.Email.ToLower().Contains(keyword));
}

queryable = (query.SortBy?.Trim().ToLower(), query.IsDescending) switch   // 3. Sắp xếp
{
    ("hoten", false) => queryable.OrderBy(s => s.HoTen),
    ("hoten", true)  => queryable.OrderByDescending(s => s.HoTen),
    ...
    _ => queryable.OrderBy(s => s.Id)
};

var totalCount = await queryable.CountAsync();                    // 4. SQL #1: đếm
var items = await queryable                                       // 5. SQL #2: lấy 1 trang
    .Skip((query.PageNumber - 1) * query.PageSize)
    .Take(query.PageSize)
    .Select(s => new SinhVienDto { ... })
    .ToListAsync();
```

| Điểm then chốt | Giải thích |
|---|---|
| **`IQueryable` + thực thi trễ (deferred execution)** | `Where`, `OrderBy` chỉ **ghép dần** câu truy vấn, chưa chạy. SQL chỉ chạy ở `CountAsync` / `ToListAsync` → toàn bộ lọc/sắp xếp/phân trang diễn ra **trong SQL Server** |
| Chặn giá trị sai | `PageSize = 100000` sẽ bị đổi về 5 → không ai tải cả bảng được |
| Luôn có `OrderBy` (mặc định theo Id) | SQL Server bắt buộc có `ORDER BY` khi dùng `OFFSET`; không sắp xếp thì thứ tự trang không ổn định |
| `Skip/Take` | EF dịch thành `OFFSET x ROWS FETCH NEXT y ROWS ONLY` |
| `AsNoTracking()` | Chỉ đọc → không theo dõi thay đổi, nhanh và nhẹ hơn |
| `switch` theo danh sách cột cố định | Không đưa thẳng tên cột từ client vào truy vấn → không thể sắp xếp theo cột lạ |

**Angular** – `services/sinh-vien.ts`:

```ts
getAll(page: number, size: number, keyword: string, sortBy: string, isDesc: boolean) {
  let params = `?pageNumber=${page}&pageSize=${size}`;
  if (keyword) params += `&keyword=${encodeURIComponent(keyword)}`;
  if (sortBy) params += `&sortBy=${sortBy}&isDescending=${isDesc}`;
  return this.http.get<any>(`${this.apiUrl}${params}`);
}
```

Component (`sinh-vien.ts`): `timKiem()` đưa về trang 1; `thayDoiSapXep(cot)` bấm lần 2 cùng cột thì đảo chiều; `chuyenTrang()` chặn trang ngoài khoảng 1..tongSoTrang. Sau khi xóa dòng cuối cùng của một trang, tự lùi về trang trước.

`encodeURIComponent`: mã hóa ký tự đặc biệt/tiếng Việt trong từ khóa để URL không bị vỡ.

### ④ Kiểm tra
Tab Network: request `GET /api/SinhVien?pageNumber=2&pageSize=5...` → response chỉ có 5 dòng + `totalCount`, `totalPages`. Gõ "an" → chỉ còn sinh viên khớp. Bấm tiêu đề cột Họ tên 2 lần → đổi chiều sắp xếp.

### ⑤ Vấn đáp
**H: Vì sao phân trang phía server?** – Chỉ truyền đúng dữ liệu cần hiển thị; dữ liệu lớn bao nhiêu thì mỗi lần vẫn chỉ tải một trang.
**H: `IQueryable` khác `IEnumerable`?** – `IQueryable` ghép điều kiện thành **câu SQL** chạy trong DB. `IEnumerable` lọc **trong RAM** sau khi đã tải dữ liệu về.
**H: Vì sao phải 2 câu SQL (Count và lấy trang)?** – Cần tổng số dòng để Angular tính số trang và hiện "Tổng cộng N sinh viên".
**H: Có điểm nào cải thiện được?** – Khi không có dữ liệu, `TotalPages = 0` nên giao diện hiện "Trang 1 / 0"; có thể hiển thị tối thiểu 1 trang.

---

## 8. Phân quyền 3 lớp

### ① Vấn đề
Giảng viên chỉ được **xem**; Admin được **Thêm/Sửa/Xóa** và xem **Lịch sử**. Nếu chỉ ẩn nút trên giao diện, người dùng vẫn có thể gõ URL hoặc dùng Postman gọi thẳng API.

### ② Ý tưởng
Chặn ở **3 lớp**, nhưng hiểu rõ lớp nào là **bảo mật thật**:

| Lớp | Cách làm | Mục đích | Bảo mật thật? |
|---|---|---|---|
| 1. Giao diện | `*ngIf="authService.hasRole('Admin')"` | Ẩn nút không dùng được → giao diện gọn | ❌ |
| 2. Route | `roleGuard(['Admin'])` | Chặn gõ URL `/lich-su` | ❌ |
| 3. **API** | `[Authorize(Roles = "Admin")]` | Server từ chối, kể cả gọi bằng Postman | ✅ |

Lớp 1 và 2 chạy **trên máy người dùng** nên sửa được; chỉ lớp 3 chạy **trên server** mới là bảo mật thật.

### ③ Cách làm

**Lớp 3 – API** (`SinhVienController.cs`, `AuditLogController.cs`):

```csharp
[Authorize]                                   // cả Controller: phải đăng nhập
public class SinhVienController : ControllerBase
{
    [HttpGet] ...                             // Admin + GiangVien đều xem được
    [HttpPost]   [Authorize(Roles = "Admin")] // chỉ Admin
    [HttpPut]    [Authorize(Roles = "Admin")]
    [HttpDelete] [Authorize(Roles = "Admin")]
    [HttpPost("upload-avatar/{id}")] [Authorize(Roles = "Admin,GiangVien")]
}

[Authorize(Roles = "Admin")]                  // cả Controller lịch sử: chỉ Admin
public class AuditLogController : ControllerBase
```

Role lấy từ claim `ClaimTypes.Role` **trong JWT đã kiểm chữ ký** → không giả mạo được.
- Không có token / token sai → **401 Unauthorized** ("bạn là ai?").
- Có token nhưng sai role → **403 Forbidden** ("biết bạn là ai, nhưng không được phép").

**Lớp 2 – Route** (`guards/role.ts` + `app.routes.ts`):

```ts
export const roleGuard = (allowedRoles: string[]): CanActivateFn => {
  return () => {
    if (!isPlatformBrowser(inject(PLATFORM_ID))) return true;   // SSR: để trình duyệt kiểm
    const authService = inject(AuthService);
    const router = inject(Router);
    if (!authService.isLoggedIn()) return router.parseUrl('/login');
    if (allowedRoles.includes(authService.getRole())) return true;
    inject(ToastService).showError('Không thể truy cập dưới quyền Admin.');
    return router.parseUrl('/sinh-vien');
  };
};

// app.routes.ts
{ path: 'sinh-vien', component: SinhVienComponent, canActivate: [authGuard] },
{ path: 'lich-su',   component: LichSuComponent,   canActivate: [roleGuard(['Admin'])] },
```

- `roleGuard(['Admin'])` là **hàm trả về guard** → dùng lại được với danh sách role khác.
- Trả về `UrlTree` (`router.parseUrl`) thay vì `router.navigate` → Angular **hủy** điều hướng cũ và chuyển hướng gọn trong một bước.

**Lớp 1 – Giao diện** (`sinh-vien.html`):

```html
<button *ngIf="authService.hasRole('Admin')" routerLink="/lich-su">Lịch sử</button>
<div class="form-box" *ngIf="authService.hasRole('Admin')"> ...form thêm/sửa... </div>
<th *ngIf="authService.hasRole('Admin')">Thao tác</th>
```

**Chống sửa role trong localStorage** (`services/auth.ts` – `getRole()`): không tin `localStorage.role`, mà **giải mã Payload của JWT** để lấy role thật. Nếu `localStorage.role` bị sửa khác role trong token → ghi đè lại role thật và báo lỗi.

### ④ Kiểm tra
1. Đăng nhập Giảng viên → không thấy form, nút Sửa/Xóa, nút Lịch sử.
2. Gõ `/lich-su` → bị đưa về `/sinh-vien` kèm toast.
3. F12 → Local Storage → sửa `role` thành `Admin` → tải lại → role bị trả về `GiangVien`, vẫn không thấy nút.
4. Postman gọi `POST /api/SinhVien` bằng token Giảng viên → **403**; không token → **401**.

### ⑤ Vấn đáp
**H: Sửa role trong localStorage thành Admin thì có thêm được sinh viên không?** – Không. Giao diện tự lấy lại role từ JWT; và kể cả có hiện nút, API đọc role từ **token có chữ ký**, nên vẫn trả 403.
**H: Sửa luôn role trong Payload của token thì sao?** – Chữ ký không khớp nữa → server trả 401.
**H: 401 khác 403?** – 401: chưa xác thực (chưa đăng nhập / token hỏng). 403: đã xác thực nhưng không đủ quyền.

---

## 9. Audit Log (Nhật ký thao tác)

### ① Vấn đề
Dữ liệu sinh viên bị sửa sai mà không biết **ai** sửa, **lúc nào**, **trước đó là gì**.

### ② Ý tưởng
Mỗi lần Thêm/Sửa/Xóa, ghi một dòng vào bảng `AuditLogs` gồm người thực hiện, hành động, bảng, giá trị cũ, giá trị mới, thời gian. Chỉ Admin được xem.

### ③ Cách làm
**Bảng** – `Models/AuditLog.cs`:

```csharp
public class AuditLog
{
    public int Id { get; set; }
    public string Username { get; set; } = "Anonymous"; // Người thực hiện
    public string Action { get; set; } = string.Empty;   // Thêm / Sửa / Xóa
    public string TableName { get; set; } = string.Empty; // Bảng bị tác động
    public string? OldValues { get; set; }               // Giá trị cũ (JSON)
    public string? NewValues { get; set; }               // Giá trị mới (JSON)
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
```

**API xem** – `AuditLogController.GetAll` (`[Authorize(Roles = "Admin")]`): `AsNoTracking()` → `OrderByDescending(Timestamp)` (mới nhất lên đầu) → `Select` sang `AuditLogDto`.

**Trang `/lich-su`** (Angular): gọi API, hiển thị bảng; được bảo vệ bởi `roleGuard(['Admin'])`.

**Cách ghi log:** Tuần 2 ban đầu ghi tay trong từng hàm Controller. Đến **Tuần 3-4** chuyển sang `AuditSaveChangesInterceptor` ghi **tự động** cho mọi `SaveChanges`, và loại `PasswordHash`, `RowVersion` khỏi log (xem file `TUAN_3-4_CHI_TIET.md`).

### ④ Kiểm tra
Admin sửa tuổi một sinh viên → vào trang Lịch sử → dòng mới nhất: username Admin, hành động Sửa, cũ `Tuoi: 20`, mới `Tuoi: 21`.

### ⑤ Vấn đáp
**H: Lấy username người thực hiện từ đâu?** – Từ claim trong JWT của request hiện tại (`HttpContext.User`), không tin dữ liệu client gửi lên.
**H: Vì sao chỉ Admin xem được?** – Lịch sử chứa dữ liệu cũ và thông tin người dùng; chặn ở cả API (`[Authorize(Roles="Admin")]`) và route.

---

## 10. Upload ảnh đại diện

### ① Vấn đề
Cho phép tải ảnh lên mà không kiểm tra thì người dùng có thể gửi file rất lớn, file không phải ảnh, hoặc file trùng tên ghi đè ảnh người khác.

### ② Ý tưởng
Kiểm tra **trước khi lưu** (tồn tại, rỗng, đuôi file, dung lượng), đặt **tên file mới ngẫu nhiên**, lưu vào `wwwroot/avatars`, chỉ lưu **đường dẫn** vào DB. Angular gửi file dạng `multipart/form-data` và hiện **% tiến trình**.

### ③ Cách làm
**Backend** – `SinhVienController.UploadAvatar` (`[Authorize(Roles = "Admin,GiangVien")]`):

```csharp
var sinhVien = await _context.SinhVien.FindAsync(id)
    ?? throw new NotFoundException(...);                                   // 1. Có sinh viên không
if (file == null || file.Length == 0) throw new BadRequestException(...); // 2. File rỗng

var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };                // 3. Đuôi file
var extension = Path.GetExtension(file.FileName).ToLower();
if (!allowedExtensions.Contains(extension)) throw new BadRequestException(...);

if (file.Length > 2 * 1024 * 1024) throw new BadRequestException(...);  // 4. ≤ 2MB

var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");          // 5. Thư mục lưu
if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

var uniqueFileName = $"{Guid.NewGuid()}{extension}";                     // 6. Tên ngẫu nhiên
var filePath = Path.Combine(uploadsFolder, uniqueFileName);
using (var stream = new FileStream(filePath, FileMode.Create))
    await file.CopyToAsync(stream);

sinhVien.AvatarUrl = $"/avatars/{uniqueFileName}";                       // 7. Lưu đường dẫn vào DB
await _context.SaveChangesAsync();   // (Tuần 3-4: lỗi thì xóa file mới; thành công thì xóa ảnh cũ)
return Ok(new { AvatarUrl = sinhVien.AvatarUrl });
```

| Điểm | Vì sao |
|---|---|
| `IFormFile` | Kiểu ASP.NET dùng để nhận file upload |
| Tên `Guid` | Không trùng, không ghi đè ảnh người khác, không đoán được tên; không dùng tên file gốc từ client (tránh ký tự lạ, `../`) |
| Lưu **đường dẫn**, không lưu file vào DB | DB nhẹ; file tĩnh được phục vụ trực tiếp nhờ `app.UseStaticFiles()` |
| `_env.WebRootPath` | Đường dẫn thật tới `wwwroot`, không viết cứng |

**Angular** – `services/sinh-vien.ts`:

```ts
uploadAvatar(id: number, file: File): Observable<any> {
  const formData = new FormData();
  formData.append('file', file);           // tên 'file' khớp tham số IFormFile file
  return this.http.post(`${this.apiUrl}/upload-avatar/${id}`, formData, {
    reportProgress: true,                   // bật sự kiện tiến trình
    observe: 'events'                       // nhận mọi sự kiện, không chỉ kết quả cuối
  });
}
```

Component nhận sự kiện:
- `HttpEventType.UploadProgress` → `Math.round(100 * event.loaded / event.total)` → thanh %.
- `HttpEventType.Response` → xong, tải lại danh sách, hiện toast thành công.

### ④ Kiểm tra
Upload ảnh `.png` < 2MB → thanh % chạy, ảnh hiện trong bảng, thư mục `wwwroot/avatars` có file tên Guid. Upload `.pdf` → toast "Định dạng file không hợp lệ...". Upload ảnh > 2MB → toast "Dung lượng file quá lớn...".

### ⑤ Vấn đáp
**H: Vì sao đổi tên file bằng Guid?** – Tránh trùng/ghi đè, tránh đoán tên, tránh tên file độc hại.
**H: Kiểm tra đuôi file đã đủ an toàn chưa?** – Chưa tuyệt đối: đổi tên `virus.exe` thành `.png` vẫn qua. Có thể kiểm tra thêm **chữ ký byte đầu file** (magic number). Ở đây file chỉ được phục vụ như ảnh tĩnh, không được thực thi, nên rủi ro thấp.
**H: `observe: 'events'` để làm gì?** – Mặc định HttpClient chỉ trả kết quả cuối; muốn tính % phải nhận cả các sự kiện tiến trình.

---

## 11. Chuẩn hóa lỗi + Toast

### ① Vấn đề
Mỗi API trả lỗi một kiểu (chuỗi, object khác tên trường, body rỗng); mỗi component tự viết `alert()` hoặc banner riêng → giao diện báo lỗi lộn xộn, có chỗ báo 2 lần, có chỗ không báo.

### ② Ý tưởng
- **Backend**: mọi lỗi có chung một khuôn `{ statusCode, message, details }` (lớp `ErrorResponse`).
- **Frontend**: một `errorInterceptor` bắt **mọi** lỗi HTTP, lấy `message`, hiện **toast** ở một chỗ duy nhất.

### ③ Cách làm
`interceptors/error.ts`:

```ts
return next(req).pipe(
  catchError((error: HttpErrorResponse) => {
    if (typeof window === 'undefined') return throwError(() => error);   // SSR: bỏ qua

    const laApiXacThuc = req.url.includes('/api/XacThuc/');
    if (!laApiXacThuc) toastService.showError(layThongBaoLoi(error));    // trang login tự hiện banner

    if (error.status === 401 && !laApiXacThuc) {
      authService.logout();
      router.navigate(['/login']);                  // hết hạn / token hỏng → đăng nhập lại
    } else if (error.status === 403) {
      router.navigate(['/sinh-vien']);              // không đủ quyền → về trang chính
    }
    return throwError(() => error);                 // ném tiếp để component xử lý riêng nếu cần
  })
);
```

`services/toast.ts`: dùng `Subject` của RxJS – `showSuccess / showError / showWarning` phát thông điệp, `ToastComponent` (đặt một lần ở `app.html`) lắng nghe và hiển thị. Nếu lúc đó chưa có component nào lắng nghe (ví dụ đang chuyển trang), thông điệp được cất tạm vào `sessionStorage` (`flashToast`) để hiện sau khi trang mới mở.

Phía backend, khuôn JSON chung được hoàn thiện ở Tuần 3-4 bằng `ExceptionMiddleware` (xem `TUAN_3-4_CHI_TIET.md`).

### ④ Kiểm tra
Thêm sinh viên trùng email → toast đỏ "Email này đã tồn tại...". Tắt backend rồi bấm Tìm kiếm → toast "Không thể kết nối đến máy chủ...". Xóa token rồi thao tác → về trang đăng nhập.

### ⑤ Vấn đáp
**H: Vì sao xử lý lỗi trong interceptor mà không ở từng component?** – Viết một lần, áp dụng mọi request; giao diện báo lỗi thống nhất; component chỉ xử lý thêm khi có nhu cầu riêng (ví dụ đóng form khi 409 đụng độ).
**H: Vì sao trang đăng nhập không hiện toast?** – Trang đó đã có banner lỗi riêng; hiện thêm toast là báo 2 lần.
**H: Vì sao vẫn `throwError` sau khi đã hiện toast?** – Để component biết request thất bại (tắt loading, giữ form…).

---

# TỔNG HỢP – BỨC TRANH CHUNG CỦA TUẦN 1-2

## Đường đi của một request (ví dụ: Admin thêm sinh viên)

```
[Angular] Form → SinhVienService.create(dto)
   ↓ jwtInterceptor: gắn "Authorization: Bearer <token>"          (Mục 4)
   ↓ HTTP POST /api/SinhVien  (CORS cho phép localhost:4200)
[ASP.NET] UseAuthentication: kiểm chữ ký, hạn, issuer → biết "là ai"   (Mục 4)
   ↓ UseAuthorization: [Authorize(Roles="Admin")] → đủ quyền?          (Mục 8)
   ↓ [ApiController]: kiểm [Required], [EmailAddress], [Range] trên DTO (Mục 1)
   ↓ Controller → (Tuần 5-6: Service) → await SaveChangesAsync()       (Mục 5)
   ↓ ghi AuditLog                                                       (Mục 9)
   ↓ SQL Server (connection string từ appsettings.json)                (Mục 2)
   ↓ 201 Created + SinhVienDto
[Angular] tải lại danh sách theo trang hiện tại                         (Mục 7)
   ↳ nếu lỗi: errorInterceptor → toast / 401 về login / 403 về trang chính (Mục 6, 11)
```

## Ba nhóm ý nghĩa

| Nhóm | Các mục | Giải quyết |
|---|---|---|
| **Bảo mật** | DTO, BCrypt, JWT, Phân quyền 3 lớp, Upload có kiểm tra | Ai được vào, được làm gì, dữ liệu nào được lộ ra |
| **Hiệu năng** | async/await, Phân trang phía server, `AsNoTracking`, Projection | Server chịu nhiều người, mỗi lần chỉ tải đúng dữ liệu cần |
| **Trải nghiệm & vận hành** | Cấu hình ngoài code, Báo lỗi + Toast, Audit Log | Dễ triển khai, người dùng hiểu lỗi, truy vết được thay đổi |

## Nguyên lý cốt lõi (nói được câu này là "ăn điểm")

> **"Không bao giờ tin client"**: giao diện ẩn nút chỉ để gọn; bảo mật thật nằm ở server (`[Authorize]` đọc role từ JWT có chữ ký, validation trên DTO).
> **"Để database làm việc của database"**: lọc, sắp xếp, phân trang chạy trong SQL nhờ `IQueryable`, chỉ trả về đúng một trang.
> **"Viết một lần, dùng mọi nơi"**: interceptor gắn token và xử lý lỗi cho mọi request; cấu hình đọc từ một chỗ.

---

# KỊCH BẢN TRÌNH BÀY (~4 phút)

**(Mở đầu – 20 giây)**
"Tuần 1-2 em xây nền móng cho hệ thống quản lý sinh viên: Tuần 1 là bảo mật và nền tảng kỹ thuật, Tuần 2 là các tính năng chính – phân trang, phân quyền, lịch sử thao tác, upload ảnh và báo lỗi."

**(Tuần 1 – 90 giây)**
"Đầu tiên em dùng **DTO** thay vì trả thẳng Entity: API chỉ gửi những trường cần thiết, client không gửi lên được trường nội bộ như `IsDeleted`, và các luật như email đúng định dạng, tuổi 18–99 được đặt ngay trên DTO, sai thì tự trả 400.
Cấu hình như connection string và khóa JWT em đặt trong `appsettings.json`, không viết cứng trong code. Nhờ vậy đến Tuần 7 test chuyển sang DB Docker mà không sửa code.
Mật khẩu được băm bằng **BCrypt** có salt; trong DB chỉ thấy chuỗi băm. Đăng nhập sai thì báo chung một câu để không lộ tài khoản nào tồn tại.
Đăng nhập đúng, server phát **JWT** có chữ ký, chứa Id, tên và role, hạn 2 giờ. Angular lưu token và một interceptor tự gắn `Bearer token` vào mọi request. Backend kiểm chữ ký, hạn dùng, issuer và audience.
Mọi thao tác DB đều dùng **async/await** để server không bị giữ luồng khi chờ DB. Và khi backend tắt hay sai mật khẩu, giao diện báo rõ ràng ngay."
*(Demo: đăng nhập sai → báo lỗi; đăng nhập đúng → mở jwt.io xem token.)*

**(Tuần 2 – 110 giây)**
"Danh sách sinh viên được **phân trang, tìm kiếm, sắp xếp phía server**. Em dùng `IQueryable` nên các điều kiện được ghép thành một câu SQL, SQL Server chỉ trả đúng một trang kèm tổng số dòng. Các tham số sai như pageSize quá lớn được tự sửa lại.
**Phân quyền** em làm 3 lớp: ẩn nút trên giao diện, guard chặn route `/lich-su`, và quan trọng nhất là `[Authorize(Roles = "Admin")]` trên API. Em hiểu hai lớp đầu chỉ để trải nghiệm, lớp API mới là bảo mật thật, vì role được đọc từ token có chữ ký.
*(Demo: Giảng viên không thấy nút; sửa role trong localStorage thành Admin → bị trả về; Postman gọi POST → 403.)*
**Audit Log** ghi ai đã thêm, sửa, xóa gì, giá trị cũ và mới; chỉ Admin xem được ở trang Lịch sử.
**Upload ảnh** kiểm tra đuôi file, tối đa 2MB, đổi tên bằng Guid để không trùng, lưu file vào `wwwroot/avatars` và chỉ lưu đường dẫn trong DB; Angular hiển thị phần trăm tiến trình.
Cuối cùng, lỗi được **chuẩn hóa**: một interceptor bắt mọi lỗi HTTP, hiện toast thống nhất, 401 thì đăng xuất, 403 thì về trang chính."

**(Kết – 20 giây)**
"Tóm lại, Tuần 1-2 tuân theo ba nguyên tắc: **không tin client**, **để database làm việc của database**, và **viết một lần dùng mọi nơi**. Các tuần sau em nâng cấp tiếp trên nền này: Middleware cho lỗi, Interceptor cho audit, tầng Service, test và xử lý đụng độ."
