# TUẦN 7 – HƯỚNG DẪN LÀM · PHẦN TRÌNH BÀY · TỪ KHÓA

Tuần 7 có 3 yêu cầu:

1. **Integration Test với TestContainers** – ít nhất 2 kịch bản gọi API từ Controller xuống tận DB, chạy pass.
2. **Code Coverage** – tầng Service đạt ≥ 80%.
3. **Optimistic Concurrency** – dùng RowVersion để hai người cùng sửa thì người lưu sau bị chặn.

Nên làm theo thứ tự **3 → 1 → 2**: làm tính năng RowVersion trước, rồi viết integration test (kiểm chứng được cả RowVersion), cuối cùng đo coverage.

---

# PHẦN 1 – HƯỚNG DẪN LÀM

## YÊU CẦU 3 – OPTIMISTIC CONCURRENCY (làm trước)

**Bước 1. Thêm cột vào DB** (SSMS, database `QLSINHVIEN`):
```sql
ALTER TABLE SinhVien ADD RowVersion ROWVERSION;
```
→ SQL Server tự sinh và **tự tăng** giá trị này mỗi khi dòng bị UPDATE.

**Bước 2. Model** – `Models/SinhVien.cs`, thêm:
```csharp
[Timestamp]
public byte[]? RowVersion { get; set; }
```
→ `[Timestamp]` báo EF: cột do DB sinh, và phải đưa vào `WHERE` khi UPDATE.

**Bước 3. DTO** – `DTOs/SinhVienDto.cs`, thêm:
```csharp
public byte[]? RowVersion { get; set; }
```
Trong `SinhVienService`, thêm `RowVersion = s.RowVersion` vào phần `Select(...)` của `GetAllAsync` và vào hàm `ToDto`.
→ Client nhận được phiên bản (dạng chuỗi Base64 trong JSON).

**Bước 4. Service** – sửa `UpdateAsync`:
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

    // So với phiên bản lúc client MỞ FORM, không phải phiên bản vừa đọc từ DB
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
→ EF sinh `UPDATE ... WHERE Id = @id AND RowVersion = @cũ`. Nếu đã có người lưu trước, câu lệnh trúng 0 dòng → EF ném `DbUpdateConcurrencyException` → trả **409**.

**Bước 5. Audit** – `AuditSaveChangesInterceptor`, thêm `"RowVersion"` vào danh sách bỏ qua:
```csharp
private static readonly HashSet<string> SensitiveProperties = new() { "PasswordHash", "RowVersion" };
```

**Bước 6. Angular**
- `models/sinh-vien.ts`: thêm `rowVersion?: string;`
- `sinh-vien.ts` → hàm `sua(sv)`: thêm `rowVersion: sv.rowVersion` khi sao chép vào form.
- `sinh-vien.ts` → hàm `luu()`, nhánh `error` khi sửa:
```ts
const laDungDo = error.status === 409 && error.error?.message?.includes('người khác thay đổi');
if (laDungDo) {
  this.lamMoiForm();   // đóng form có dữ liệu cũ
  this.taiDanhSach();  // tải dữ liệu + RowVersion mới
}
```

**Bước 7. Unit test** – trong `SinhVienServiceTests.TaoDb()` gán `RowVersion = new byte[] { 1 }` cho dữ liệu mẫu; các test Update gửi kèm `RowVersion = new byte[] { 1 }`; thêm test:
```csharp
[Fact]
public async Task Update_ThieuRowVersion_NemBadRequest()
{
    await Assert.ThrowsAsync<BadRequestException>(() => new SinhVienService(TaoDb())
        .UpdateAsync(1, new SinhVienDto { Id = 1, HoTen = "X", Email = "c@x.com", Tuoi = 20 }));
}
```

**Kiểm tra:** mở 2 tab cùng đăng nhập Admin → cả hai bấm Sửa cùng một sinh viên → tab 1 Lưu thành công → tab 2 Lưu → hiện *"Dữ liệu sinh viên đã bị người khác thay đổi..."*, form đóng, danh sách tải lại. F12 → Network thấy **409**.

---

## YÊU CẦU 1 – INTEGRATION TEST VỚI TESTCONTAINERS

**Bước 1. Chuẩn bị Docker** – cài Docker Desktop (bật Virtualization trong BIOS + WSL2). Kiểm tra: `docker run hello-world`.

