# TUẦN 7 – BẢN CHẤT · CÁCH LÀM · CÁC HÀM LIÊN QUAN · KIỂM TRA · ĐOẠN TRÌNH BÀY

> Người thực hiện: Nguyễn Thanh Phong · Dự án Quản lý Sinh viên
> Đối chiếu với code hiện tại trong `D:\TT\QuanLySinhVien` (ApiFactory.cs, SinhVienApiTest.cs, SinhVienService.cs, SinhVien.cs, sinh-vien.ts).

## Yêu cầu Tuần 7 gồm 3 phần

| # | Yêu cầu của mentor | Tiêu chí "đạt" |
|---|---|---|
| 1 | **Kiểm thử nâng cao (Integration Testing)** dùng **TestContainers** | Ít nhất **2 kịch bản** gọi API từ **Controller xuyên xuống tận DB**, chạy **pass** |
| 2 | **Quản lý chất lượng code (Code Coverage)** | Độ phủ **tầng Service ≥ 80%** |
| 3 | **Xử lý đụng độ dữ liệu (Optimistic Concurrency)** | Dùng **RowVersion**; hai người cùng sửa thì người lưu sau bị chặn |

Mỗi phần dưới đây có 5 mục: **A. Bản chất → B. Cách làm → C. Các hàm liên quan → D. Hướng dẫn kiểm tra → E. Đoạn trình bày**.

---

# YÊU CẦU 1 – INTEGRATION TEST VỚI TESTCONTAINERS

## A. Bản chất

**Integration Test là gì?** Unit test kiểm tra **từng bộ phận riêng lẻ**. Integration test kiểm tra **các bộ phận ghép lại có chạy đúng với nhau không**.

> Ví von: Unit test giống kiểm tra riêng động cơ, bánh xe, phanh. Integration test là **lắp cả chiếc xe lại rồi chạy thử trên đường thật**.

Trong dự án, một request thật phải đi qua rất nhiều tầng mà unit test không chạm tới:

```
HTTP → ExceptionMiddleware → Authentication (JWT) → Authorization ([Authorize]) → Validation (DTO)
     → Controller → SinhVienService → EF Core (Query Filter, Interceptor) → SQL Server
```

Integration test gửi **HTTP request thật** và kiểm tra **kết quả cuối cùng** (mã HTTP, JSON, dữ liệu trong DB).

**TestContainers là gì?** Một thư viện tự **bật một container Docker** khi test bắt đầu và **tự xóa** khi test xong. Ở đây container là **SQL Server 2022 thật**.

**Vì sao cần cả hai?**

| Nếu dùng... | Vấn đề |
|---|---|
| DB InMemory (như unit test) | Không phải SQL Server: không tự sinh `rowversion`, không kiểm ràng buộc như SQL thật |
| DB thật `QLSINHVIEN` | Làm bẩn dữ liệu thật; máy khác không có DB này thì không chạy được |
| **SQL Server trong Docker** ✅ | Giống thật 100%, sạch mỗi lần chạy, chạy được trên mọi máy có Docker |

**Bản chất ngắn gọn:** *"Dựng lại toàn bộ hệ thống trong một môi trường thật nhưng dùng một lần, rồi gọi nó y như Angular gọi."*

## B. Cách làm (6 bước)

> Mỗi bước gồm: **Làm gì → Vì sao → Nếu bỏ qua thì sao → Dấu hiệu làm đúng**.

### Bước 1 – Cài và kiểm tra Docker Desktop

**Làm gì:**
- Bật **Virtualization** trong BIOS (thường nằm ở mục *Advanced / CPU Configuration*, tên Intel VT-x hoặc AMD SVM).
- Bật WSL2 bằng CMD quyền Administrator:
  ```bash
  dism /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
  dism /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
  wsl --update
  ```
- Cài Docker Desktop. Nếu ổ C ít chỗ, vào **Settings → Resources → Disk image location** để chuyển sang ổ D (máy em dùng `D:\DockerData`).
- Kiểm tra: `docker run hello-world`.

**Vì sao:** TestContainers **không tự chạy SQL Server**. Nó chỉ "ra lệnh" cho Docker bật container. Không có Docker thì không có gì để ra lệnh. Docker trên Windows chạy bên trong máy ảo Linux nhẹ (WSL2), nên cần bật ảo hóa.

**Nếu bỏ qua:** chạy test sẽ lỗi kiểu *"Docker is either not running or misconfigured"*, toàn bộ integration test đỏ.

**Dấu hiệu làm đúng:** lệnh `hello-world` in ra *"Hello from Docker!"*; Docker Desktop hiện **Engine running**.

---

### Bước 2 – Thêm 2 gói NuGet vào project test

**Làm gì:** chuột phải `QuanLySinhVien.Tests` → **Manage NuGet Packages** → cài:
- `Microsoft.AspNetCore.Mvc.Testing`
- `Testcontainers.MsSql`

