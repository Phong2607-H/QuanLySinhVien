# TUẦN 5-6 – GIẢI THÍCH TƯỜNG TẬN: YÊU CẦU · CÁCH LÀM · TRÌNH BÀY

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên

Tuần 5-6 có **2 yêu cầu**, cùng một tinh thần: **làm cho dữ liệu an toàn hơn và code dễ kiểm chứng hơn**.

| # | Yêu cầu | Một câu tóm tắt |
|---|---|---|
| 1 | **Xóa mềm (Soft Delete) + Global Query Filter** | Bấm "Xóa" thì sinh viên **không bị xóa khỏi DB**, chỉ được đánh dấu `IsDeleted = true`; mọi câu truy vấn **tự động ẩn** các dòng đã đánh dấu |
| 2 | **Tách tầng Service + viết Unit Test** | Đưa toàn bộ nghiệp vụ từ Controller sang `SinhVienService`, rồi viết test tự động chứng minh từng nhánh nghiệp vụ chạy đúng |

Mỗi phần trình bày theo thứ tự: **① Vấn đề trước khi làm → ② Ý tưởng → ③ Các bước làm (code thật) → ④ Luồng chạy → ⑤ Kiểm tra → ⑥ Từ khóa → ⑦ Câu hỏi vấn đáp**.

> Quyết định phạm vi: **chỉ áp dụng xóa mềm cho Sinh viên**, không áp dụng cho Tài khoản (NguoiDung).

---

# YÊU CẦU 1 – XÓA MỀM (SOFT DELETE) + GLOBAL QUERY FILTER

## ① Vấn đề trước khi làm

Trước đây, hàm xóa là **xóa cứng (hard delete)**:

```csharp
// Kiểu cũ
_context.SinhVien.Remove(sv);   // → SQL: DELETE FROM SinhVien WHERE Id = 5
await _context.SaveChangesAsync();
```

Hậu quả:

- **Mất vĩnh viễn**: lỡ tay xóa nhầm là không cứu được (trừ khi khôi phục backup cả DB).
- **Mất lịch sử**: các bản ghi khác (điểm, audit log…) đang trỏ tới sinh viên đó trở nên "mồ côi" hoặc DB chặn không cho xóa vì khóa ngoại.
- Thực tế, trường học **cần giữ hồ sơ** sinh viên đã nghỉ để tra cứu sau này.

## ② Ý tưởng giải quyết

Chia làm 2 nửa:

1. **"Xóa" = sửa một cờ (flag).** Thêm cột `IsDeleted`. Khi xóa, chỉ đổi `IsDeleted` thành `true` → SQL thực chất là `UPDATE`, không phải `DELETE`.
2. **Tự động ẩn dòng đã xóa.** Nếu chỉ có cờ mà không lọc, thì danh sách, tìm theo Id, đếm tổng… vẫn hiện sinh viên "đã xóa". Phải nhớ thêm `.Where(s => !s.IsDeleted)` vào **mọi** câu truy vấn → rất dễ quên.
   → Dùng **Global Query Filter** của EF Core: khai báo **một lần** trong `AppDbContext`, EF **tự gắn** điều kiện `WHERE IsDeleted = 0` vào **mọi** câu truy vấn trên bảng SinhVien.

> Ví von: giống thư mục "Thùng rác" — file vẫn nằm trên ổ, nhưng Explorer mặc định không hiện nó ra.

## ③ Các bước làm

### Bước 1 – Thêm cột `IsDeleted` vào Model

`Models/SinhVien.cs`:

```csharp
public class SinhVien
{
    public int Id { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Tuoi { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsDeleted { get; set; } = false;   // ← cờ xóa mềm
    ...
}
```

| Dòng | Ý nghĩa |
|---|---|
| `bool IsDeleted` | `false` = đang hoạt động, `true` = đã xóa |
| `= false` | Sinh viên mới tạo mặc định là chưa xóa |

Trong DB thêm cột tương ứng (bằng Migration hoặc SQL):

```sql
ALTER TABLE SinhVien ADD IsDeleted BIT NOT NULL DEFAULT 0;
```

`DEFAULT 0` giúp các dòng **đã có sẵn** trong bảng tự nhận giá trị "chưa xóa".