**Bước 2. Cài gói NuGet** cho project `QuanLySinhVien.Tests`:
- `Microsoft.AspNetCore.Mvc.Testing`
- `Testcontainers.MsSql`

**Bước 3. `Program.cs`** – thêm dòng cuối cùng:
```csharp
public partial class Program { }
```
→ để project test nhìn thấy lớp `Program` (do Program.cs viết kiểu top-level statements).

**Bước 4. Tạo `QuanLySinhVien.Tests/IntegrationTests/ApiFactory.cs`:**
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLySinhVien.Data;
using Testcontainers.MsSql;

namespace QuanLySinhVien.Tests.IntegrationTests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Thay DB thật bằng DB trong container
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();                                   // bật container
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Database.GetConnectionString()!.Contains("QLSINHVIEN"))
            throw new InvalidOperationException("Test đang trỏ vào DB thật!");   // chốt an toàn
        await db.Database.EnsureCreatedAsync();                   // tạo bảng
    }

    public new async Task DisposeAsync() => await _db.DisposeAsync();   // xóa container
}
```

**Bước 5. Tạo `IntegrationTests/SinhVienApiTest.cs`** – lớp test dùng chung `ApiFactory`, có 2 helper:
```csharp
public class SinhVienApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SinhVienApiTests(ApiFactory factory) => _factory = factory;

    // Đăng ký → đổi role trong DB → đăng nhập → gắn token
    private async Task<HttpClient> TaoClientAsync(string role)
    {
        var username = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/XacThuc/dangky",
            new DangKyDto { Username = username, Password = "123456", FullName = "Test" }))
            .EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.NguoiDung.SingleAsync(u => u.Username == username);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var res = await client.PostAsJsonAsync("/api/XacThuc/dangnhap",
            new DangNhapDto { Username = username, Password = "123456" });
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Email ngẫu nhiên để các test không trùng nhau
    private static SinhVienDto SinhVienMoi() => new()
    {
        HoTen = "Nguyễn Văn Test",
        Email = $"{Guid.NewGuid():N}@test.com",
        Tuoi = 20
    };
```

**Bước 6. Viết kịch bản** (tối thiểu 2; dự án có 6). Hai kịch bản tiêu biểu:
```csharp
    [Fact]
    public async Task TaoSinhVien_RoiDocLai_DungDuLieu()
    {
        var admin = await TaoClientAsync("Admin");
        var dto = SinhVienMoi();

        var create = await admin.PostAsJsonAsync("/api/SinhVien", dto);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<SinhVienDto>();

        var get = await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created!.Id}");
        Assert.Equal(dto.Email, get!.Email);
    }

    [Fact]
    public async Task HaiNguoiCungSua_NguoiSauBi409()
    {
        var admin = await TaoClientAsync("Admin");
        var created = (await (await admin.PostAsJsonAsync("/api/SinhVien", SinhVienMoi()))
            .Content.ReadFromJsonAsync<SinhVienDto>())!;

        var formA = (await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}"))!;
        var formB = (await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}"))!;

        formA.HoTen = "A đã sửa";
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/SinhVien/{created.Id}", formA)).StatusCode);

        formB.HoTen = "B đã sửa";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/SinhVien/{created.Id}", formB)).StatusCode);

        var final = (await admin.GetFromJsonAsync<SinhVienDto>($"/api/SinhVien/{created.Id}"))!;
        Assert.Equal("A đã sửa", final.HoTen);
    }
}
```
4 kịch bản còn lại trong dự án: email trùng → 409, xóa mềm → 404 nhưng DB vẫn còn, phân quyền 401/403, tuổi sai → 400.

**Kiểm tra:** bật Docker Desktop → Visual Studio **Test → Test Explorer → Run All** (hoặc `dotnet test --filter "FullyQualifiedName~IntegrationTests"`) → 6 test xanh. Trong lúc chạy, Docker Desktop hiện container SQL Server, chạy xong tự biến mất.

---

## YÊU CẦU 2 – CODE COVERAGE ≥ 80%

**Bước 1.** Kiểm tra `QuanLySinhVien.Tests.csproj` có `coverlet.collector` (có sẵn).

**Bước 2.** Cài ReportGenerator (một lần):
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

**Bước 3.** Bật Docker, tại `D:\TT\QuanLySinhVien` xóa kết quả cũ rồi chạy test kèm đo:
```bash
rmdir /s /q TestResults
rmdir /s /q CoverageReport
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