Sau khi cài, file `QuanLySinhVien.Tests.csproj` có:
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
<PackageReference Include="Testcontainers.MsSql" Version="4.15.0" />
```

**Vì sao:**
| Gói | Cung cấp | Dùng để |
|---|---|---|
| `Mvc.Testing` | `WebApplicationFactory<T>` | Chạy toàn bộ API **trong bộ nhớ** của test, không cần bấm F5 mở server |
| `Testcontainers.MsSql` | `MsSqlBuilder`, `MsSqlContainer` | Bật / tắt SQL Server trong Docker bằng code C# |

Project test cũng phải **tham chiếu** tới project API (`<ProjectReference Include="..\QuanLySinhVien\QuanLySinhVien.csproj" />`) để dùng được `Program`, `AppDbContext`, các DTO.

**Nếu bỏ qua:** lỗi biên dịch *"The type or namespace name 'WebApplicationFactory' could not be found"*.

**Dấu hiệu làm đúng:** Build project test thành công.

---

### Bước 3 – Thêm `public partial class Program { }` vào cuối `Program.cs`

**Làm gì:** thêm đúng 1 dòng vào **cuối cùng** của `Program.cs`, sau `app.Run();`:
```csharp
public partial class Program { }
```

**Vì sao:** `Program.cs` viết kiểu **top-level statements** – không có `class Program { static void Main() ... }`. Trình biên dịch vẫn **tự sinh** một lớp `Program` để chứa code đó, nhưng lớp tự sinh có quyền truy cập `internal` (chỉ dùng được trong project API). `WebApplicationFactory<Program>` nằm ở **project test** cần nhìn thấy `Program` làm "điểm bắt đầu" để biết phải chạy ứng dụng nào.
- `partial` = "đây là **phần còn lại** của lớp Program mà trình biên dịch đã sinh" → hai phần ghép lại thành một lớp.
- `public` = cho project khác nhìn thấy.

**Nếu bỏ qua:** lỗi *"'Program' is inaccessible due to its protection level"*.

**Dấu hiệu làm đúng:** viết `WebApplicationFactory<Program>` trong project test không còn gạch đỏ.

---

### Bước 4 – Viết `ApiFactory` (dựng API + DB cho test)

**Làm gì:** tạo file `QuanLySinhVien.Tests/IntegrationTests/ApiFactory.cs` (code đầy đủ ở mục C2). Lớp này làm 4 việc theo thứ tự:

| Thứ tự | Việc | Code | Giải thích |
|---|---|---|---|
| 4.1 | **Khai báo** container | `new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build()` | Mới "đặt hàng", chưa bật. Ghi rõ phiên bản image 2022 để lần nào chạy cũng giống nhau |
| 4.2 | **Ghi đè** chuỗi kết nối | `ConfigureWebHost` → `builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString())` | API vốn đọc chuỗi kết nối từ `appsettings.json` (trỏ `QLSINHVIEN`). `UseSetting` chèn một giá trị **ưu tiên cao hơn** cho đúng khóa đó → API kết nối vào container. Đây là lý do Tuần 1 phải **không hardcode** cấu hình |
| 4.3 | **Bật** container + **tạo bảng** | `InitializeAsync`: `_db.StartAsync()` → tạo scope lấy `AppDbContext` → chốt an toàn → `EnsureCreatedAsync()` | Container mới tinh **chưa có bảng nào**. `EnsureCreated` đọc các Model C# và tạo bảng tương ứng (`SinhVien` có `IsDeleted`, `RowVersion`; `Users`; `AuditLogs`) |
| 4.4 | **Dọn** container | `DisposeAsync` → `_db.DisposeAsync()` | Xóa container, không để rác trên máy |

**Vì sao kế thừa `IAsyncLifetime`:** bật container và tạo bảng là việc **bất đồng bộ** và tốn thời gian, không làm được trong constructor. `IAsyncLifetime` cho xUnit biết: "gọi `InitializeAsync` **trước** khi chạy test đầu tiên, gọi `DisposeAsync` **sau** test cuối cùng".

**Vì sao phải `CreateScope()`:** `AppDbContext` đăng ký kiểu **Scoped** (mỗi request một instance). Ngoài request HTTP thì không có scope sẵn, nên phải tự tạo một scope để xin DbContext; `using` giúp scope được hủy ngay sau khi dùng.

**Vì sao có chốt an toàn:** nếu vì lý do nào đó việc ghi đè ở 4.2 không có tác dụng, test sẽ chạy trên DB thật và tạo hàng loạt dữ liệu rác. Câu kiểm tra `Contains("QLSINHVIEN")` làm test **dừng ngay** trước khi kịp ghi gì.

**Nếu bỏ qua 4.2:** test chạy vào DB thật. **Nếu bỏ qua `EnsureCreated`:** mọi test lỗi *"Invalid object name 'SinhVien'"*.

**Dấu hiệu làm đúng:** khi chạy test, Docker Desktop xuất hiện container SQL Server; test xong thì container biến mất.

---

### Bước 5 – Viết các hàm hỗ trợ (helper)

**5.1. `TaoClientAsync(string role)` – tạo "người dùng đã đăng nhập"**

| Bước nhỏ | Code | Vì sao |
|---|---|---|
| ① Tên ngẫu nhiên | `$"user_{Guid.NewGuid():N}"` | Mỗi lần gọi một tài khoản mới, không bị lỗi "Tài khoản đã tồn tại" |
| ② Tạo client | `_factory.CreateClient()` | `HttpClient` gọi thẳng vào API trong bộ nhớ |
| ③ Đăng ký | `POST /api/XacThuc/dangky` + `EnsureSuccessStatusCode()` | Đi đúng luồng đăng ký thật (có BCrypt). `EnsureSuccess...` để nếu đăng ký hỏng thì báo lỗi ngay chỗ này, dễ tìm nguyên nhân |
| ④ Đổi role trong DB | scope → `NguoiDung.SingleAsync(...)` → `user.Role = role` → `SaveChangesAsync()` | API đăng ký luôn cho role `GiangVien`; muốn test quyền Admin phải nâng role trực tiếp trong DB |
| ⑤ Đăng nhập | `POST /api/XacThuc/dangnhap` → đọc `token` từ JSON | Lấy **JWT thật**, có chữ ký thật |
| ⑥ Gắn token | `client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token)` | Từ đây mọi request của client này đều mang token – giống `jwtInterceptor` bên Angular |

> Lưu ý thứ tự: phải đổi role **trước** khi đăng nhập, vì role được ghi vào token **lúc đăng nhập**.

**5.2. `SinhVienMoi()` – dữ liệu sinh viên mẫu**
- Email dạng `{Guid}@test.com` → **không bao giờ trùng**.
- **Vì sao:** tất cả test dùng **chung một container**. Nếu hai test cùng tạo email `a@test.com`, test chạy sau bị 409 "email trùng" dù nó không test chuyện đó → test đỏ oan.

---

### Bước 6 – Viết các kịch bản test

**Làm gì:** tạo lớp `SinhVienApiTests : IClassFixture<ApiFactory>` trong `SinhVienApiTest.cs`, mỗi kịch bản là một hàm `[Fact]`.

**Vì sao `IClassFixture<ApiFactory>`:** xUnit tạo **một** `ApiFactory` duy nhất và đưa vào constructor của lớp test cho **mọi** test dùng chung. Bật container mất vài giây đến vài chục giây; 6 test mà mỗi test bật một container thì rất chậm.

**Cách viết một kịch bản – theo Arrange / Act / Assert**, ví dụ `TaoSinhVien_EmailTrung_Tra409`:

| Phần | Code | Ý nghĩa |
|---|---|---|
| **Arrange** (chuẩn bị) | `var admin = await TaoClientAsync("Admin"); var dto = SinhVienMoi(); await admin.PostAsJsonAsync("/api/SinhVien", dto);` | Có client Admin và **đã tồn tại** một sinh viên với email đó |
| **Act** (hành động) | `var res = await admin.PostAsJsonAsync("/api/SinhVien", dto);` | Thêm lại **cùng email** |
| **Assert** (kiểm tra) | `Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);` + đọc `ErrorResponse`, kiểm `StatusCode == 409` và `Message` chứa `"Email"` | Kiểm cả **mã HTTP** lẫn **định dạng JSON lỗi** – tức là kiểm luôn cả `ExceptionMiddleware` của Tuần 3-4 |

**Cách chọn kịch bản:** mỗi kịch bản nên chứng minh một **tầng** mà unit test không chạm tới (xem bảng C4): Middleware, JWT, `[Authorize]`, validation DTO, Query Filter trên SQL thật, RowVersion trên SQL thật.

**Dấu hiệu làm đúng:** Test Explorer hiện 6 test xanh trong `SinhVienApiTests`.

## C. Các hàm liên quan

### C1. `Program.cs`

| Code | Vai trò | Vì sao |
|---|---|---|
| `public partial class Program { }` | Biến lớp `Program` thành `public` | `Program.cs` viết kiểu top-level statements nên lớp Program tự sinh là `internal`; project test cần thấy nó để viết `WebApplicationFactory<Program>` |

### C2. `ApiFactory.cs` – "nhà máy" dựng API + DB cho test

```csharp
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _db =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());

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

