# TUẦN 7 – GIẢI THÍCH TƯỜNG TẬN: YÊU CẦU · CÁCH LÀM · TRÌNH BÀY

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên

Tuần 7 có **3 yêu cầu**, cùng một tinh thần: **chứng minh hệ thống chạy đúng trong điều kiện thật** — trên SQL Server thật, có số liệu đo được, và khi nhiều người dùng cùng lúc.

| # | Yêu cầu | Một câu tóm tắt |
|---|---|---|
| 1 | **Kiểm thử nâng cao – Integration Test với TestContainers** | Viết ít nhất 2 kịch bản gọi API **từ Controller xuyên xuống tận DB** (SQL Server thật chạy trong Docker) và pass |
| 2 | **Quản lý chất lượng code – Code Coverage ≥ 80%** | Đo xem test đã chạy qua bao nhiêu phần trăm code của **tầng Service**, đạt tối thiểu 80% |
| 3 | **Xử lý đụng độ dữ liệu – Optimistic Concurrency với RowVersion** | Hai người cùng sửa một sinh viên → người lưu sau bị chặn (409) thay vì âm thầm ghi đè người trước |

Mỗi phần trình bày theo thứ tự: **① Vấn đề trước khi làm → ② Ý tưởng → ③ Các bước làm (code thật) → ④ Luồng chạy → ⑤ Kiểm tra → ⑥ Từ khóa → ⑦ Câu hỏi vấn đáp**.

> Nên đọc theo thứ tự **3 → 1 → 2** nếu muốn hiểu dễ nhất: Concurrency là tính năng mới; Integration Test chứng minh nó chạy trên SQL thật; Coverage đo chất lượng test. Tài liệu vẫn giữ thứ tự 1-2-3 như đề bài.

---

# YÊU CẦU 1 – INTEGRATION TEST VỚI TESTCONTAINERS

## ① Vấn đề trước khi làm

Tuần 5-6 đã có **Unit Test**, nhưng nó có 2 giới hạn:

1. **Chỉ test riêng Service.** Không kiểm tra được những thứ nằm *ngoài* Service: routing (`/api/SinhVien/5`), `[Authorize]` và JWT, validation 400, `ExceptionMiddleware` đổi exception thành JSON, chuyển đổi JSON ↔ DTO.
2. **Dùng DB InMemory, không phải SQL Server.** InMemory không tự sinh `RowVersion`, không kiểm unique/khóa ngoại như SQL thật, một số câu LINQ dịch khác. Code có thể pass trên InMemory nhưng lỗi trên SQL Server.

Còn nếu test trực tiếp vào DB thật `QLSINHVIEN` thì **làm bẩn dữ liệu thật** và test phụ thuộc vào máy có cài SQL Server hay không.

## ② Ý tưởng giải quyết

Ghép 2 công cụ:

| Công cụ | Vai trò | Ví von |
|---|---|---|
| **WebApplicationFactory<Program>** | Khởi động **toàn bộ API trong bộ nhớ** (đủ Program.cs, Middleware, JWT, Controller, Service) và cho một `HttpClient` gọi vào như Angular | Một "bản sao" của server, chạy ngay trong test |
| **TestContainers (MsSqlContainer)** | Tự bật **một SQL Server thật trong Docker** khi test bắt đầu, tự xóa khi test xong | Thuê phòng thí nghiệm sạch, dùng xong trả lại |

Kết quả: test đi **đúng con đường của request thật**:

```
HttpClient → Middleware → [Authorize] → Controller → Service → EF Core → SQL Server (Docker)
```

mà **không đụng vào DB thật** và **không phải cài SQL Server** — chỉ cần Docker.

## ③ Các bước làm

### Bước 0 – Chuẩn bị Docker

- Cài **Docker Desktop**, bật ảo hóa (Virtualization) trong BIOS và WSL2.
- Kiểm tra: `docker run hello-world` chạy được là OK.
- (Trên máy em: ổ C hết dung lượng gây lỗi `mkfs`, đã chuyển nơi lưu dữ liệu Docker sang `D:\DockerData`.)

### Bước 1 – Thêm gói NuGet vào project test

`QuanLySinhVien.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
<PackageReference Include="Testcontainers.MsSql" Version="4.15.0" />
```

| Gói | Dùng để |
|---|---|
| `Microsoft.AspNetCore.Mvc.Testing` | Cung cấp `WebApplicationFactory` |
| `Testcontainers.MsSql` | Cung cấp `MsSqlBuilder`, `MsSqlContainer` để bật SQL Server trong Docker |

### Bước 2 – Cho test "nhìn thấy" lớp `Program`

Cuối `Program.cs`:

```csharp
public partial class Program { }
```

**Vì sao?** `Program.cs` viết kiểu **top-level statements** (không có `class Program` tường minh). Trình biên dịch tự sinh lớp `Program` nhưng ở dạng `internal`, project test không truy cập được. Dòng này biến nó thành `public` để viết được `WebApplicationFactory<Program>`.

### Bước 3 – Viết `ApiFactory`: dựng server + DB cho test

`QuanLySinhVien.Tests/IntegrationTests/ApiFactory.cs`:

```csharp
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // SQL Server chạy trong Docker — mỗi lần chạy test là một DB mới tinh
    private readonly MsSqlContainer _db =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Thay connection string QLSINHVIEN bằng DB trong container
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync(); // 1. Bật container

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Chốt an toàn: lỡ trỏ vào DB thật thì dừng ngay
        if (db.Database.GetConnectionString()!.Contains("QLSINHVIEN"))
            throw new InvalidOperationException("Test đang trỏ vào DB thật!");

        await db.Database.EnsureCreatedAsync(); // 2. Tạo bảng theo Model
    }

    public new async Task DisposeAsync() => await _db.DisposeAsync(); // 3. Xóa container
}
```

| Thành phần | Ý nghĩa |
|---|---|
| Tên `ApiFactory` | "Nhà máy sản xuất API" cho test: kế thừa `WebApplicationFactory` – theo mẫu thiết kế **Factory** |
| `WebApplicationFactory<Program>` | Chạy toàn bộ API (từ `Program.cs`) ngay trong bộ nhớ của test |
| `IAsyncLifetime` | Cho xUnit gọi `InitializeAsync` **trước** khi chạy test và `DisposeAsync` **sau** khi chạy xong |
| `MsSqlBuilder("...2022-latest").Build()` | Khai báo container SQL Server 2022 (chưa bật) |
| `ConfigureWebHost` + `UseSetting` | **Ghi đè** connection string: API trong test kết nối DB trong Docker thay vì `QLSINHVIEN` |
| `_db.StartAsync()` | Kéo image (lần đầu) và bật container; TestContainers tự chọn cổng trống, tự sinh mật khẩu |
| Chốt an toàn `Contains("QLSINHVIEN")` | Phòng trường hợp ghi đè thất bại → dừng ngay, không bao giờ làm bẩn DB thật |
| `EnsureCreatedAsync()` | Tạo toàn bộ bảng theo các Model C# (SinhVien có `IsDeleted`, `RowVersion`…) |
| `DisposeAsync()` | Xóa container → không để lại rác |

### Bước 4 – Helper đăng nhập: `TaoClientAsync(role)`

API có `[Authorize]`, nên test phải có JWT thật:

```csharp
private async Task<HttpClient> TaoClientAsync(string role)
{
    var username = $"user_{Guid.NewGuid():N}";
    var client = _factory.CreateClient();

    // 1. Đăng ký
    (await client.PostAsJsonAsync("/api/XacThuc/dangky",
        new DangKyDto { Username = username, Password = "123456", FullName = "Test" }))
        .EnsureSuccessStatusCode();

    // 2. DangKy mặc định Role = "GiangVien" → sửa trong DB để có Admin
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.NguoiDung.SingleAsync(u => u.Username == username);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    // 3. Đăng nhập lấy token
    var res = await client.PostAsJsonAsync("/api/XacThuc/dangnhap",
        new DangNhapDto { Username = username, Password = "123456" });
    var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();

    // 4. Gắn token vào mọi request sau
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
}
```

Điểm hay: test đi qua **đúng luồng đăng ký → đăng nhập → JWT** như người dùng thật. Username ngẫu nhiên (`Guid`) để các test không trùng nhau.

Helper dữ liệu: `SinhVienMoi()` tạo sinh viên với **email ngẫu nhiên**, vì tất cả test dùng chung một DB container.

### Bước 5 – Viết các kịch bản test

Lớp test khai báo `IClassFixture<ApiFactory>`:

```csharp
public class SinhVienApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SinhVienApiTests(ApiFactory factory) => _factory = factory;
    ...
}
```

`IClassFixture` = **cả lớp test dùng chung một `ApiFactory`** → chỉ bật container **một lần** (mất khoảng chục giây), thay vì mỗi test bật một lần.

Dự án có **6 kịch bản** (yêu cầu tối thiểu 2):

| # | Test | Đi qua những tầng nào | Kiểm tra |
|---|---|---|---|
| 1 | `TaoSinhVien_RoiDocLai_DungDuLieu` | JWT → Controller → Service → SQL | POST trả **201**, GET lại đúng dữ liệu |
| 2 | `TaoSinhVien_EmailTrung_Tra409` | … → Service ném `ConflictException` → **Middleware** | **409** + JSON `{statusCode, message}` chuẩn |
| 3 | `XoaSinhVien_LaXoaMem` | … → **Query Filter** trên SQL thật | DELETE **204**, GET lại **404**, nhưng dòng vẫn còn với `IsDeleted = true` |
| 4 | `PhanQuyen_401Va403` | **JWT + `[Authorize(Roles)]`** | Chưa đăng nhập **401**, Giảng viên thêm SV **403** |
| 5 | `TaoSinhVien_TuoiKhongHopLe_Tra400` | **Validation** + `InvalidModelStateResponseFactory` | **400** + câu lỗi tiếng Việt |
| 6 | `HaiNguoiCungSua_NguoiSauBi409` | … → **RowVersion trên SQL Server thật** | Người sau bị **409**, dữ liệu người trước được giữ |

**Phân tích kịch bản 1 (mẫu Arrange – Act – Assert):**

```csharp
[Fact]
public async Task TaoSinhVien_RoiDocLai_DungDuLieu()
{
    var admin = await TaoClientAsync("Admin");          // Arrange: có client đã đăng nhập Admin
    var dto = SinhVienMoi();

    var create = await admin.PostAsJsonAsync("/api/SinhVien", dto);   // Act 1: gọi API thêm
    Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    var created = await create.Content.ReadFromJsonAsync<SinhVienDto>();

    var get = await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created!.Id}"); // Act 2: đọc lại
    Assert.Equal(dto.Email, get!.Email);                                                  // Assert
    Assert.Equal(dto.HoTen, get.HoTen);
}
```

