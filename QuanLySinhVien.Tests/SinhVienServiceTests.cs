using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Exceptions;
using QuanLySinhVien.Models;
using QuanLySinhVien.Services;
using System.Net;

namespace QuanLySinhVien.Tests;

public class SinhVienServiceTests
{
    // Mỗi test một DB InMemory riêng (tên Guid) → các test không ảnh hưởng nhau
    // Dữ liệu mẫu: Id 1 = Binh, Id 2 = An, Id 3 = Cuong
    private static AppDbContext TaoDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SinhVien.AddRange(
            new SinhVien { HoTen = "Binh", Email = "c@x.com", Tuoi = 22, RowVersion = new byte[] { 1 } },
            new SinhVien { HoTen = "An", Email = "b@x.com", Tuoi = 20, RowVersion = new byte[] { 1 } },
            new SinhVien { HoTen = "Cuong", Email = "a@x.com", Tuoi = 21, RowVersion = new byte[] { 1 } });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    // ===== GetAll: mọi nhánh của switch sắp xếp =====
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
        var service = new SinhVienService(TaoDb());

        var result = await service.GetAllAsync(new SinhVienQuery { SortBy = sortBy, IsDescending = desc });

        Assert.Equal(idDauTien, result.Items[0].Id);
    }

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

    // ===== GetById =====
    [Fact]
    public async Task GetById_CoTonTai_TraVeDto()
    {
        var dto = await new SinhVienService(TaoDb()).GetByIdAsync(2);
        Assert.Equal("An", dto.HoTen);
    }

    [Fact]
    public async Task GetById_KhongTonTai_NemNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new SinhVienService(TaoDb()).GetByIdAsync(99));
    }

    // ===== Create =====
    [Fact]
    public async Task Create_HopLe_LuuVaoDb()
    {
        var db = TaoDb();
        var created = await new SinhVienService(db)
            .CreateAsync(new SinhVienDto { HoTen = "Moi", Email = "moi@x.com", Tuoi = 19 });

        Assert.True(created.Id > 0);
        Assert.Equal(4, await db.SinhVien.CountAsync());
    }

    [Fact]
    public async Task Create_EmailTrung_KhongPhanBietHoaThuong_NemConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => new SinhVienService(TaoDb())
            .CreateAsync(new SinhVienDto { HoTen = "X", Email = "A@X.COM", Tuoi = 19 }));
    }

    // ===== Update =====
    // ===== Update =====
    [Fact]
    public async Task Update_HopLe_CapNhatDuLieu()
    {
        var db = TaoDb();
        await new SinhVienService(db)
            .UpdateAsync(1, new SinhVienDto { Id = 1, HoTen = "Binh Moi", Email = "c@x.com", Tuoi = 30, RowVersion = new byte[] { 1 } });

        var sv = await db.SinhVien.FindAsync(1);
        Assert.Equal("Binh Moi", sv!.HoTen);
        Assert.Equal(30, sv.Tuoi);
    }

    [Fact]
    public async Task Update_IdKhongKhop_NemBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => new SinhVienService(TaoDb())
            .UpdateAsync(1, new SinhVienDto { Id = 2, HoTen = "X", Email = "x@x.com", Tuoi = 20 }));
    }

    [Fact]
    public async Task Update_ThieuRowVersion_NemBadRequest()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => new SinhVienService(TaoDb())
            .UpdateAsync(1, new SinhVienDto { Id = 1, HoTen = "X", Email = "c@x.com", Tuoi = 20 }));
    }

    [Fact]
    public async Task Update_KhongTonTai_NemNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new SinhVienService(TaoDb())
            .UpdateAsync(99, new SinhVienDto { Id = 99, HoTen = "X", Email = "x@x.com", Tuoi = 20, RowVersion = new byte[] { 1 } }));
    }

    [Fact]
    public async Task Update_EmailCuaNguoiKhac_NemConflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => new SinhVienService(TaoDb())
            .UpdateAsync(1, new SinhVienDto { Id = 1, HoTen = "Binh", Email = "b@x.com", Tuoi = 22, RowVersion = new byte[] { 1 } }));
    }

    // ===== Delete =====
    [Fact]
    public async Task Delete_LaXoaMem()
    {
        var db = TaoDb();
        await new SinhVienService(db).DeleteAsync(1);

        var sv = await db.SinhVien.IgnoreQueryFilters().SingleAsync(s => s.Id == 1);
        Assert.True(sv.IsDeleted);
        Assert.Equal(2, await db.SinhVien.CountAsync()); // filter ẩn dòng đã xóa
    }

    [Fact]
    public async Task Delete_KhongTonTai_NemNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new SinhVienService(TaoDb()).DeleteAsync(99));
    }
    
}