| Hàm / thành phần | Làm gì | Khi nào chạy |
|---|---|---|
| `WebApplicationFactory<Program>` (lớp cha) | Chạy **toàn bộ** `Program.cs` (middleware, JWT, DI, Controller) **trong bộ nhớ**, không cần mở cổng thật | Khi lần đầu dùng `Services` hoặc `CreateClient()` |
| `IAsyncLifetime` | Hợp đồng để xUnit gọi `InitializeAsync()` trước và `DisposeAsync()` sau | Đầu / cuối vòng đời fixture |
| `new MsSqlBuilder("...2022-latest").Build()` | **Khai báo** container SQL Server 2022 (chưa bật) | Khi tạo `ApiFactory` |
| `ConfigureWebHost(builder)` | Chỗ để **sửa cấu hình** API trước khi nó khởi động | Khi API được dựng |
| `builder.UseSetting(key, value)` | **Ghi đè** `ConnectionStrings:DefaultConnection` bằng chuỗi kết nối của container | Trong `ConfigureWebHost` |
| `_db.GetConnectionString()` | Lấy chuỗi kết nối container (cổng ngẫu nhiên, mật khẩu tự sinh) | Trong `ConfigureWebHost` |
| `_db.StartAsync()` | Kéo image (lần đầu) + bật container, đợi SQL sẵn sàng | Đầu `InitializeAsync` |
| `Services.CreateScope()` | Tạo một "phạm vi" DI để lấy `AppDbContext` (vì DbContext là Scoped) | Trong `InitializeAsync` |
| `GetRequiredService<AppDbContext>()` | Lấy DbContext mà API đang dùng | Trong `InitializeAsync` |
| `Database.GetConnectionString()` + `Contains("QLSINHVIEN")` | **Chốt an toàn**: nếu lỡ trỏ DB thật thì dừng ngay | Trước khi tạo bảng |
| `Database.EnsureCreatedAsync()` | Tạo toàn bộ bảng theo Model C# (có `IsDeleted`, `RowVersion`…) | Sau khi container chạy |
| `DisposeAsync()` → `_db.DisposeAsync()` | Dừng và **xóa container** | Khi chạy xong cả lớp test |

### C3. `SinhVienApiTest.cs` – các hàm hỗ trợ

| Hàm | Làm gì |
|---|---|
| `SinhVienApiTests(ApiFactory factory)` (constructor) | xUnit **tiêm** `ApiFactory` dùng chung nhờ `IClassFixture<ApiFactory>` → cả lớp chỉ bật **1 container** |
| `TaoClientAsync(string role)` | Tạo `HttpClient` đã đăng nhập: `CreateClient()` → `POST /api/XacThuc/dangky` → mở scope sửa `Role` trong DB → `POST /api/XacThuc/dangnhap` → đọc `token` → gắn `Authorization: Bearer` |
| `SinhVienMoi()` | Tạo `SinhVienDto` với **email ngẫu nhiên** (`Guid`) → các test dùng chung DB không đụng nhau |
| `_factory.CreateClient()` | Tạo `HttpClient` gọi thẳng vào API trong bộ nhớ |
| `PostAsJsonAsync / PutAsJsonAsync / GetFromJsonAsync / DeleteAsync` | Gửi HTTP, tự chuyển object ↔ JSON |
| `ReadFromJsonAsync<T>()` | Đọc body JSON thành `SinhVienDto` / `ErrorResponse` |
| `EnsureSuccessStatusCode()` | Ném lỗi nếu mã HTTP không phải 2xx (dùng trong bước chuẩn bị) |
| `IgnoreQueryFilters()` | Nhìn thẳng DB, thấy cả dòng đã xóa mềm |

### C4. 6 kịch bản test (yêu cầu tối thiểu 2)

| # | Tên test | Tầng được kiểm chứng | Kỳ vọng |
|---|---|---|---|
| 1 | `TaoSinhVien_RoiDocLai_DungDuLieu` | JWT → Controller → Service → SQL | POST **201**, GET lại đúng email + họ tên |
| 2 | `TaoSinhVien_EmailTrung_Tra409` | Service ném `ConflictException` → **Middleware** | **409** + `ErrorResponse { StatusCode = 409, Message chứa "Email" }` |
| 3 | `XoaSinhVien_LaXoaMem` | **Global Query Filter** trên SQL thật | DELETE **204** → GET **404** → DB vẫn có dòng `IsDeleted = true` |
| 4 | `PhanQuyen_401Va403` | **JWT + `[Authorize(Roles)]`** | Không token **401**; Giảng viên thêm SV **403** |
| 5 | `TaoSinhVien_TuoiKhongHopLe_Tra400` | **Validation DTO** + `InvalidModelStateResponseFactory` | **400** + "Tuổi phải là số dương từ 18 đến 99!" |
| 6 | `HaiNguoiCungSua_NguoiSauBi409` | **RowVersion** trên SQL Server thật | A **204**, B **409**, DB giữ dữ liệu của A |

Mỗi test viết theo **Arrange – Act – Assert**.

## D. Hướng dẫn kiểm tra

**Cách 1 – Visual Studio (không cần gõ lệnh)**
1. Mở **Docker Desktop**, chờ biểu tượng chuyển xanh "Engine running".
2. Visual Studio → menu **Test → Test Explorer**.
3. Mở rộng `QuanLySinhVien.Tests → IntegrationTests → SinhVienApiTests` → chuột phải → **Run**.
4. ✅ Kỳ vọng: **6 test có dấu tích xanh**. Lần đầu có thể mất 1–2 phút để tải image SQL Server.

**Cách 2 – Dòng lệnh** (tại `D:\TT\QuanLySinhVien`)
```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```
✅ Kỳ vọng: `Passed! - Failed: 0, Passed: 6`.

**Chứng minh test thật sự dùng Docker**
- Trong lúc test chạy, mở Docker Desktop → tab **Containers** → thấy container image `mcr.microsoft.com/mssql/server:2022-latest` (kèm một container nhỏ `testcontainers/ryuk` dùng để dọn dẹp). Test xong → chúng biến mất.
- Tắt Docker rồi chạy lại → integration test **đỏ** (không bật được container), còn unit test vẫn **xanh**.

**Chứng minh test có giá trị (bắt được lỗi)**
- Tạm đổi trong `SinhVienService.DeleteAsync`: `sv.IsDeleted = true;` → `_context.SinhVien.Remove(sv);` → chạy lại → `XoaSinhVien_LaXoaMem` **đỏ**. Sửa lại → xanh.

**Chứng minh không đụng DB thật**
- Sau khi chạy test, mở SSMS → `SELECT COUNT(*) FROM SinhVien` trên `QLSINHVIEN` → số dòng **không đổi**, không có sinh viên "Nguyễn Văn Test".

## E. Đoạn trình bày

> "Yêu cầu đầu tiên của Tuần 7 là Integration Test. Unit test ở tuần trước chỉ kiểm tra riêng tầng Service trên một database giả trong RAM, nên chưa chứng minh được các tầng ghép lại có chạy đúng không. Em dùng **WebApplicationFactory** để chạy **toàn bộ API ngay trong test** – đủ middleware, JWT, phân quyền, Controller và Service – và dùng **TestContainers** để tự bật một **SQL Server 2022 thật trong Docker**, dùng xong tự xóa. Trong `ApiFactory`, em ghi đè chuỗi kết nối bằng `UseSetting` sang database trong container, thêm một chốt an toàn: nếu chuỗi kết nối vẫn trỏ vào `QLSINHVIEN` thì dừng ngay, rồi `EnsureCreated` để tạo bảng. Mỗi test đi đúng đường của người dùng thật: đăng ký, đăng nhập lấy token, gọi API qua HTTP xuống tận SQL Server. Yêu cầu tối thiểu là 2 kịch bản, em viết **6 kịch bản**: thêm rồi đọc lại, email trùng trả 409, xóa mềm, phân quyền 401 và 403, dữ liệu sai trả 400, và hai người cùng sửa thì người sau bị 409. *(Demo: Run → Docker Desktop thấy container bật lên rồi tự xóa → 6 test xanh.)*"

---

# YÊU CẦU 2 – CODE COVERAGE ≥ 80% CHO TẦNG SERVICE