**Phân tích kịch bản 3 – vừa gọi API vừa nhìn thẳng vào DB:**

```csharp
var del = await admin.DeleteAsync($"/api/SinhVien/{created!.Id}");
Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

var get = await admin.GetAsync($"/api/SinhVien/{created.Id}");
Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);   // API: đã "biến mất"

using var scope = _factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var row = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == created.Id);
Assert.True(row.IsDeleted);                              // DB: vẫn còn, chỉ bị đánh dấu
```

## ④ Luồng chạy

```
dotnet test
   ↓
xUnit thấy IClassFixture<ApiFactory> → tạo ApiFactory
   ↓
InitializeAsync: bật container SQL Server trong Docker → EnsureCreated tạo bảng
   ↓
Mỗi test: CreateClient() → đăng ký, đăng nhập → gửi HTTP thật vào API trong bộ nhớ
   ↓
API chạy đủ: ExceptionMiddleware → Authentication/Authorization → Controller → Service → EF → SQL (Docker)
   ↓
Assert mã HTTP + JSON trả về (+ nhìn thẳng vào DB nếu cần)
   ↓
Hết test: DisposeAsync → container bị xóa
```

## ⑤ Kiểm tra

- Mở **Docker Desktop** → chạy test → tab **Containers** thấy xuất hiện container `mcr.microsoft.com/mssql/server:2022-latest` (cùng một container phụ tên `testcontainers/ryuk` dùng để dọn dẹp). Test xong thì chúng biến mất.
- Visual Studio: **Test Explorer → Run All** → 6 test trong `SinhVienApiTests` đều ✓ xanh.
- Dòng lệnh: `dotnet test --filter "FullyQualifiedName~IntegrationTests"` để chỉ chạy integration test.
- Tắt Docker rồi chạy lại → integration test **đỏ** (không bật được container), còn unit test vẫn xanh. Điều này chứng minh test thực sự dùng Docker.

## ⑥ Từ khóa

Integration Test · TestContainers · Docker · `WebApplicationFactory<Program>` · `public partial class Program` · top-level statements · `IAsyncLifetime` · `IClassFixture` · `UseSetting` · `EnsureCreated` · `HttpClient` · Bearer token

## ⑦ Câu hỏi vấn đáp

**H: Unit test khác Integration test thế nào?**
Đ: Unit test kiểm tra **một phần nhỏ** (Service) độc lập, DB giả trong RAM, rất nhanh. Integration test kiểm tra **nhiều phần ghép lại** (HTTP → Middleware → JWT → Controller → Service → SQL Server thật), chậm hơn nhưng sát thực tế hơn. Hai loại bổ sung cho nhau.

**H: Docker đóng vai trò gì trong Integration Test?**
Đ: Docker cung cấp một **SQL Server thật, sạch, dùng một lần**. Test không đụng DB thật, máy nào có Docker cũng chạy được giống nhau, chạy xong tự dọn.

**H: Vì sao không test trực tiếp vào DB `QLSINHVIEN`?**
Đ: Sẽ thêm rác vào dữ liệu thật, kết quả test phụ thuộc dữ liệu đang có, và máy người khác (hoặc CI) không có DB đó. Em còn thêm chốt an toàn: nếu connection string chứa `QLSINHVIEN` thì dừng ngay.

**H: Vì sao phải có `public partial class Program { }`?**
Đ: `Program.cs` dùng top-level statements nên lớp `Program` do trình biên dịch tự sinh là `internal`. `WebApplicationFactory<Program>` ở project test cần truy cập nó, nên khai báo thêm phần `public`.

**H: `IClassFixture` để làm gì?**
Đ: Cho cả lớp test dùng chung một `ApiFactory`, tức một container. Bật container mất vài giây đến vài chục giây, bật một lần thì cả bộ test nhanh hơn nhiều.

**H: Dùng chung DB thì các test có đụng nhau không?**
Đ: Em tránh bằng dữ liệu ngẫu nhiên: username và email đều sinh từ `Guid`, mỗi test tự tạo sinh viên riêng và chỉ kiểm tra trên sinh viên đó.

**H: Vì sao dùng `EnsureCreated` mà không chạy Migration?**
Đ: Mục đích là có đủ bảng đúng theo Model cho test, `EnsureCreated` làm việc đó trong một lệnh. Nếu dự án quản lý schema hoàn toàn bằng Migration thì có thể đổi sang `Database.MigrateAsync()` để test cả migration.

---

# YÊU CẦU 2 – CODE COVERAGE ≥ 80% CHO TẦNG SERVICE

## ① Vấn đề trước khi làm

Đã có nhiều test, nhưng câu hỏi là: **"Test đã đủ chưa?"** Có nhánh `if`, có `case` nào trong `switch` chưa từng được test chạy qua không? Nếu chỉ nhìn bằng mắt thì không biết chắc.

## ② Ý tưởng giải quyết

Dùng công cụ **đo độ phủ (Code Coverage)**: khi chạy test, công cụ đánh dấu **dòng nào đã được thực thi**. Sau đó tính tỉ lệ và xuất báo cáo tô màu:

- **Xanh**: dòng đã được test chạy qua.
- **Đỏ**: dòng chưa test nào chạy tới → cần viết thêm test.
- **Vàng**: nhánh điều kiện mới chạy một phía (ví dụ `if` chỉ chạy nhánh đúng).

> Ví von: đội kiểm tra đi qua các phòng của tòa nhà. Coverage 85% = đã bước vào 85% số phòng. Nó cho biết **phòng nào chưa ai vào**, nhưng **không** đảm bảo phòng đã vào là an toàn.

**Chỉ đo tầng Service**, vì đó là nơi chứa nghiệp vụ (đúng yêu cầu mentor). Đo cả Controller, Migration, Program.cs sẽ làm con số bị pha loãng và ít ý nghĩa.

## ③ Các bước làm

### Bước 1 – Công cụ thu thập: Coverlet

Gói `coverlet.collector` **đã có sẵn** trong `QuanLySinhVien.Tests.csproj` (template xUnit tự thêm):

```xml
<PackageReference Include="coverlet.collector" Version="6.0.4" />
```

### Bước 2 – Chạy test kèm thu thập coverage

Tại thư mục `D:\TT\QuanLySinhVien`:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

| Phần | Ý nghĩa |
|---|---|
| `--collect:"XPlat Code Coverage"` | Bật Coverlet đếm dòng đã chạy |
| `--results-directory .\TestResults` | Nơi lưu kết quả; tạo ra file `coverage.cobertura.xml` |

### Bước 3 – Đổi XML thành trang HTML dễ đọc: ReportGenerator

Cài một lần:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Xuất báo cáo, **chỉ tính tầng Service**:

```bash
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```

| Phần | Ý nghĩa |
|---|---|
| `-reports:` | Đường dẫn file XML (dấu `**` = tìm trong mọi thư mục con) |
| `-targetdir:` | Thư mục chứa báo cáo HTML |
| `-classfilters:"+QuanLySinhVien.Services.*"` | Dấu `+` = **chỉ giữ** các lớp trong namespace Services |

Mở `CoverageReport\index.html` bằng trình duyệt → xem **Line coverage** của `SinhVienService` (yêu cầu ≥ 80%).

### Bước 4 – Đọc báo cáo, bổ sung test chỗ đỏ

Bấm vào `SinhVienService` → thấy code tô màu. Cách em đạt ≥ 80%:

| Chỗ trong Service | Test phủ nó |
|---|---|
| 9 nhánh `switch` sắp xếp | `GetAll_SapXep_DungThuTu` với 9 `InlineData` |
| `if (PageNumber < 1)`, `if (PageSize ...)`, `if (Keyword ...)` | `GetAll_TimKiem_VaChanPhanTrangSai` |
| `?? throw NotFoundException` | `GetById_/Update_/Delete_KhongTonTai_NemNotFound` |
| Email trùng khi thêm / khi sửa | `Create_EmailTrung_...`, `Update_EmailCuaNguoiKhac_...` |
| `id != dto.Id`, `RowVersion is null` | `Update_IdKhongKhop_...`, `Update_ThieuRowVersion_...` |
| Đường thành công Create / Update / Delete | `Create_HopLe_...`, `Update_HopLe_...`, `Delete_LaXoaMem` |

Phần thường còn đỏ: khối `catch (DbUpdateConcurrencyException)` trong `UpdateAsync`, vì InMemory không giả lập được xung đột RowVersion. Khối này đã được **integration test số 6 chạy trên SQL thật** kiểm chứng (và nếu chạy coverage cho cả bộ test, nó cũng được tính vào).

### Bước 5 (tùy chọn) – Thêm thư mục kết quả vào `.gitignore`

```
TestResults/
CoverageReport/
```

Đây là file sinh ra, không cần đưa lên Git.

## ④ Luồng chạy

```
dotnet test --collect:"XPlat Code Coverage"
   ↓
Coverlet "gắn đánh dấu" vào từng dòng của DLL QuanLySinhVien
   ↓
Test chạy → dòng nào được thực thi thì được đánh dấu
   ↓
Xuất coverage.cobertura.xml
   ↓
reportgenerator lọc lớp Services.* → index.html (xanh / vàng / đỏ, % line và % branch)
```

## ⑤ Kiểm tra

1. Chạy 2 lệnh ở Bước 2 và Bước 3.
2. Mở `CoverageReport\index.html` → dòng `QuanLySinhVien.Services.SinhVienService` có **Line coverage ≥ 80%**.
3. Thử xóa (tạm) test `GetAll_SapXep_DungThuTu` rồi đo lại → phần trăm giảm, các nhánh `switch` chuyển đỏ. Khôi phục test → xanh lại. Đây là demo cho thấy coverage phản ánh đúng test.

## ⑥ Từ khóa

Code Coverage · Line coverage · Branch coverage · Coverlet · `XPlat Code Coverage` · Cobertura XML · ReportGenerator · `-classfilters`

## ⑦ Câu hỏi vấn đáp

**H: Code coverage là gì?**
Đ: Tỉ lệ phần trăm code được thực thi khi chạy test. Em đo bằng Coverlet, xuất HTML bằng ReportGenerator, chỉ tính tầng Service.