### Bước 2 – Khai báo Global Query Filter

`Data/AppDbContext.cs`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Global Query Filter: chỉ áp dụng xóa mềm cho Sinh viên (tài khoản không xóa mềm)
    modelBuilder.Entity<SinhVien>().HasQueryFilter(s => !s.IsDeleted);
}
```

| Thành phần | Ý nghĩa |
|---|---|
| `OnModelCreating` | Hàm EF gọi **một lần** khi dựng mô hình dữ liệu; nơi cấu hình bảng, khóa, bộ lọc… |
| `Entity<SinhVien>()` | Cấu hình cho bảng SinhVien |
| `HasQueryFilter(s => !s.IsDeleted)` | Mọi truy vấn LINQ trên `SinhVien` đều được EF tự thêm `WHERE IsDeleted = 0` |

Kết quả: code `_context.SinhVien.ToListAsync()` sinh ra SQL:

```sql
SELECT ... FROM SinhVien WHERE IsDeleted = 0
```

Bộ lọc tự áp dụng cho: `ToListAsync`, `CountAsync`, `AnyAsync`, `FindAsync`, `Where`, `Skip/Take`… → **mọi chỗ** trong `SinhVienService` đều được bảo vệ mà không cần viết thêm dòng nào.

### Bước 3 – Đổi hàm xóa thành "cập nhật cờ"

`Services/SinhVienService.cs`:

```csharp
public async Task DeleteAsync(int id)
{
    var sv = await TimHoacBaoLoiAsync(id);
    sv.IsDeleted = true; // xóa mềm
    await _context.SaveChangesAsync();
}
```

| Dòng | Ý nghĩa |
|---|---|
| `TimHoacBaoLoiAsync(id)` | Tìm sinh viên; không có thì ném `NotFoundException` (→ 404) |
| `sv.IsDeleted = true` | Không gọi `Remove` nữa, chỉ đổi cờ |
| `SaveChangesAsync()` | EF thấy entity bị **sửa** → sinh `UPDATE SinhVien SET IsDeleted = 1 WHERE Id = ...` |

Điểm hay: vì `FindAsync` cũng bị bộ lọc áp dụng, nên **xóa lần 2** một sinh viên đã xóa sẽ nhận **404** — đúng như người dùng mong đợi ("không còn sinh viên này").

### Bước 4 – Khi CẦN thấy dữ liệu đã xóa: `IgnoreQueryFilters()`

```csharp
var tatCa = await _context.SinhVien.IgnoreQueryFilters().ToListAsync();
```

`IgnoreQueryFilters()` **tắt** bộ lọc cho riêng câu truy vấn đó. Dùng cho: trang quản trị "Thùng rác", chức năng khôi phục, báo cáo, và **trong unit test** để kiểm tra dòng vẫn còn trong DB.

### Bước 5 – Phía Angular: không cần sửa gì

Angular vẫn gọi `DELETE /api/SinhVien/5` như cũ, vẫn nhận `204 No Content`. **Cách xóa thay đổi bên trong backend, hợp đồng API giữ nguyên** → frontend không bị ảnh hưởng. Đây là lợi ích của việc API che giấu chi tiết bên trong.

## ④ Luồng chạy – ví dụ "Admin xóa sinh viên Id = 5"

```
Angular: bấm Xóa → confirm → DELETE /api/SinhVien/5 (kèm JWT)
   ↓
[Authorize(Roles = "Admin")] → hợp lệ
   ↓
SinhVienController.Delete(5) → _service.DeleteAsync(5)
   ↓
FindAsync(5)  →  SQL: ... WHERE Id = 5 AND IsDeleted = 0   → tìm thấy
   ↓
sv.IsDeleted = true → SaveChangesAsync()
   ↓
AuditSaveChangesInterceptor ghi lịch sử: IsDeleted false → true   (Tuần 3-4 vẫn hoạt động)
   ↓
SQL: UPDATE SinhVien SET IsDeleted = 1 WHERE Id = 5
   ↓
204 No Content → Angular tải lại danh sách → GET /api/SinhVien
   ↓