## A. Bản chất

**Code Coverage** = tỉ lệ phần trăm code **đã được thực thi** khi chạy test.

**Cơ chế bên trong:** Coverlet **chèn bộ đếm** vào từng dòng và từng nhánh trong file DLL của dự án (gọi là *instrumentation*). Khi test chạy, dòng nào được thực thi thì bộ đếm tăng. Chạy xong, Coverlet ghi kết quả ra file XML; ReportGenerator đọc file đó và vẽ báo cáo HTML.

| Loại | Đo gì | Ví dụ |
|---|---|---|
| **Line coverage** | Bao nhiêu **dòng** đã chạy | 81/81 dòng = 100% |
| **Branch coverage** | Bao nhiêu **nhánh rẽ** (`if`, `switch`, `??`, `&&`) đã đi qua cả hai phía | `if (a)` phải có test cho cả `a` đúng **và** sai |

> Ví von: đội kiểm tra đi qua các phòng của tòa nhà. Coverage cho biết **phòng nào chưa ai vào**, nhưng không chứng minh phòng đã vào là an toàn.

**Bản chất ngắn gọn:** *"Coverage là bản đồ chỉ ra chỗ nào chưa được test; nó đo **độ rộng** của test, không đo **độ đúng**."*

**Vì sao chỉ đo tầng Service?** Service là nơi chứa **nghiệp vụ**. Controller giờ chỉ 1–2 dòng gọi Service (đã được integration test chạy qua); đo cả `Program.cs`, Model, DTO chỉ làm loãng con số.

## B. Cách làm (4 bước + 1 bước bổ sung test)

### Bước 1 – Có công cụ thu thập: Coverlet

**Làm gì:** kiểm tra file `QuanLySinhVien.Tests.csproj` có dòng:
```xml
<PackageReference Include="coverlet.collector" Version="6.0.4" />
```
(Template xUnit tự thêm sẵn, nên dự án không phải cài gì.)

**Vì sao:** `dotnet test` mặc định **chỉ chạy test**, không đo gì. Coverlet là "người đếm": nó gắn vào quá trình chạy test, chèn bộ đếm vào DLL `QuanLySinhVien` để biết dòng nào được chạy.

**Nếu thiếu:** lệnh ở Bước 3 vẫn chạy test nhưng **không sinh** file `coverage.cobertura.xml`.

---

### Bước 2 – Cài công cụ báo cáo ReportGenerator (làm một lần trên máy)