**H: Line coverage khác Branch coverage?**
Đ: Line coverage đếm **dòng** đã chạy. Branch coverage đếm **nhánh** của điều kiện: `if (a || b)` có thể chạy dòng đó nhưng chưa thử hết các trường hợp đúng/sai. Branch coverage khắt khe hơn.

**H: Coverage 100% có nghĩa là không có lỗi không?**
Đ: Không. Coverage chỉ nói **dòng đã được chạy**, không nói **kết quả có đúng không**. Một test không có `Assert` vẫn làm tăng coverage. Chất lượng nằm ở các `Assert`.

**H: Vì sao chỉ đo tầng Service?**
Đ: Service chứa nghiệp vụ, là nơi lỗi gây hậu quả nhất. Controller giờ chỉ 1-2 dòng gọi Service, đã được integration test bao phủ. Đo cả Program.cs hay Migration chỉ làm loãng con số.

**H: Vì sao phải tách Service (Tuần 5-6) thì mới đo được?**
Đ: Trước đó logic nằm trong Controller, không unit test được, nên cũng không có gì để đo theo tầng. Tách Service làm cho nghiệp vụ vừa test được vừa đo được.

---

# YÊU CẦU 3 – OPTIMISTIC CONCURRENCY VỚI ROWVERSION

## ① Vấn đề trước khi làm – "Lost Update" (mất cập nhật)

Kịch bản thực tế:

| Thời điểm | Admin A | Admin B |
|---|---|---|
| 9:00 | Mở form sửa SV #5 (Tuổi 20) | Mở form sửa SV #5 (Tuổi 20) |
| 9:01 | Sửa Họ tên → **Lưu** ✓ | |
| 9:02 | | Sửa Tuổi → **Lưu** ✓ |

Form của B vẫn chứa **họ tên cũ** (lấy lúc 9:00). Khi B lưu, toàn bộ form được gửi lên → **họ tên A vừa sửa bị ghi đè lại bằng tên cũ**. Không ai báo lỗi, A không hề biết thay đổi của mình đã mất. Đây gọi là **Lost Update**.

## ② Ý tưởng giải quyết

Có 2 cách chống:

| Cách | Làm thế nào | Nhược điểm |
|---|---|---|
| **Pessimistic (bi quan)** | A mở form thì **khóa** dòng, B phải đợi | A mở form rồi đi ăn trưa → B bị khóa cả tiếng |
| **Optimistic (lạc quan)** ✅ | Không khóa. Cho mọi người sửa thoải mái, **chỉ kiểm tra lúc lưu**: "dữ liệu có bị ai đổi kể từ lúc tôi mở form không?" | Người lưu sau phải tải lại và sửa lại (chấp nhận được vì xung đột hiếm) |

Ứng dụng web chọn **Optimistic**. Công cụ để "biết dữ liệu có bị đổi không" là cột **`rowversion`** của SQL Server:

- Là một số 8 byte, **SQL Server tự tăng mỗi khi dòng bị UPDATE**. Ứng dụng không bao giờ tự gán.
- Giống **số phiên bản của tài liệu**: mở form lúc phiên bản 7; lúc lưu nếu DB vẫn là 7 → cho lưu (DB tự lên 8); nếu DB đã là 8 (ai đó lưu trước) → từ chối.

## ③ Các bước làm

### Bước 1 – Thêm cột RowVersion vào DB và Model

SQL:

```sql
ALTER TABLE SinhVien ADD RowVersion ROWVERSION;
```

`Models/SinhVien.cs`:

```csharp
[Timestamp]                              // ← báo EF: đây là cột concurrency
public byte[]? RowVersion { get; set; }
```

| Thành phần | Ý nghĩa |
|---|---|
| `[Timestamp]` | Báo EF Core: (1) cột này do DB tự sinh, đừng ghi vào; (2) **đưa nó vào điều kiện WHERE** khi UPDATE/DELETE |
| `byte[]` | `rowversion` là 8 byte nhị phân |

Nhờ `[Timestamp]`, khi lưu EF sinh SQL dạng:

```sql
UPDATE SinhVien SET HoTen = @p0, Email = @p1, Tuoi = @p2
WHERE Id = @id AND RowVersion = @rowVersionCu;
-- EF kiểm tra: số dòng bị ảnh hưởng = 0 → ném DbUpdateConcurrencyException
```

### Bước 2 – Gửi RowVersion ra ngoài qua DTO

`SinhVienDto` thêm:

```csharp
public byte[]? RowVersion { get; set; }
```

Trong Service, cả `GetAllAsync` (trong `Select`) và `ToDto` đều trả `RowVersion = s.RowVersion`. Khi chuyển thành JSON, `byte[]` được tự động mã hóa thành chuỗi **Base64**, ví dụ `"AAAAAAAAB9E="`.

### Bước 3 – Service: so RowVersion của client với DB