SQL tự có WHERE IsDeleted = 0 → sinh viên 5 không còn trong danh sách
```

## ⑤ Kiểm tra

**Trên web:**

1. Đăng nhập Admin → xóa một sinh viên → sinh viên biến mất khỏi danh sách, tổng số giảm 1.
2. Gõ thẳng `GET /api/SinhVien/{id}` (Swagger/Postman) của sinh viên vừa xóa → **404**.
3. Xóa lại lần nữa Id đó → **404** với message "Không tìm thấy sinh viên có Id = ...".
4. Trang Lịch sử: có dòng ghi thay đổi `IsDeleted`.

**Trong SQL Server Management Studio:**

```sql
SELECT Id, HoTen, IsDeleted FROM SinhVien;   -- dòng vừa xóa VẪN CÒN, IsDeleted = 1
```

→ Đây là bằng chứng mạnh nhất: "trên web mất, trong DB vẫn còn".

## ⑥ Từ khóa

Soft Delete · Hard Delete · Flag `IsDeleted` · Global Query Filter · `HasQueryFilter` · `OnModelCreating` · `IgnoreQueryFilters` · `UPDATE` thay vì `DELETE`

## ⑦ Câu hỏi vấn đáp

**H: Xóa mềm khác xóa cứng thế nào?**
Đ: Xóa cứng chạy `DELETE`, dữ liệu mất hẳn. Xóa mềm chạy `UPDATE` đổi cờ `IsDeleted`, dữ liệu còn trong DB nhưng bị ẩn khỏi ứng dụng.

**H: Vì sao dùng Global Query Filter mà không tự viết `Where(!IsDeleted)`?**
Đ: Viết tay thì phải nhớ ở **mọi** câu truy vấn; quên một chỗ là lộ dữ liệu đã xóa. Filter khai báo một lần, EF tự áp dụng mọi nơi, kể cả `FindAsync`, `CountAsync`, `AnyAsync`.

**H: Muốn xem hoặc khôi phục sinh viên đã xóa thì làm sao?**
Đ: Dùng `IgnoreQueryFilters()` để lấy cả dòng đã xóa, rồi đặt `IsDeleted = false` và lưu lại. Hiện dự án chưa làm API khôi phục vì chưa có yêu cầu, nhưng thêm rất nhanh.

**H: Giảng viên có xem được sinh viên đã xóa không?**
Đ: Không. Bộ lọc nằm ở tầng DbContext, áp dụng cho mọi người dùng, mọi API. Không có API nào gọi `IgnoreQueryFilters` cho dữ liệu sinh viên.

**H: Vì sao không xóa mềm tài khoản?**
Đ: Đó là quyết định phạm vi: yêu cầu nghiệp vụ là giữ hồ sơ sinh viên. Tài khoản đăng nhập không cần lưu trữ như vậy, nên giữ đơn giản.

**H: Nhược điểm của xóa mềm?**
Đ: (1) Bảng phình to vì dữ liệu không bao giờ mất. (2) Ràng buộc duy nhất (unique) như Email có thể vướng: sinh viên đã xóa vẫn giữ email trong DB. (3) Viết SQL tay (không qua EF) sẽ **không** có bộ lọc, phải tự nhớ thêm điều kiện.

---

# YÊU CẦU 2 – TÁCH TẦNG SERVICE + UNIT TEST

## ① Vấn đề trước khi làm

Trước đây **toàn bộ logic nằm trong Controller**: kiểm tra email trùng, phân trang, sắp xếp, tìm kiếm, gọi DB… Hậu quả:

- **Controller "béo" (Fat Controller)**: một file làm quá nhiều việc, khó đọc.
- **Khó test**: muốn test logic "email trùng" thì phải dựng cả HTTP, routing, JWT, Controller → phức tạp và chậm.
- **Khó tái sử dụng**: nếu sau này có chỗ khác (job chạy nền, API khác) cần logic tương tự thì phải copy.

## ② Ý tưởng giải quyết

**Chia trách nhiệm (Separation of Concerns):**

| Tầng | Trách nhiệm | Không làm |
|---|---|---|
| **Controller** | Nhận HTTP request, kiểm quyền (`[Authorize]`), gọi Service, trả mã HTTP | Không chứa nghiệp vụ |
| **Service** | Nghiệp vụ: kiểm tra, tính toán, gọi DB, ném exception khi sai | Không biết gì về HTTP |
| **DbContext** | Đọc/ghi DB | Không chứa nghiệp vụ |

Khi Service **không phụ thuộc HTTP**, ta có thể `new SinhVienService(db)` trực tiếp trong test và gọi hàm như một hàm C# bình thường → đó là **Unit Test**.

## ③ Các bước làm

### Bước 1 – Tạo `SinhVienService` và chuyển logic sang

`Services/SinhVienService.cs` (tóm tắt các hàm):

```csharp
public class SinhVienService
{
    private readonly AppDbContext _context;
    public SinhVienService(AppDbContext context) => _context = context;