**Làm gì:**
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```
`-g` = cài **toàn cục** (global), dùng được ở mọi thư mục.

**Vì sao:** Coverlet chỉ cho ra file XML hàng nghìn dòng số liệu, người đọc không nổi. ReportGenerator đọc XML đó và vẽ **trang HTML** có phần trăm, có tô màu từng dòng code.

**Dấu hiệu làm đúng:** gõ `reportgenerator` trong CMD thấy hướng dẫn sử dụng (không báo "not recognized"). Nếu đã cài rồi, lệnh cài lại sẽ báo *"already installed"* – không sao.

---

### Bước 3 – Chạy test kèm đo độ phủ

**Làm gì:** mở **Docker Desktop**, rồi tại `D:\TT\QuanLySinhVien` chạy:
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

| Phần lệnh | Giải thích |
|---|---|
| `dotnet test` | Build và chạy **tất cả** test (cả unit test lẫn integration test) |
| `--collect:"XPlat Code Coverage"` | Bật Coverlet. "XPlat" = cross-platform, chạy được trên Windows/Linux/Mac |
| `--results-directory .\TestResults` | Gom kết quả vào thư mục `TestResults` ngay trong dự án, dễ tìm |

**Vì sao phải bật Docker:** integration test cũng được tính vào độ phủ. `WebApplicationFactory` chạy API **trong cùng tiến trình** với test, nên khi integration test gọi API, các dòng trong `SinhVienService` chạy và được Coverlet đếm. Đặc biệt khối `catch (DbUpdateConcurrencyException)` **chỉ** được chạy qua bởi integration test trên SQL Server thật.

**Kết quả:** trong `TestResults` xuất hiện một thư mục tên Guid, bên trong có `coverage.cobertura.xml`.

---

### Bước 4 – Xuất báo cáo HTML, chỉ lọc tầng Service

**Làm gì:**
```bash
reportgenerator -reports:".\TestResults\**\coverage.cobertura.xml" -targetdir:".\CoverageReport" -reporttypes:Html -classfilters:"+QuanLySinhVien.Services.*"
```

| Tham số | Giải thích |
|---|---|
| `-reports:".\TestResults\**\coverage.cobertura.xml"` | `**` = tìm trong **mọi thư mục con**, vì mỗi lần chạy test Coverlet tạo một thư mục Guid mới |
| `-targetdir:".\CoverageReport"` | Thư mục chứa báo cáo HTML |
| `-reporttypes:Html` | Kiểu báo cáo: trang web |
| `-classfilters:"+QuanLySinhVien.Services.*"` | Dấu `+` nghĩa là **chỉ giữ** các lớp có namespace bắt đầu bằng `QuanLySinhVien.Services` |

**Vì sao lọc chỉ Service:** yêu cầu mentor tính cho **tầng Service**. Nếu không lọc, báo cáo gồm cả `Program.cs`, Controller, Middleware, Model, DTO; con số sẽ không phản ánh đúng chất lượng test của phần nghiệp vụ.

**Vì sao nên xóa `TestResults` cũ trước khi chạy:** do `**` gom **tất cả** file XML tìm thấy, các lần chạy cũ (với code cũ) sẽ bị trộn vào, làm số liệu sai lệch.

**Dấu hiệu làm đúng:** mở `CoverageReport\index.html` thấy đúng **1 lớp** `QuanLySinhVien.Services.SinhVienService` cùng phần trăm Line / Branch.

---

### Bước 5 – Đọc báo cáo và bổ sung test cho chỗ còn đỏ

**Làm gì:**
1. Trong `index.html`, bấm vào `SinhVienService` để xem code đã tô màu.
2. Tìm dòng **đỏ** (chưa chạy) hoặc **vàng** (nhánh mới chạy một phía, ví dụ `if` chỉ có test cho trường hợp đúng).
3. Tự hỏi: *"Cần đầu vào gì để code đi vào dòng/nhánh này?"* → viết test với đầu vào đó.
4. Chạy lại Bước 3 và 4, xem dòng đã chuyển xanh chưa.

**Ví dụ thực tế trong dự án:**
| Chỗ từng chưa phủ | Đầu vào cần có | Test được thêm |
|---|---|---|
| 9 nhánh `switch` sắp xếp | Mỗi cặp (SortBy, IsDescending) khác nhau | `[Theory]` với 9 `[InlineData]` |
| `if (PageNumber < 1)`, `if (PageSize ...)` | PageNumber = 0, PageSize = 100 | `GetAll_TimKiem_VaChanPhanTrangSai` |
| `if (dto.RowVersion is null)` | DTO không có RowVersion | `Update_ThieuRowVersion_NemBadRequest` |

**Vì sao:** đây mới là **mục đích thật** của coverage: không phải để có một con số đẹp, mà để **tìm chỗ chưa được kiểm tra**.

**Lưu ý:** viết test chỉ để "chạy qua" dòng mà **không có Assert** thì coverage vẫn tăng nhưng không chứng minh được gì. Mỗi test bổ sung phải kiểm tra kết quả.

## C. Các thành phần / tham số liên quan

| Thành phần | Vai trò |
|---|---|
| `coverlet.collector` | Gói NuGet thu thập độ phủ khi `dotnet test` chạy |
| `--collect:"XPlat Code Coverage"` | Bật Coverlet (XPlat = chạy được trên mọi hệ điều hành) |
| `--results-directory .\TestResults` | Nơi lưu file `coverage.cobertura.xml` |
| `coverage.cobertura.xml` | Kết quả thô: từng dòng chạy bao nhiêu lần |
| `reportgenerator` | Chuyển XML → HTML có tô màu |
| `-reports:"...\**\..."` | `**` = tìm trong mọi thư mục con (mỗi lần chạy test tạo một thư mục Guid) |
| `-classfilters:"+QuanLySinhVien.Services.*"` | Dấu `+` = **chỉ giữ** các lớp trong namespace `Services` |
| `CoverageReport\index.html` | Trang tổng hợp; bấm vào `SinhVienService` để xem từng dòng |

**Test nào phủ phần nào của `SinhVienService`:**

| Hàm trong Service | Nhánh / dòng | Test phủ |
|---|---|---|
| `GetAllAsync` | 9 nhánh `switch` sắp xếp | `GetAll_SapXep_DungThuTu` (×9 `InlineData`) |
| `GetAllAsync` | `PageNumber < 1`, `PageSize` ngoài 1–50, có `Keyword` | `GetAll_TimKiem_VaChanPhanTrangSai` |
| `GetByIdAsync` | tìm thấy / không thấy | `GetById_CoTonTai_TraVeDto`, `GetById_KhongTonTai_NemNotFound` |
| `CreateAsync` | hợp lệ / email trùng | `Create_HopLe_LuuVaoDb`, `Create_EmailTrung_...` |
| `UpdateAsync` | Id lệch, thiếu RowVersion, không thấy, email trùng, hợp lệ | 5 test `Update_...` |
| `UpdateAsync` | `catch (DbUpdateConcurrencyException)` | Integration test `HaiNguoiCungSua_NguoiSauBi409` (cần SQL thật) |
| `DeleteAsync` | hợp lệ / không thấy | `Delete_LaXoaMem`, `Delete_KhongTonTai_NemNotFound` |
| `TimHoacBaoLoiAsync`, `ToDto` | dùng chung | Được phủ gián tiếp qua các test trên |

## D. Hướng dẫn kiểm tra

1. Mở **Docker Desktop** (để integration test cũng chạy và phủ khối `catch` concurrency).
2. Mở **Command Prompt** tại `D:\TT\QuanLySinhVien`, **xóa kết quả cũ** để không bị lẫn số liệu lần trước:
   ```bash
   rmdir /s /q TestResults
   rmdir /s /q CoverageReport
   ```
3. Chạy 2 lệnh ở bước B3 và B4.
4. Mở `CoverageReport\index.html` bằng trình duyệt.
5. ✅ Kỳ vọng: dòng `QuanLySinhVien.Services.SinhVienService` có **Line coverage ≥ 80%**.

> 📌 Báo cáo hiện có trên máy bạn (tạo lúc **02:23 sáng 03/10**) ghi: **Line 100% (81/81 dòng)**, **Branch 96.8% (31/32 nhánh)**. Nhưng báo cáo đó được tạo **trước** lần sửa cuối của `SinhVienService.cs` (phần RowVersion). Hãy **chạy lại** theo các bước trên để lấy con số mới nhất trước khi trình bày.

6. Bấm vào `SinhVienService` → xem code tô màu: **xanh** đã chạy, **đỏ** chưa chạy, **vàng** nhánh mới chạy một phía.
7. **Demo chứng minh coverage phản ánh đúng test:** tạm comment test `GetAll_SapXep_DungThuTu` → đo lại → % giảm, các dòng `switch` chuyển đỏ → bỏ comment → đo lại → trở về như cũ.

**Mẹo:** nếu chạy khi **tắt Docker**, integration test lỗi và khối `catch (DbUpdateConcurrencyException)` sẽ hiện đỏ. Đó là lý do bước 1 bật Docker.

## E. Đoạn trình bày

> "Yêu cầu thứ hai là quản lý chất lượng code bằng **Code Coverage**, đạt tối thiểu 80% cho tầng Service. Coverage là tỉ lệ code đã được thực thi khi chạy test. Em dùng **Coverlet** – gói có sẵn trong project test – để chèn bộ đếm vào từng dòng khi test chạy, rồi dùng **ReportGenerator** chuyển kết quả thành trang HTML, với bộ lọc `classfilters` chỉ giữ namespace `Services`, vì đó là nơi chứa nghiệp vụ. Kết quả `SinhVienService` đạt **[con số line]%** số dòng và **[con số branch]%** số nhánh. Báo cáo tô đỏ những dòng chưa được test để em bổ sung; ví dụ để phủ hết 9 nhánh sắp xếp, em dùng `[Theory]` với 9 bộ dữ liệu. Riêng khối bắt lỗi đụng độ dữ liệu thì database giả không tái hiện được, nên nó được phủ bởi integration test chạy trên SQL Server thật. Tuy nhiên em hiểu coverage cao **không có nghĩa là không có lỗi** – nó chỉ cho biết dòng nào đã chạy; chất lượng thật nằm ở các câu **Assert**. *(Demo: mở `index.html`, chỉ con số và các dòng xanh.)*"

---

# YÊU CẦU 3 – OPTIMISTIC CONCURRENCY VỚI ROWVERSION

## A. Bản chất

**Vấn đề – "Lost Update":**

| Thời điểm | Admin A | Admin B | DB |
|---|---|---|---|
| 9:00 | Mở form SV #5 | Mở form SV #5 | Tên "An", Tuổi 20 |
| 9:01 | Đổi tên → "An Mới" → Lưu ✓ | | "An Mới", 20 |
| 9:02 | | Đổi tuổi → 21 → Lưu ✓ (form vẫn chứa tên "An") | **"An", 21** ❌ |

Thay đổi của A **biến mất** mà không ai biết.

**Hai cách giải:**
- **Pessimistic (bi quan)**: khóa dòng khi A mở form → B phải đợi. Không hợp với web vì người dùng có thể mở form rất lâu.
- **Optimistic (lạc quan)** ✅: không khóa, **chỉ kiểm tra lúc lưu** xem dữ liệu có bị đổi kể từ lúc mở form không.

**RowVersion là gì?** Cột kiểu `rowversion` của SQL Server, 8 byte, **SQL Server tự tăng mỗi lần dòng bị UPDATE**. Giống **số phiên bản của tài liệu**.

**Cơ chế cốt lõi – "so sánh rồi ghi" trong MỘT câu SQL:**

```sql
UPDATE SinhVien SET HoTen = ..., Email = ..., Tuoi = ...
WHERE Id = 5 AND RowVersion = <phiên bản lúc mở form>;
```

- Phiên bản **còn khớp** → cập nhật **1 dòng** → thành công, SQL tự tăng RowVersion.
- Đã có người lưu trước → phiên bản **không khớp** → cập nhật **0 dòng** → EF hiểu là đụng độ.

Việc kiểm tra và ghi nằm trong **cùng một câu lệnh** nên **không có khe hở** giữa "đọc" và "ghi" (tính nguyên tử – atomic).

**Bản chất ngắn gọn:** *"Mỗi lần lưu phải trình đúng 'số phiên bản' mình đã thấy; sai số phiên bản tức là có người sửa trước, thì từ chối."*

## B. Cách làm (6 bước)

### Bước 1 – Thêm cột RowVersion vào bảng SinhVien

**Làm gì:** chạy trong SSMS trên DB `QLSINHVIEN`:
```sql
ALTER TABLE SinhVien ADD RowVersion ROWVERSION;
```

**Vì sao dùng kiểu `rowversion`:**
- SQL Server **tự gán** giá trị khi thêm dòng và **tự tăng** mỗi khi dòng bị UPDATE. Ứng dụng không cần (và không được) tự gán.
- Giá trị là **duy nhất trong cả database** và chỉ tăng, nên không bao giờ "quay về" giá trị cũ.
- Các dòng **đã có sẵn** trong bảng cũng được gán giá trị ngay khi thêm cột.

**Vì sao không dùng cột ngày giờ `UpdatedAt`:** ngày giờ có thể trùng (hai lần sửa trong cùng một tích tắc), và phải nhớ tự cập nhật trong code. `rowversion` do SQL Server lo hoàn toàn.

**Dấu hiệu làm đúng:** `SELECT Id, RowVersion FROM SinhVien` thấy mỗi dòng có giá trị dạng `0x00000000000007D1`.

---

### Bước 2 – Khai báo trong Model `SinhVien`

**Làm gì:** `Models/SinhVien.cs`:
```csharp
[Timestamp]                              // ← báo EF: đây là cột concurrency
public byte[]? RowVersion { get; set; }
```

**Vì sao:**
| Thành phần | Tác dụng |
|---|---|
| `byte[]` | `rowversion` là 8 byte nhị phân, C# biểu diễn bằng mảng byte |
| `?` (cho phép null) | Lúc tạo đối tượng mới trong C#, DB chưa sinh giá trị |
| `[Timestamp]` | Báo EF hai điều: (1) cột do DB sinh → **không đưa vào** câu INSERT/UPDATE; (2) đây là **concurrency token** → **đưa vào `WHERE`** của câu UPDATE/DELETE |

**Nếu thiếu `[Timestamp]`:** EF coi đây là cột bình thường, câu UPDATE chỉ có `WHERE Id = ...` → không bao giờ phát hiện đụng độ, và còn cố ghi vào cột rowversion gây lỗi SQL.

---

### Bước 3 – Đưa RowVersion ra ngoài qua DTO

**Làm gì:**
- `DTOs/SinhVienDto.cs` thêm `public byte[]? RowVersion { get; set; }`.
- Trong `SinhVienService`: phần `Select(...)` của `GetAllAsync` và hàm `ToDto` đều thêm `RowVersion = s.RowVersion`.

**Vì sao:** client phải **biết** phiên bản lúc nó lấy dữ liệu thì mới gửi lại được khi lưu. Không có phiên bản trong danh sách → Angular không có gì để giữ.

**Điều xảy ra khi chuyển sang JSON:** `byte[]` được tự động mã hóa thành chuỗi **Base64**, ví dụ `"rowVersion": "AAAAAAAAB9E="`. Khi Angular gửi chuỗi đó trở lại, ASP.NET tự giải mã về `byte[]`. Vì vậy phía Angular khai báo `rowVersion?: string` và **không cần xử lý gì** – chỉ cần giữ nguyên và gửi lại.

**Dấu hiệu làm đúng:** F12 → Network → `GET /api/SinhVien` → mỗi sinh viên có trường `rowVersion`.

---

### Bước 4 – Sửa `UpdateAsync` trong Service

**Làm gì:** thêm 3 phần vào `UpdateAsync` (code đầy đủ ở mục C1):

**4.1. Bắt buộc có RowVersion**
```csharp
if (dto.RowVersion is null)
    throw new BadRequestException("Thiếu RowVersion! Vui lòng tải lại dữ liệu trước khi sửa.");