`Services/SinhVienService.cs` – hàm `UpdateAsync`:

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

    // ★ Dùng RowVersion của lúc client MỞ FORM, không phải RowVersion vừa đọc từ DB
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
        throw new ConflictException(
            "Dữ liệu sinh viên đã bị người khác thay đổi. Vui lòng tải lại trang rồi sửa lại!");
    }
}
```

| Dòng | Ý nghĩa |
|---|---|
| `RowVersion is null` → 400 | Client không gửi phiên bản thì không biết so với gì → từ chối, tránh lưu "mù" |
| `TimHoacBaoLoiAsync(id)` | Đọc sinh viên từ DB. Lúc này `sv.RowVersion` là phiên bản **mới nhất** trong DB |
| **`OriginalValue = dto.RowVersion`** | **Dòng quan trọng nhất.** EF dùng `OriginalValue` để đặt vào `WHERE RowVersion = ...`. Nếu để nguyên, EF sẽ so với phiên bản vừa đọc (luôn khớp) → không bao giờ phát hiện xung đột. Gán bằng phiên bản client cầm **lúc mở form** thì mới so đúng |
| Gán `HoTen`, `Email`, `Tuoi` | Cập nhật dữ liệu mới |
| `catch (DbUpdateConcurrencyException)` | EF ném lỗi này khi `UPDATE ... WHERE RowVersion = cũ` ảnh hưởng **0 dòng** |
| Đổi thành `ConflictException` | `ExceptionMiddleware` (Tuần 3-4) trả **409 Conflict** + JSON chuẩn, Angular hiện toast |

Thứ tự kiểm tra có chủ ý: lỗi rẻ (sai Id, thiếu RowVersion → 400) trước, rồi mới truy vấn DB (404), kiểm email (409), cuối cùng mới đến xung đột phiên bản (409).

### Bước 4 – Audit không ghi RowVersion

`Data/AuditSaveChangesInterceptor.cs`:

```csharp
SensitiveProperties = { "PasswordHash", "RowVersion" }
```

RowVersion là dữ liệu kỹ thuật, đổi sau mỗi lần UPDATE. Ghi vào lịch sử chỉ gây nhiễu, nên loại ra cùng với mật khẩu.

### Bước 5 – Angular: giữ RowVersion khi mở form, gửi lại khi lưu

`models/sinh-vien.ts`:

```ts
rowVersion?: string;   // chuỗi Base64 nhận từ API
```

`sinh-vien.ts` – hàm `sua()` (mở form sửa) phải **sao chép cả rowVersion**:

```ts
this.sinhVien = {
  id: sv.id,
  hoTen: sv.hoTen,
  email: sv.email,
  tuoi: sv.tuoi,
  avatarUrl: sv.avatarUrl,
  rowVersion: sv.rowVersion   // giữ phiên bản lúc mở form để server so sánh khi lưu
};
```

> Lỗi em từng gặp: quên dòng `rowVersion` → bấm Lưu nhận toast "Thiếu RowVersion" (400). Thêm dòng này là hết.
> Gợi ý nhỏ: comment hiện tại ở dòng này đang ghi nhầm nội dung "giữ ảnh đại diện", nên sửa lại như trên.

Xử lý lỗi khi lưu:

```ts
error: (error) => {
  console.error('Lỗi sửa:', error);
  const laDungDo = error.status === 409 && error.error?.message?.includes('người khác thay đổi');
  if (laDungDo) {
    this.lamMoiForm();     // đóng form cũ (đang chứa dữ liệu lỗi thời)
    this.taiDanhSach();    // tải dữ liệu + RowVersion mới nhất
  }
}
```

| Dòng | Ý nghĩa |
|---|---|
| Toast lỗi | `errorInterceptor` (Tuần 3-4) tự hiện message từ JSON, ở đây không phải viết thêm |
| `laDungDo` | Phân biệt **409 đụng độ** với **409 email trùng**. Email trùng thì **giữ form** cho người dùng sửa email; đụng độ thì đóng form vì dữ liệu trong form đã cũ |
| `lamMoiForm()` + `taiDanhSach()` | Người dùng thấy ngay dữ liệu mới nhất, bấm Sửa lại sẽ nhận RowVersion mới |

### Bước 6 – Test

- **Unit test** (`SinhVienServiceTests`): `Update_ThieuRowVersion_NemBadRequest`, và các test Update khác đều gửi `RowVersion = { 1 }` (TaoDb gán sẵn vì InMemory không tự sinh).
- **Integration test** `HaiNguoiCungSua_NguoiSauBi409` trên SQL Server thật (vì chỉ SQL Server mới tự tăng rowversion):

```csharp
// A và B cùng mở form → cầm CÙNG một RowVersion
var formA = await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}");
var formB = await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}");
Assert.Equal(formA.RowVersion, formB.RowVersion);

// A lưu trước → 204
formA.HoTen = "A đã sửa";
Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync(..., formA)).StatusCode);

// B lưu sau với RowVersion cũ → 409
formB.HoTen = "B đã sửa";
Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(..., formB)).StatusCode);

// Dữ liệu trong DB là của A, RowVersion đã đổi
var final = await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}");
Assert.Equal("A đã sửa", final.HoTen);
Assert.NotEqual(formA.RowVersion, final.RowVersion);
```

Test này **tái hiện đúng bảng ở mục ①** và chứng minh Lost Update đã bị chặn.

## ④ Luồng chạy – ví dụ A và B cùng sửa SV #5

```
DB: SV #5, RowVersion = 7
   ↓
A mở form (cầm 7)          B mở form (cầm 7)
   ↓
A lưu: PUT {..., rowVersion: 7}
   → OriginalValue = 7 → UPDATE ... WHERE Id=5 AND RowVersion=7 → 1 dòng ✓
   → SQL tự tăng RowVersion = 8 → 204
   ↓