    public async Task<PagedResult<SinhVienDto>> GetAllAsync(SinhVienQuery query) { ... }
    public async Task<SinhVienDto> GetByIdAsync(int id) { ... }
    public async Task<SinhVienDto> CreateAsync(SinhVienDto dto) { ... }
    public async Task UpdateAsync(int id, SinhVienDto dto) { ... }
    public async Task DeleteAsync(int id) { ... }

    private async Task<SinhVien> TimHoacBaoLoiAsync(int id) =>
        await _context.SinhVien.FindAsync(id)
        ?? throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");

    private static SinhVienDto ToDto(SinhVien s) => new() { ... };
}
```

| Thành phần | Ý nghĩa |
|---|---|
| Constructor nhận `AppDbContext` | **Dependency Injection**: Service không tự `new` DbContext, mà nhận từ bên ngoài → khi chạy thật nhận DB SQL Server, khi test nhận DB InMemory |
| `TimHoacBaoLoiAsync` | Hàm dùng chung cho GetById/Update/Delete → viết logic "không tìm thấy thì 404" **một lần** |
| `?? throw` | Nếu `FindAsync` trả `null` thì ném exception ngay trong một dòng |
| `ToDto` | Chuyển Entity → DTO, để không trả thẳng entity (có `IsDeleted`) ra ngoài |
| Ném `NotFoundException` / `ConflictException` / `BadRequestException` | Service **không trả mã HTTP**, chỉ ném lỗi nghiệp vụ; `ExceptionMiddleware` (Tuần 3-4) đổi thành 404/409/400 |

**Chi tiết `GetAllAsync`** – hàm nhiều nhánh nhất:

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
    ...
    (_, true) => queryable.OrderByDescending(s => s.Id),
    _ => queryable.OrderBy(s => s.Id)
};

var totalCount = await queryable.CountAsync();
var items = await queryable.Skip((query.PageNumber - 1) * query.PageSize)
                           .Take(query.PageSize)
                           .Select(s => new SinhVienDto { ... })
                           .ToListAsync();
```

| Dòng | Ý nghĩa |
|---|---|
| Chặn `PageNumber`, `PageSize` | Client gửi số vô lý (0, âm, 100000) → tự sửa, tránh lỗi và tránh tải quá nhiều |
| `AsNoTracking()` | Chỉ đọc, không theo dõi thay đổi → nhanh hơn, tốn ít bộ nhớ |
| `Trim().ToLower()` | Tìm kiếm không phân biệt hoa/thường, bỏ khoảng trắng thừa |
| `switch` theo tuple `(SortBy, IsDescending)` | Mỗi cặp ứng với một kiểu sắp xếp; `_` là mặc định theo Id |
| `CountAsync` trước `Skip/Take` | Đếm **tổng** kết quả để Angular tính số trang |
| `Skip/Take` | Phân trang: trang 2, mỗi trang 5 → bỏ 5, lấy 5 |
| `Select(...)` | Chỉ lấy cột cần thiết, đổi thẳng sang DTO ngay trong SQL |

Lưu ý: tất cả các câu trên đều **tự động** có `WHERE IsDeleted = 0` nhờ Yêu cầu 1.

### Bước 2 – Controller chỉ còn "mỏng"

`Controllers/SinhVienController.cs`:

```csharp
[HttpGet]
public async Task<ActionResult<PagedResult<SinhVienDto>>> GetAll([FromQuery] SinhVienQuery query)
    => Ok(await _service.GetAllAsync(query));

[HttpPost]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
{
    var created = await _service.CreateAsync(sinhVienDto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
}

[HttpDelete("{id}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> Delete(int id)
{
    await _service.DeleteAsync(id);
    return NoContent();
}
```

