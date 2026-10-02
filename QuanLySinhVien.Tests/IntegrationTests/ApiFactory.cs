using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuanLySinhVien.Data;
using Testcontainers.MsSql;

namespace QuanLySinhVien.Tests.IntegrationTests;

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