B lưu: PUT {..., rowVersion: 7}
   → OriginalValue = 7 → UPDATE ... WHERE Id=5 AND RowVersion=7 → 0 dòng ✗
   → EF ném DbUpdateConcurrencyException
   → Service đổi thành ConflictException
   → ExceptionMiddleware: 409 {statusCode:409, message:"Dữ liệu sinh viên đã bị người khác thay đổi..."}
   ↓
Angular B: errorInterceptor hiện toast → đóng form → tải lại danh sách (RowVersion = 8)
   ↓
B bấm Sửa lại → thấy tên A vừa sửa → sửa tuổi → lưu với 8 → thành công
```

## ⑤ Kiểm tra trên web

1. Mở **2 tab** trình duyệt, cùng đăng nhập Admin, cùng ở trang sinh viên.
2. Tab 1 và Tab 2 cùng bấm **Sửa** một sinh viên.
3. Tab 1 đổi họ tên → **Lưu** → thành công.
4. Tab 2 (chưa tải lại) đổi tuổi → **Lưu** → toast đỏ *"Dữ liệu sinh viên đã bị người khác thay đổi..."*, form đóng, danh sách tải lại với tên mới của Tab 1.
5. Tab 2 bấm Sửa lại → lưu → thành công.
6. Kiểm tra thêm: F12 → tab **Network** → request PUT bị lỗi có status **409** và body JSON chuẩn.

> Nếu chạy API bằng F5 (Debug), Visual Studio có thể dừng ở `ConflictException`. Đó là debugger bắt exception, **không phải lỗi**: bấm Continue, hoặc chạy bằng **Ctrl+F5**.

## ⑥ Từ khóa

Lost Update · Optimistic Concurrency · Pessimistic Locking · `rowversion` · `[Timestamp]` · Concurrency token · `OriginalValue` · `DbUpdateConcurrencyException` · 409 Conflict · Base64

## ⑦ Câu hỏi vấn đáp

**H: Optimistic khác Pessimistic Concurrency thế nào?**
Đ: Pessimistic khóa dữ liệu ngay khi mở để sửa, người khác phải đợi. Optimistic không khóa, chỉ kiểm tra lúc lưu xem có ai đổi chưa. Web chọn Optimistic vì người dùng có thể mở form rất lâu và xung đột hiếm khi xảy ra.

**H: RowVersion là gì? Ai tăng nó?**
Đ: Cột 8 byte của SQL Server, **SQL Server tự tăng** mỗi khi dòng bị UPDATE. Ứng dụng chỉ đọc và gửi lại, không bao giờ tự gán.

**H: Vì sao phải gán `OriginalValue = dto.RowVersion`?**
Đ: Vì Service đọc sinh viên từ DB ngay trước khi lưu, nên RowVersion đọc được luôn là mới nhất, so với chính nó thì luôn khớp. Phải thay bằng RowVersion client cầm **lúc mở form** thì EF mới đặt đúng giá trị cũ vào `WHERE` và phát hiện được thay đổi xen giữa.

**H: Vì sao không so sánh `dto.RowVersion` với `sv.RowVersion` bằng `if` cho đơn giản?**
Đ: Vẫn còn khoảng hở giữa lúc đọc và lúc ghi: người khác có thể lưu đúng vào khoảng đó. Đưa điều kiện vào **chính câu UPDATE** thì SQL Server kiểm tra và ghi trong **một thao tác nguyên tử**, không còn khoảng hở.

**H: Vì sao trả 409 mà không phải 400?**
Đ: 400 là request sai định dạng. 409 Conflict nghĩa là request hợp lệ nhưng **xung đột với trạng thái hiện tại** của tài nguyên, đúng với tình huống này.

**H: Thiếu RowVersion thì sao?**
Đ: Service trả 400 "Thiếu RowVersion". Không cho lưu mà không có phiên bản, vì như vậy sẽ quay lại tình trạng ghi đè mù.

**H: Làm sao Angular phân biệt 409 email trùng với 409 đụng độ?**
Đ: Hiện em dựa vào nội dung message. Đơn giản nhưng phụ thuộc câu chữ; nếu muốn chắc chắn hơn có thể thêm một trường mã lỗi riêng (ví dụ `errorCode: "CONCURRENCY"`) vào JSON lỗi.

**H: Vì sao test đụng độ phải là integration test?**
Đ: InMemory không tự tăng RowVersion như SQL Server nên không tái hiện được xung đột thật. Chạy trên SQL Server trong Docker thì tái hiện chính xác.

---

# TỔNG HỢP – BỨC TRANH CHUNG CỦA TUẦN 7

## Ba yêu cầu bổ trợ cho nhau

```
                 ┌───────────────────────────────────────────┐
                 │  YÊU CẦU 3: Optimistic Concurrency        │  ← tính năng mới
                 │  RowVersion + OriginalValue + 409         │
                 └───────────────────┬───────────────────────┘
                                     │ được chứng minh bởi
          ┌──────────────────────────┴──────────────────────────┐
          ▼                                                     ▼
┌─────────────────────────────┐                 ┌─────────────────────────────┐
│ Unit Test (Tuần 5-6)        │                 │ YÊU CẦU 1: Integration Test │
│ Service + InMemory          │                 │ HTTP → ... → SQL (Docker)   │
│ nhanh, phủ mọi nhánh        │                 │ sát thực tế, 6 kịch bản     │
└──────────────┬──────────────┘                 └─────────────────────────────┘
               │ được đo bởi
               ▼