Mỗi action chỉ còn 1–2 dòng: **gọi Service → trả mã HTTP**. Không còn `if`, không còn `try/catch` (lỗi đã có Middleware lo).

> `UploadAvatar` vẫn để ở Controller vì nó làm việc với **file và `IWebHostEnvironment`** (thư mục `wwwroot`) – thuộc về hạ tầng web. Có thể chuyển sau nếu cần.

### Bước 3 – Đăng ký Service vào DI

`Program.cs`:

```csharp
builder.Services.AddScoped<QuanLySinhVien.Services.SinhVienService>();
```

| Lựa chọn | Vì sao |
|---|---|
| `AddScoped` | Mỗi HTTP request tạo **một** instance; khớp với `AppDbContext` (cũng Scoped). Nếu dùng `AddSingleton` thì Service sống mãi nhưng giữ một DbContext đã hết hạn → lỗi |

ASP.NET thấy Controller cần `SinhVienService` trong constructor → tự tạo Service → Service cần `AppDbContext` → tự tạo DbContext. Đây là **Dependency Injection**.

### Bước 4 – Tạo project test

Project `QuanLySinhVien.Tests` dùng:

| Thư viện | Vai trò |
|---|---|
| **xUnit** | Framework chạy test: `[Fact]`, `[Theory]`, `Assert` |
| **EF Core InMemory** | Cơ sở dữ liệu giả lập **trong RAM**, không cần SQL Server |
| Tham chiếu tới project `QuanLySinhVien` | Để gọi được `SinhVienService`, `AppDbContext` |

### Bước 5 – Hàm dựng dữ liệu mẫu `TaoDb()`

`QuanLySinhVien.Tests/SinhVienServiceTests.cs`:

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

| Dòng | Ý nghĩa |
|---|---|
| `UseInMemoryDatabase(Guid.NewGuid().ToString())` | Mỗi test một DB **tên ngẫu nhiên** → các test **độc lập**, không làm bẩn dữ liệu của nhau |
| `AddRange(...)` | 3 sinh viên mẫu: Id 1 = Binh, 2 = An, 3 = Cuong. Dữ liệu được chọn **cố ý** để mỗi kiểu sắp xếp cho ra người đứng đầu khác nhau |
| `RowVersion = { 1 }` | InMemory không tự sinh RowVersion như SQL Server, nên gán sẵn (cho tính năng Tuần 7) |
| `ChangeTracker.Clear()` | Quên các entity vừa thêm → khi test chạy, Service phải đọc lại từ DB, giống tình huống thật |

### Bước 6 – Viết test theo mẫu **Arrange – Act – Assert (AAA)**

**Ví dụ 1 – `[Theory]`: một test chạy với nhiều bộ dữ liệu**

```csharp
[Theory]
[InlineData("hoten", false, 2)]   // An
[InlineData("hoten", true, 3)]    // Cuong
[InlineData("email", false, 3)]   // a@x.com
[InlineData("email", true, 1)]    // c@x.com
[InlineData("tuoi", false, 2)]    // 20
[InlineData("tuoi", true, 1)]     // 22
[InlineData("id", true, 3)]       // Id giảm dần
[InlineData("khac", false, 1)]    // mặc định theo Id
[InlineData(null, false, 1)]      // không truyền SortBy
public async Task GetAll_SapXep_DungThuTu(string? sortBy, bool desc, int idDauTien)
{
    var service = new SinhVienService(TaoDb());                       // Arrange
    var result = await service.GetAllAsync(
        new SinhVienQuery { SortBy = sortBy, IsDescending = desc });  // Act
    Assert.Equal(idDauTien, result.Items[0].Id);                       // Assert
}
```

9 dòng `InlineData` = 9 lần chạy → phủ **mọi nhánh** của `switch` sắp xếp chỉ bằng một hàm test.

**Ví dụ 2 – Test chặn dữ liệu xấu**

```csharp
[Fact]
public async Task GetAll_TimKiem_VaChanPhanTrangSai()
{
    var service = new SinhVienService(TaoDb());
    var result = await service.GetAllAsync(new SinhVienQuery { Keyword = " AN ", PageNumber = 0, PageSize = 100 });

    Assert.Equal(1, result.TotalCount);
    Assert.Equal("An", result.Items[0].HoTen);
    Assert.Equal(1, result.PageNumber); // 0 → tự sửa thành 1
    Assert.Equal(5, result.PageSize);   // 100 → tự sửa thành 5
}
```