```
*Vì sao:* nếu cho phép thiếu, client cũ hoặc gọi bằng Postman sẽ lưu mà không kiểm tra phiên bản → quay lại tình trạng ghi đè mù. Kiểm tra sớm, trước khi truy vấn DB, vì đây là lỗi rẻ nhất.

**4.2. Gán phiên bản của client làm "giá trị gốc"** – *dòng quan trọng nhất*
```csharp
_context.Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;
```
*Vì sao:* EF theo dõi mỗi thuộc tính bằng hai giá trị:
- **OriginalValue**: giá trị lúc đọc từ DB → EF dùng để viết `WHERE RowVersion = @original`.
- **CurrentValue**: giá trị hiện tại trong code.

Ngay trước dòng này, `TimHoacBaoLoiAsync` vừa đọc `sv` từ DB, nên OriginalValue đang là **phiên bản mới nhất**. Nếu để nguyên, `WHERE` sẽ so phiên bản mới nhất với chính nó → **luôn khớp** → không bao giờ phát hiện người khác đã sửa.
Gán bằng `dto.RowVersion` (phiên bản client cầm **lúc mở form**) thì `WHERE` mới hỏi đúng câu: *"Dòng này còn đúng là phiên bản tôi đã thấy không?"*

> Ví dụ: client mở form lúc phiên bản 7. Người khác lưu → DB lên 8. Service đọc `sv` → OriginalValue = 8. Gán lại = 7 → câu SQL `WHERE RowVersion = 7` → không khớp → phát hiện đụng độ.

**4.3. Bắt lỗi đụng độ và đổi thành 409**
```csharp
try { await _context.SaveChangesAsync(); }
catch (DbUpdateConcurrencyException)
{
    throw new ConflictException("Dữ liệu sinh viên đã bị người khác thay đổi. Vui lòng tải lại trang rồi sửa lại!");
}
```
*Vì sao:* khi câu UPDATE ảnh hưởng **0 dòng**, EF ném `DbUpdateConcurrencyException` – một lỗi kỹ thuật. Nếu để nguyên, Middleware coi là lỗi không lường trước → trả **500**. Đổi sang `ConflictException` để trả **409** cùng câu thông báo dễ hiểu cho người dùng.

**Thứ tự kiểm tra trong `UpdateAsync` và lý do:**
| # | Kiểm tra | Mã | Vì sao ở vị trí này |
|---|---|---|---|
| 1 | Id URL ≠ Id body | 400 | Không cần DB, rẻ nhất |
| 2 | Thiếu RowVersion | 400 | Không cần DB |
| 3 | Không tìm thấy | 404 | Cần 1 truy vấn |
| 4 | Email trùng người khác | 409 | Cần thêm 1 truy vấn |
| 5 | Đụng độ phiên bản | 409 | Chỉ biết được khi thật sự ghi |

---

### Bước 5 – Angular: giữ RowVersion và xử lý 409

**5.1. Model:** `models/sinh-vien.ts` thêm `rowVersion?: string;`

**5.2. Khi mở form sửa** – hàm `sua(sv)` sao chép thêm `rowVersion: sv.rowVersion`.
*Vì sao:* form sửa là **một bản sao** của dòng trong bảng. Thiếu trường này thì bản sao không mang phiên bản → bấm Lưu nhận 400 "Thiếu RowVersion" (đây là lỗi em từng gặp).
*Vì sao phải giữ đúng phiên bản **lúc mở form**:* đó là phiên bản của dữ liệu mà người dùng **đang nhìn thấy và chỉnh sửa**.

**5.3. Khi lưu bị lỗi** – trong `luu()`, nhánh `error`:
```ts
const laDungDo = error.status === 409 && error.error?.message?.includes('người khác thay đổi');
if (laDungDo) {
  this.lamMoiForm();
  this.taiDanhSach();
}
```
*Vì sao phân biệt hai loại 409:*
| Loại 409 | Dữ liệu trong form còn dùng được? | Xử lý |
|---|---|---|
| Email trùng | Còn – chỉ cần đổi email | **Giữ form** để người dùng sửa tiếp |
| Đụng độ dữ liệu | Không – đã lỗi thời so với DB | **Đóng form** + **tải lại danh sách** để lấy dữ liệu và RowVersion mới |

*Vì sao không cần tự hiện toast:* `errorInterceptor` (Tuần 3-4) đã tự hiện câu `message` của mọi lỗi.

---

### Bước 6 – Audit và Test

**6.1. Bỏ RowVersion khỏi Audit Log:** trong `AuditSaveChangesInterceptor`, `SensitiveProperties = { "PasswordHash", "RowVersion" }`.
*Vì sao:* RowVersion đổi **sau mọi lần sửa**, là dữ liệu kỹ thuật. Ghi vào lịch sử chỉ làm người đọc rối.

**6.2. Unit test:**
- `TaoDb()` gán sẵn `RowVersion = new byte[] { 1 }` cho dữ liệu mẫu. *Vì sao:* DB InMemory **không tự sinh** rowversion như SQL Server.
- Các test `Update_...` gửi kèm `RowVersion = new byte[] { 1 }` để vượt qua bước kiểm tra 4.1.
- Thêm `Update_ThieuRowVersion_NemBadRequest` để kiểm tra nhánh thiếu RowVersion.

**6.3. Integration test `HaiNguoiCungSua_NguoiSauBi409`** (chạy trên SQL Server thật):
| Bước | Code | Kiểm tra |
|---|---|---|
| Tạo sinh viên | `PostAsJsonAsync` | – |
| A và B cùng mở form | 2 lần `GetFromJsonAsync` | `formA.RowVersion == formB.RowVersion` |
| A lưu trước | `PutAsJsonAsync(formA)` | **204** |
| B lưu sau với phiên bản cũ | `PutAsJsonAsync(formB)` | **409** |
| Xem kết quả cuối | `GetFromJsonAsync` | Tên là "A đã sửa"; RowVersion đã khác lúc đầu |

*Vì sao phải là integration test:* chỉ SQL Server thật mới **tự tăng** rowversion sau lần lưu của A. Trên InMemory, B vẫn thấy phiên bản khớp và lưu thành công → không tái hiện được đụng độ.

## C. Các hàm liên quan

### C1. Backend

| Vị trí | Code | Làm gì |
|---|---|---|
| SQL | `ALTER TABLE SinhVien ADD RowVersion ROWVERSION;` | SQL Server tự quản lý giá trị, tự tăng mỗi lần UPDATE |
| `Models/SinhVien.cs` | `[Timestamp] public byte[]? RowVersion` | Báo EF: (1) cột do DB sinh, không ghi vào; (2) **đưa vào `WHERE`** khi UPDATE/DELETE (*concurrency token*) |
| `DTOs/SinhVienDto.cs` | `public byte[]? RowVersion` | Mang phiên bản ra client; JSON tự mã hóa `byte[]` thành chuỗi **Base64** |
| `SinhVienService.GetAllAsync` / `ToDto` | `RowVersion = s.RowVersion` | Gửi phiên bản hiện tại kèm mỗi sinh viên |
| `SinhVienService.UpdateAsync` | xem bảng dưới | Trái tim của yêu cầu |
| `Middleware/ExceptionMiddleware` | `ConflictException` → 409 | Trả JSON `{statusCode: 409, message: ...}` |
| `Data/AuditSaveChangesInterceptor` | `SensitiveProperties = { "PasswordHash", "RowVersion" }` | Không ghi RowVersion vào lịch sử (dữ liệu kỹ thuật, gây nhiễu) |

**`UpdateAsync` từng bước:**

```csharp
public async Task UpdateAsync(int id, SinhVienDto dto)
{
    if (id != dto.Id) throw new BadRequestException("Id trong URL không khớp...");        // ① 400
    if (dto.RowVersion is null) throw new BadRequestException("Thiếu RowVersion!...");   // ② 400

    var sv = await TimHoacBaoLoiAsync(id);                                                // ③ 404 nếu không có

    if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower() && s.Id != id))
        throw new ConflictException("Email này đã được sử dụng bởi sinh viên khác!");    // ④ 409 email

    _context.Entry(sv).Property(s => s.RowVersion).OriginalValue = dto.RowVersion;       // ⑤ ★

    sv.HoTen = dto.HoTen; sv.Email = dto.Email; sv.Tuoi = dto.Tuoi;                       // ⑥

    try { await _context.SaveChangesAsync(); }                                            // ⑦
    catch (DbUpdateConcurrencyException)
    {
        throw new ConflictException("Dữ liệu sinh viên đã bị người khác thay đổi...");   // ⑧ 409 đụng độ
    }
}
```

| Bước | Hàm / API | Giải thích |
|---|---|---|
| ② | `dto.RowVersion is null` | Không có phiên bản thì không biết so với gì → từ chối, tránh "lưu mù" |
| ③ | `TimHoacBaoLoiAsync` → `FindAsync` | Đọc sinh viên; lúc này `sv.RowVersion` là phiên bản **mới nhất trong DB** |
| ⑤ | `_context.Entry(sv)` | Lấy "hồ sơ theo dõi" (EntityEntry) của `sv` trong ChangeTracker |
| ⑤ | `.Property(s => s.RowVersion)` | Chọn thuộc tính RowVersion |
| ⑤ | `.OriginalValue = dto.RowVersion` | **Dòng quan trọng nhất.** EF dùng `OriginalValue` để viết `WHERE RowVersion = ...`. Nếu không gán, EF so với phiên bản vừa đọc ở ③ → **luôn khớp** → không bao giờ phát hiện đụng độ |
| ⑦ | `SaveChangesAsync()` | EF sinh `UPDATE ... WHERE Id = @id AND RowVersion = @original` rồi kiểm tra số dòng bị ảnh hưởng |
| ⑧ | `DbUpdateConcurrencyException` | EF ném khi số dòng bị ảnh hưởng = **0** |
| ⑧ | `ConflictException` | Đổi lỗi kỹ thuật thành lỗi nghiệp vụ → Middleware trả **409** |

### C2. Angular

| Vị trí | Code | Làm gì |
|---|---|---|
| `models/sinh-vien.ts` | `rowVersion?: string` | Nhận chuỗi Base64 từ API |
| `sinh-vien.ts` → `sua(sv)` | `rowVersion: sv.rowVersion` | Khi mở form, **giữ lại phiên bản lúc mở**. Thiếu dòng này → bấm Lưu nhận 400 "Thiếu RowVersion" |
| `services/sinh-vien.ts` → `update(sinhVien)` | `PUT /api/SinhVien/{id}` | Gửi cả object, trong đó có `rowVersion` |
| `sinh-vien.ts` → `luu()` nhánh `error` | `laDungDo = status === 409 && message.includes('người khác thay đổi')` | Phân biệt 409 **đụng độ** với 409 **email trùng** |
| | `lamMoiForm()` + `taiDanhSach()` | Đụng độ → đóng form cũ (dữ liệu đã lỗi thời) và tải dữ liệu + RowVersion mới; email trùng → **giữ form** cho người dùng sửa |
| `interceptors/error.ts` | toast lỗi | Tự hiện message 409, component không phải viết thêm |

> ✏️ Nhỏ nhưng nên sửa trước khi trình bày: comment ở dòng `rowVersion: sv.rowVersion` trong `sua()` đang ghi nhầm "Giữ nguyên ảnh đại diện…". Nên đổi thành `// giữ phiên bản lúc mở form để server so sánh khi lưu`.