**Bước 4.** Xuất báo cáo, chỉ tính tầng Service:
```bash
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```

**Bước 5.** Mở `CoverageReport\index.html` → xem **Line coverage** của `SinhVienService` (≥ 80%). Bấm vào lớp để xem dòng xanh (đã test) / đỏ (chưa test); chỗ đỏ thì viết thêm test rồi đo lại.

> Lần đo gần nhất trên máy: **Line 100% (81/81), Branch 96,8% (31/32)** – nhưng đo trước lần sửa cuối của Service, nên chạy lại để lấy số mới.

---

# PHẦN 2 – TÓM TẮT THÀNH PHẦN TRÌNH BÀY

### Phần 1 – Integration Test với TestContainers (~1 phút)

> "Unit test tuần trước chỉ kiểm tra riêng tầng Service trên database giả trong RAM. Để chứng minh các tầng ghép lại chạy đúng, em viết Integration Test. Em dùng **WebApplicationFactory** để chạy toàn bộ API ngay trong test, và **TestContainers** để tự bật một **SQL Server 2022 thật trong Docker**, dùng xong tự xóa. Trong `ApiFactory`, em ghi đè chuỗi kết nối sang database trong container, có chốt an toàn không cho trỏ vào database thật, rồi tạo bảng bằng `EnsureCreated`. Mỗi test đi đúng đường của người dùng: đăng ký, đăng nhập lấy JWT, gọi API qua middleware, phân quyền, Controller, Service xuống SQL Server. Yêu cầu tối thiểu 2 kịch bản, em viết 6: thêm rồi đọc lại, email trùng 409, xóa mềm, phân quyền 401/403, dữ liệu sai 400, và hai người cùng sửa thì người sau bị 409. Tất cả đều pass."

### Phần 2 – Code Coverage (~45 giây)

> "Để biết test đã đủ chưa, em đo **Code Coverage** – tỉ lệ code được chạy khi chạy test. Em dùng **Coverlet** thu thập số liệu và **ReportGenerator** xuất báo cáo HTML, lọc chỉ tầng Service vì đó là nơi chứa nghiệp vụ. `SinhVienService` đạt **[số]%** số dòng và **[số]%** số nhánh, vượt yêu cầu 80%. Báo cáo tô đỏ chỗ chưa test để em bổ sung, ví dụ em dùng `[Theory]` với 9 bộ dữ liệu để phủ hết các nhánh sắp xếp. Tuy nhiên coverage chỉ cho biết dòng nào đã chạy, không chứng minh code đúng – chất lượng nằm ở các Assert."

### Phần 3 – Optimistic Concurrency (~1 phút)

> "Khi hai Admin cùng mở form sửa một sinh viên, người lưu sau sẽ ghi đè thay đổi của người lưu trước mà không ai biết – gọi là **Lost Update**. Em chọn **Optimistic Concurrency**: không khóa dữ liệu, chỉ kiểm tra lúc lưu. Em thêm cột **rowversion** do SQL Server tự tăng mỗi lần sửa, đánh dấu `[Timestamp]` để EF đưa nó vào điều kiện WHERE. Angular giữ RowVersion lúc mở form và gửi lại khi lưu. Trong Service, em gán **OriginalValue** bằng RowVersion của client, để so với phiên bản lúc mở form. Nếu có người lưu trước, câu UPDATE không trúng dòng nào, EF ném `DbUpdateConcurrencyException`, em đổi thành 409. Angular nhận 409 thì đóng form và tải lại dữ liệu mới. *(Demo 2 tab.)*"

---

# PHẦN 3 – TỪ KHÓA CHÍNH

**1. Integration Test**
Integration Test · TestContainers · Docker · WebApplicationFactory · `public partial class Program` · IAsyncLifetime · IClassFixture · UseSetting · EnsureCreated · HttpClient · Bearer token

**2. Code Coverage**
Code Coverage · Line coverage · Branch coverage · Coverlet · ReportGenerator · `XPlat Code Coverage` · `classfilters`

**3. Optimistic Concurrency**
Lost Update · Optimistic Concurrency · Pessimistic Locking · rowversion · `[Timestamp]` · OriginalValue · DbUpdateConcurrencyException · 409 Conflict