Một test kiểm 3 việc: tìm kiếm không phân biệt hoa thường + bỏ khoảng trắng, sửa `PageNumber`, sửa `PageSize`.

**Ví dụ 3 – Test "đường lỗi" bằng `Assert.ThrowsAsync`**

```csharp
[Fact]
public async Task Create_EmailTrung_KhongPhanBietHoaThuong_NemConflict()
{
    await Assert.ThrowsAsync<ConflictException>(() => new SinhVienService(TaoDb())
        .CreateAsync(new SinhVienDto { HoTen = "X", Email = "A@X.COM", Tuoi = 19 }));
}
```

`A@X.COM` trùng `a@x.com` (khác hoa thường) → Service phải ném `ConflictException`. Nếu không ném → test đỏ.

**Ví dụ 4 – Test xóa mềm (nối Yêu cầu 1 với Yêu cầu 2)**

```csharp
[Fact]
public async Task Delete_LaXoaMem()
{
    var db = TaoDb();
    await new SinhVienService(db).DeleteAsync(1);

    var sv = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == 1);
    Assert.True(sv.IsDeleted);                       // dòng VẪN CÒN, chỉ đổi cờ
    Assert.Equal(2, await db.SinhVien.CountAsync()); // filter ẩn dòng đã xóa
}
```

Test này chứng minh **cả hai nửa** của xóa mềm: dòng còn trong DB (`IgnoreQueryFilters`) **và** bị ẩn khỏi truy vấn thường (`CountAsync` = 2).

### Bước 7 – Danh sách test hiện có

| Nhóm | Test | Kiểm tra điều gì |
|---|---|---|
| GetAll | `GetAll_SapXep_DungThuTu` (×9) | Mọi kiểu sắp xếp |
| GetAll | `GetAll_TimKiem_VaChanPhanTrangSai` | Tìm kiếm + chặn phân trang sai |
| GetById | `GetById_CoTonTai_TraVeDto` | Trả đúng DTO |
| GetById | `GetById_KhongTonTai_NemNotFound` | Id không có → 404 |
| Create | `Create_HopLe_LuuVaoDb` | Lưu thành công, DB có 4 dòng |
| Create | `Create_EmailTrung_KhongPhanBietHoaThuong_NemConflict` | Email trùng → 409 |
| Update | `Update_HopLe_CapNhatDuLieu` | Sửa thành công |
| Update | `Update_IdKhongKhop_NemBadRequest` | Id URL ≠ Id body → 400 |
| Update | `Update_ThieuRowVersion_NemBadRequest` | Thiếu RowVersion → 400 (Tuần 7) |
| Update | `Update_KhongTonTai_NemNotFound` | Id không có → 404 |
| Update | `Update_EmailCuaNguoiKhac_NemConflict` | Email trùng người khác → 409 |
| Delete | `Delete_LaXoaMem` | Xóa mềm đúng |
| Delete | `Delete_KhongTonTai_NemNotFound` | Id không có → 404 |

Nguyên tắc: **mỗi hàm test cả "đường vui" (happy path) lẫn mọi "đường lỗi"**.

## ④ Luồng chạy – một unit test

```
dotnet test
   ↓
xUnit tìm mọi hàm có [Fact]/[Theory]
   ↓
Mỗi test:  TaoDb() → DB InMemory mới, 3 sinh viên mẫu
   ↓
new SinhVienService(db)          ← không cần HTTP, không cần JWT, không cần SQL Server
   ↓
Gọi hàm Service → nhận kết quả hoặc exception
   ↓
Assert → đúng: xanh ✓ / sai: đỏ ✗ kèm giá trị mong đợi và giá trị thực tế
```

So với chạy qua web: không cần bật API, không cần đăng nhập, mỗi test chỉ mất vài mili giây.

## ⑤ Kiểm tra

**Visual Studio:** menu **Test → Test Explorer → Run All Tests** (Ctrl+R, A). Tất cả hiện dấu ✓ xanh.

**Dòng lệnh** (tại `D:\TT\QuanLySinhVien`):