### C3. Test

| Test | Loại | Kiểm tra |
|---|---|---|
| `Update_ThieuRowVersion_NemBadRequest` | Unit | Thiếu RowVersion → `BadRequestException` |
| Các test `Update_...` khác | Unit | Gửi kèm `RowVersion = { 1 }` (InMemory không tự sinh nên `TaoDb` gán sẵn) |
| `HaiNguoiCungSua_NguoiSauBi409` | Integration (SQL thật) | A và B cùng RowVersion → A 204 → B 409 → DB giữ "A đã sửa", RowVersion đã đổi |

## D. Hướng dẫn kiểm tra

> Chạy backend bằng **Ctrl+F5** (không debug). Nếu chạy F5, Visual Studio sẽ dừng ở `ConflictException` – đó là debugger bắt exception, không phải lỗi; bấm **Continue**.

**Cách 1 – Hai tab trình duyệt (giống thực tế nhất)**
1. Mở **2 tab**, cùng đăng nhập **Admin**, cùng vào trang Sinh viên.
2. Cả 2 tab bấm **Sửa** cùng một sinh viên.
3. **Tab 1**: đổi họ tên → **Lưu** → ✅ "Cập nhật thành công!".
4. **Tab 2** (không tải lại): đổi tuổi → **Lưu** → ✅ toast đỏ *"Dữ liệu sinh viên đã bị người khác thay đổi. Vui lòng tải lại trang rồi sửa lại!"*, form tự đóng, danh sách hiện tên mới của Tab 1.
5. Tab 2 bấm **Sửa** lại → đổi tuổi → **Lưu** → ✅ thành công (vì đã cầm phiên bản mới).
6. F12 → **Network** → request PUT lỗi: **Status 409**, Response `{"statusCode":409,"message":"Dữ liệu sinh viên đã bị..."}`.