┌─────────────────────────────┐
│ YÊU CẦU 2: Code Coverage    │
│ Coverlet + ReportGenerator  │
│ Service ≥ 80%               │
└─────────────────────────────┘
```

- **Concurrency** bảo vệ **dữ liệu khi nhiều người dùng cùng lúc**.
- **Integration Test** chứng minh **toàn bộ hệ thống** (không chỉ Service) chạy đúng trên **SQL Server thật**.
- **Coverage** trả lời câu hỏi **"test đã đủ chưa?"** bằng con số đo được.
- Tuần 7 dùng lại toàn bộ các tuần trước: Middleware (Tuần 3-4) trả 409/400/404, Query Filter (Tuần 5-6) được test trên SQL thật, Service tách ở Tuần 5-6 là thứ được đo coverage.

## Kim tự tháp kiểm thử (Testing Pyramid) của dự án

| Tầng | Số lượng | Tốc độ | Dự án |
|---|---|---|---|
| Unit Test | Nhiều | Mili giây | `SinhVienServiceTests` (InMemory) |
| Integration Test | Ít hơn | Giây | `SinhVienApiTests` (TestContainers) |
| E2E / thủ công | Ít nhất | Chậm | Kiểm tra trên web (2 tab) |

## Nguyên lý cốt lõi (nói được câu này là "ăn điểm")

> **"Test phải chạy trong môi trường giống thật nhất có thể"**: TestContainers cho SQL Server thật, dùng một lần.
> **"Đo được thì mới quản lý được"**: coverage biến cảm giác "chắc đủ rồi" thành con số.
> **"Không khóa, chỉ kiểm tra lúc lưu"**: Optimistic Concurrency chặn Lost Update mà không làm người dùng phải chờ.

---

# KỊCH BẢN TRÌNH BÀY (~3-4 phút)

**(Mở đầu – 20 giây)**
"Tuần 7 em làm 3 yêu cầu: **Integration Test với TestContainers**, **đo Code Coverage tầng Service đạt trên 80%**, và **xử lý đụng độ dữ liệu bằng Optimistic Concurrency**. Mục tiêu chung là chứng minh hệ thống chạy đúng trong điều kiện thật."

**(Concurrency – 70 giây)**
"Em bắt đầu từ vấn đề: hai Admin cùng mở form sửa một sinh viên. A lưu trước, B lưu sau thì toàn bộ form cũ của B ghi đè lên thay đổi của A mà không ai biết. Đây gọi là Lost Update.
Em chọn Optimistic Concurrency: không khóa, chỉ kiểm tra lúc lưu. Em thêm cột `rowversion` mà SQL Server tự tăng mỗi lần UPDATE, đánh dấu `[Timestamp]` để EF đưa nó vào điều kiện WHERE. Angular giữ RowVersion lúc mở form và gửi lại khi lưu. Trong Service, dòng quan trọng nhất là gán `OriginalValue` bằng RowVersion của client, để EF so với phiên bản lúc mở form chứ không phải phiên bản vừa đọc. Nếu UPDATE không trúng dòng nào, EF ném `DbUpdateConcurrencyException`, em đổi thành `ConflictException`, Middleware trả 409.
*(Demo 2 tab)* Tab 1 sửa và lưu thành công. Tab 2 lưu sau thì nhận thông báo dữ liệu đã bị người khác thay đổi, form đóng và danh sách tải lại dữ liệu mới."

**(Integration Test – 70 giây)**
"Unit test tuần trước chỉ kiểm tra Service trên DB giả trong RAM, mà DB giả không tự tăng RowVersion. Nên em viết Integration Test: `WebApplicationFactory` chạy toàn bộ API trong bộ nhớ, còn TestContainers bật một SQL Server 2022 thật trong Docker, dùng xong tự xóa. Em ghi đè connection string và thêm chốt an toàn để không bao giờ đụng vào DB thật.
Test đi đúng đường thật: đăng ký, đăng nhập lấy JWT, gọi API, qua Middleware, Controller, Service xuống SQL. Em có 6 kịch bản: thêm rồi đọc lại, email trùng 409, xóa mềm, phân quyền 401 và 403, dữ liệu sai 400, và hai người cùng sửa thì người sau bị 409.
*(Demo)* Chạy test, trong Docker Desktop thấy container SQL Server bật lên rồi tự biến mất, tất cả test đều xanh."

**(Coverage – 40 giây)**
"Cuối cùng, để trả lời 'test đã đủ chưa', em đo Code Coverage bằng Coverlet và xuất báo cáo HTML bằng ReportGenerator, lọc chỉ tầng Service. `SinhVienService` đạt trên 80% line coverage. Báo cáo tô đỏ chỗ chưa test để em bổ sung, ví dụ em dùng `[Theory]` với 9 bộ dữ liệu để phủ hết các nhánh sắp xếp. Tuy vậy em hiểu coverage cao không có nghĩa là không có lỗi; chất lượng nằm ở các Assert."

**(Kết – 20 giây)**
"Tóm lại: Concurrency bảo vệ dữ liệu khi nhiều người dùng cùng lúc, Integration Test chứng minh toàn hệ thống chạy đúng trên SQL Server thật, và Coverage cho em con số đo được về chất lượng test."