```bash
dotnet test
```

Kết quả mong đợi dạng: `Passed! - Failed: 0, Passed: ..., Skipped: 0`.

**Thử "làm hỏng" để chứng minh test có giá trị:** tạm sửa `sv.IsDeleted = true` thành `_context.SinhVien.Remove(sv)` → chạy lại → `Delete_LaXoaMem` **đỏ**. Sửa lại → xanh. Đây là màn demo rất thuyết phục khi trình bày.

**Trên web:** mọi chức năng (danh sách, tìm kiếm, sắp xếp, thêm, sửa, xóa) vẫn chạy y như trước → chứng minh việc tách Service **không làm thay đổi hành vi** (đây gọi là **refactor**).

## ⑥ Từ khóa

Service Layer · Separation of Concerns · Fat/Thin Controller · Dependency Injection · `AddScoped` · Refactor · Unit Test · xUnit · `[Fact]` / `[Theory]` / `[InlineData]` · Arrange–Act–Assert · EF Core InMemory · Happy path / Edge case

## ⑦ Câu hỏi vấn đáp

**H: Vì sao phải tách Service ra khỏi Controller?**
Đ: Để mỗi lớp làm một việc: Controller lo HTTP, Service lo nghiệp vụ. Nhờ vậy nghiệp vụ test được độc lập, dễ đọc, dễ tái sử dụng.

**H: Unit test là gì?**
Đ: Test tự động kiểm tra **một đơn vị code nhỏ** (ở đây là từng hàm của Service) một cách độc lập, chạy nhanh, không cần chạy cả ứng dụng.

**H: `[Fact]` khác `[Theory]` thế nào?**
Đ: `[Fact]` là một test cố định. `[Theory]` là một test chạy nhiều lần với các bộ dữ liệu khác nhau qua `[InlineData]`.

**H: Vì sao dùng DB InMemory chứ không dùng Moq để giả DbContext?**
Đ: Giả lập `DbSet` bằng Moq rất rườm rà (phải giả cả `IQueryable`, async…). InMemory là một DB thật chạy trong RAM, viết test ngắn, tự nhiên và vẫn chạy được LINQ, Query Filter.

**H: InMemory có nhược điểm gì?**
Đ: Nó không phải SQL Server: không tự sinh RowVersion, không kiểm khóa ngoại/unique như SQL thật, một số câu LINQ dịch khác. Vì thế ở Tuần 7 em bổ sung **Integration Test với TestContainers** chạy trên SQL Server thật.

**H: Vì sao mỗi test dùng tên DB `Guid.NewGuid()`?**
Đ: Để các test không dùng chung dữ liệu. Nếu dùng chung, test xóa chạy trước sẽ làm test đếm chạy sau bị sai.

**H: Vì sao Service ném exception mà không trả `NotFound()`?**
Đ: Service không biết về HTTP. Nó chỉ nói "không tìm thấy"; việc đổi thành mã 404 là của `ExceptionMiddleware`. Nhờ vậy trong test chỉ cần `Assert.ThrowsAsync<NotFoundException>`.

**H: Vì sao đăng ký Service là `AddScoped`?**
Đ: Vì Service dùng `AppDbContext` (Scoped). Service phải sống ngắn bằng hoặc ngắn hơn thứ nó phụ thuộc, nên chọn Scoped: mỗi request một instance.

**H: Có cần interface `ISinhVienService` không?**
Đ: Hiện chỉ có một cài đặt và test gọi Service trực tiếp, nên chưa cần. Khi cần mock Service để test Controller hoặc có nhiều cài đặt thì mới thêm interface.

---

# TỔNG HỢP – BỨC TRANH CHUNG CỦA TUẦN 5-6

## Hai yêu cầu bổ trợ cho nhau

```
            ┌──────────────────────────────┐
 HTTP  ───► │ Controller (mỏng)            │  [Authorize], trả mã HTTP
            └──────────────┬───────────────┘
                           ▼
            ┌──────────────────────────────┐
            │ SinhVienService (nghiệp vụ)  │  ◄── Unit Test gọi thẳng vào đây
            └──────────────┬───────────────┘
                           ▼
            ┌──────────────────────────────┐
            │ AppDbContext                 │  HasQueryFilter(!IsDeleted)
            │  + AuditInterceptor (T3-4)   │  → mọi truy vấn tự ẩn dòng đã xóa
            └──────────────┬───────────────┘
                           ▼
                     SQL Server
```