**Cách 2 – Một tab + SSMS (giả lập "người khác" bằng SQL)**
1. Trên web bấm **Sửa** sinh viên Id = X (chưa lưu).
2. Trong SSMS chạy:
   ```sql
   SELECT Id, HoTen, RowVersion FROM SinhVien WHERE Id = X;   -- ghi lại RowVersion
   UPDATE SinhVien SET Tuoi = Tuoi WHERE Id = X;              -- không đổi dữ liệu nhưng RowVersion vẫn tăng
   SELECT Id, HoTen, RowVersion FROM SinhVien WHERE Id = X;   -- RowVersion đã khác
   ```
3. Quay lại web bấm **Lưu** → ✅ **409**. Chứng minh: **chỉ cần dòng bị UPDATE là phiên bản đổi**, kể cả khi giá trị không đổi.

**Cách 3 – Phân biệt hai loại 409**
- Sửa sinh viên, đặt email trùng một sinh viên khác → Lưu → toast "Email này đã được sử dụng…" và **form vẫn mở** (để sửa email).
- So với Cách 1: lỗi đụng độ thì **form đóng** và danh sách tải lại.

**Cách 4 – Thiếu RowVersion (Swagger/Postman)**
- `PUT /api/SinhVien/X` với token Admin, body **không có** `rowVersion` → ✅ **400** "Thiếu RowVersion! Vui lòng tải lại dữ liệu trước khi sửa."

**Cách 5 – Test tự động**
- Test Explorer → chạy `HaiNguoiCungSua_NguoiSauBi409` và `Update_ThieuRowVersion_NemBadRequest` → ✅ xanh.

## E. Đoạn trình bày

> "Yêu cầu thứ ba là xử lý đụng độ dữ liệu. Tình huống là hai Admin cùng mở form sửa một sinh viên: A lưu trước, B lưu sau, và vì form của B vẫn chứa dữ liệu cũ nên thay đổi của A bị ghi đè mà không ai biết – gọi là **Lost Update**. Em chọn **Optimistic Concurrency**: không khóa dữ liệu, chỉ kiểm tra lúc lưu. Em thêm cột **`rowversion`** mà **SQL Server tự tăng** mỗi lần dòng bị sửa, và đánh dấu `[Timestamp]` để EF đưa cột này vào điều kiện `WHERE` của câu UPDATE. Angular giữ RowVersion lúc mở form và gửi lại khi lưu. Trong `UpdateAsync`, dòng quan trọng nhất là gán **`OriginalValue`** bằng RowVersion client gửi lên – nếu không, EF sẽ so với phiên bản vừa đọc từ DB và luôn khớp. Khi đã có người lưu trước, câu UPDATE không trúng dòng nào, EF ném `DbUpdateConcurrencyException`, em đổi thành `ConflictException` và middleware trả **409**. Việc so sánh và ghi nằm trong cùng một câu SQL nên không có khe hở. Phía Angular, nếu là 409 đụng độ thì đóng form và tải lại dữ liệu mới, còn 409 do email trùng thì giữ form để người dùng sửa. Thiếu RowVersion thì trả 400 để không bao giờ lưu mù. *(Demo: 2 tab cùng sửa → tab sau nhận thông báo, form đóng, danh sách cập nhật.)*"

---

# TỔNG KẾT TUẦN 7 (đoạn chốt ~30 giây)

> "Tóm lại, Tuần 7 em làm ba việc bổ trợ cho nhau. **Optimistic Concurrency** bảo vệ dữ liệu khi nhiều người dùng cùng lúc. **Integration Test với TestContainers** chứng minh toàn bộ hệ thống, từ HTTP xuống SQL Server thật, chạy đúng – và chính nó kiểm chứng được tính năng đụng độ mà database giả không làm được. **Code Coverage** cho em con số đo được để biết test đã đủ rộng chưa. Ba nguyên tắc em rút ra là: test trong môi trường giống thật nhất có thể, đo được thì mới quản lý được, và không khóa mà kiểm tra lúc lưu."

## Bảng ôn nhanh

| | Integration Test | Code Coverage | Optimistic Concurrency |
|---|---|---|---|
| **Bản chất** | Dựng cả hệ thống trong môi trường thật dùng một lần, gọi như client thật | Bản đồ chỗ nào chưa được test | Trình đúng "số phiên bản" mới được lưu |
| **Công cụ** | WebApplicationFactory, TestContainers, Docker | Coverlet, ReportGenerator | `rowversion`, `[Timestamp]`, EF ChangeTracker |
| **Hàm then chốt** | `ConfigureWebHost` + `UseSetting`, `InitializeAsync`, `TaoClientAsync` | `--collect:"XPlat Code Coverage"`, `-classfilters` | `Entry().Property().OriginalValue`, `catch (DbUpdateConcurrencyException)` |
| **Kết quả** | 6 test pass trên SQL Server thật | Service ≥ 80% (chạy lại để lấy số mới) | Người lưu sau nhận 409, không mất dữ liệu |
| **Câu "ăn điểm"** | Đi đúng đường thật mà không đụng DB thật | Đo độ rộng, không đo độ đúng | So sánh và ghi trong một câu SQL nguyên tử |
