using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Exceptions;
using QuanLySinhVien.Models;

namespace QuanLySinhVien.Services
{
    public class SinhVienService
    {
        private readonly AppDbContext _context;

        public SinhVienService(AppDbContext context) => _context = context;

        public async Task<PagedResult<SinhVienDto>> GetAllAsync(SinhVienQuery query)
        {
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
                ("hoten", true) => queryable.OrderByDescending(s => s.HoTen),
                ("email", false) => queryable.OrderBy(s => s.Email),
                ("email", true) => queryable.OrderByDescending(s => s.Email),
                ("tuoi", false) => queryable.OrderBy(s => s.Tuoi),
                ("tuoi", true) => queryable.OrderByDescending(s => s.Tuoi),
                (_, true) => queryable.OrderByDescending(s => s.Id),
                _ => queryable.OrderBy(s => s.Id)
            };

            var totalCount = await queryable.CountAsync();

            var items = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(s => new SinhVienDto
                {
                    Id = s.Id,
                    HoTen = s.HoTen,
                    Email = s.Email,
                    Tuoi = s.Tuoi,
                    AvatarUrl = s.AvatarUrl,
                    RowVersion = s.RowVersion
                })
                .ToListAsync();

            return new PagedResult<SinhVienDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }

        public async Task<SinhVienDto> GetByIdAsync(int id)
        {
            var sv = await TimHoacBaoLoiAsync(id);
            return ToDto(sv);
        }

        public async Task<SinhVienDto> CreateAsync(SinhVienDto dto)
        {
            if (await _context.SinhVien.AnyAsync(s => s.Email.ToLower() == dto.Email.ToLower()))
                throw new ConflictException("Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác.");

            var sv = new SinhVien { HoTen = dto.HoTen, Email = dto.Email, Tuoi = dto.Tuoi, AvatarUrl = dto.AvatarUrl };
            _context.SinhVien.Add(sv);
            await _context.SaveChangesAsync();

            return ToDto(sv);
        }

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

        public async Task DeleteAsync(int id)
        {
            var sv = await TimHoacBaoLoiAsync(id);
            sv.IsDeleted = true; // xóa mềm
            await _context.SaveChangesAsync();
        }

        private async Task<SinhVien> TimHoacBaoLoiAsync(int id) =>
            await _context.SinhVien.FindAsync(id)
            ?? throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");

        private static SinhVienDto ToDto(SinhVien s) => new()
        {
            Id = s.Id,
            HoTen = s.HoTen,
            Email = s.Email,
            Tuoi = s.Tuoi,
            AvatarUrl = s.AvatarUrl,
            RowVersion = s.RowVersion
        };
    }
}