- **Xóa mềm** bảo vệ **dữ liệu**: không mất hồ sơ, vẫn có lịch sử.
- **Service + Unit Test** bảo vệ **code**: mỗi lần sửa, chạy `dotnet test` là biết ngay có làm hỏng gì không.
- Hai yêu cầu gặp nhau ở test `Delete_LaXoaMem`: test chứng minh xóa mềm hoạt động đúng.
- Tất cả nối tiếp Tuần 3-4: Service ném exception → Middleware đổi thành JSON lỗi; xóa mềm là một lần "sửa" → Interceptor ghi lịch sử.

## Nguyên lý cốt lõi (nói được câu này là "ăn điểm")

> **"Khai báo một lần, áp dụng mọi nơi"**: Query Filter khai báo một lần trong DbContext thay vì nhớ `Where` ở mọi truy vấn.
> **"Mỗi lớp một trách nhiệm"**: Controller lo HTTP, Service lo nghiệp vụ, nên nghiệp vụ test được mà không cần HTTP.

---

# KỊCH BẢN TRÌNH BÀY (~3 phút)

**(Mở đầu – 20 giây)**
"Tuần 5-6 em làm 2 yêu cầu: **xóa mềm với Global Query Filter**, và **tách tầng Service kèm Unit Test**. Mục tiêu là dữ liệu không bị mất do xóa nhầm, và nghiệp vụ được kiểm chứng tự động."

**(Xóa mềm – 60 giây)**
"Trước đây bấm xóa là chạy `DELETE`, mất hẳn. Em thêm cột `IsDeleted`. Hàm `DeleteAsync` giờ chỉ đặt `IsDeleted = true`, nên SQL thực chất là `UPDATE`.
Để không phải nhớ thêm điều kiện ở mọi truy vấn, em khai báo **Global Query Filter** một lần trong `AppDbContext`: `HasQueryFilter(s => !s.IsDeleted)`. EF tự thêm `WHERE IsDeleted = 0` vào mọi câu: danh sách, đếm, tìm theo Id. Vì vậy xóa lại một sinh viên đã xóa sẽ nhận 404.
*(Demo)* Em xóa một sinh viên: trên web biến mất, nhưng mở SQL Server thì dòng vẫn còn với `IsDeleted = 1`. Khi cần xem lại thì dùng `IgnoreQueryFilters()`.
Em chỉ áp dụng cho sinh viên, không áp dụng cho tài khoản, theo đúng phạm vi nghiệp vụ."

**(Service + Unit Test – 80 giây)**
"Trước đây toàn bộ logic nằm trong Controller nên rất khó test. Em tách ra `SinhVienService`: Controller giờ mỗi action chỉ một hai dòng, gọi Service rồi trả mã HTTP. Service không biết HTTP, khi sai thì ném `NotFoundException`, `ConflictException`, và Middleware tuần trước đổi thành JSON lỗi. Service được đăng ký `AddScoped` vì dùng chung vòng đời với DbContext.
Nhờ tách như vậy em viết được unit test với xUnit và EF InMemory. Mỗi test tạo một DB riêng trong RAM, gọi thẳng Service, kiểm tra theo mẫu Arrange–Act–Assert. Em test cả đường thành công lẫn mọi đường lỗi: không tìm thấy, email trùng không phân biệt hoa thường, Id không khớp, phân trang sai. Riêng sắp xếp em dùng `[Theory]` với 9 bộ dữ liệu để phủ hết các nhánh `switch`.
*(Demo)* Em chạy `dotnet test`: tất cả đều xanh. Nếu em đổi xóa mềm thành `Remove`, test `Delete_LaXoaMem` đỏ ngay."

**(Kết – 20 giây)**
"Tóm lại: Query Filter giúp **khai báo một lần, áp dụng mọi nơi**; tách Service giúp **mỗi lớp một trách nhiệm** và test được. Hạn chế của InMemory là không giống hệt SQL Server, nên ở Tuần 7 em bổ sung Integration Test với TestContainers trên SQL Server thật."
