using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;

namespace QuanLySinhVien.Tests.IntegrationTests;

public class SinhVienApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SinhVienApiTests(ApiFactory factory) => _factory = factory;

    // Helper: đăng ký → đổi Role trong DB → đăng nhập → gắn token Bearer
    private async Task<HttpClient> TaoClientAsync(string role)
    {
        var username = $"user_{Guid.NewGuid():N}";
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/XacThuc/dangky",
            new DangKyDto { Username = username, Password = "123456", FullName = "Test" }))
            .EnsureSuccessStatusCode();

        // DangKy mặc định Role = "GiangVien" → sửa trong DB để có Admin
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.NguoiDung.SingleAsync(u => u.Username == username);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        var res = await client.PostAsJsonAsync("/api/XacThuc/dangnhap",
            new DangNhapDto { Username = username, Password = "123456" });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static SinhVienDto SinhVienMoi() => new()
    {
        HoTen = "Nguyễn Văn Test",
        Email = $"{Guid.NewGuid():N}@test.com", // email ngẫu nhiên → các test không đụng nhau
        Tuoi = 20
    };

    // 1. Tạo mới → đọc lại đúng dữ liệu (Controller → SQL Server thật)
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
        Assert.Equal(dto.HoTen, get.HoTen);
    }

    // 2. Email trùng → 409 với JSON chuẩn {statusCode, message}
    [Fact]
    public async Task TaoSinhVien_EmailTrung_Tra409()
    {
        var admin = await TaoClientAsync("Admin");
        var dto = SinhVienMoi();
        await admin.PostAsJsonAsync("/api/SinhVien", dto);

        var res = await admin.PostAsJsonAsync("/api/SinhVien", dto);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(409, err!.StatusCode);
        Assert.Contains("Email", err.Message);
    }

    // 3. Xóa mềm → API trả 404 nhưng dòng vẫn còn trong DB
    [Fact]
    public async Task XoaSinhVien_LaXoaMem()
    {
        var admin = await TaoClientAsync("Admin");
        var created = await (await admin.PostAsJsonAsync("/api/SinhVien", SinhVienMoi()))
            .Content.ReadFromJsonAsync<SinhVienDto>();

        var del = await admin.DeleteAsync($"/api/SinhVien/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await admin.GetAsync($"/api/SinhVien/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode); // Global Query Filter ẩn đi

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == created.Id);
        Assert.True(row.IsDeleted); // vẫn còn trong DB, chỉ bị đánh dấu
    }

    // 4. Phân quyền: chưa đăng nhập → 401, Giảng viên thêm SV → 403
    [Fact]
    public async Task PhanQuyen_401Va403()
    {
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/SinhVien")).StatusCode);

        var giangVien = await TaoClientAsync("GiangVien");
        var res = await giangVien.PostAsJsonAsync("/api/SinhVien", SinhVienMoi());
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // 5. Dữ liệu sai → 400 kèm câu lỗi tiếng Việt
    [Fact]
    public async Task TaoSinhVien_TuoiKhongHopLe_Tra400()
    {
        var admin = await TaoClientAsync("Admin");
        var dto = SinhVienMoi();
        dto.Tuoi = 10;

        var res = await admin.PostAsJsonAsync("/api/SinhVien", dto);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Tuổi phải là số dương từ 18 đến 99!", err!.Message);
    }
}