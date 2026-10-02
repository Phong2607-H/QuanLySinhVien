using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Exceptions;
using QuanLySinhVien.Models;
namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SinhVienController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;// Để lấy đường dẫn tuyệt đối của thư mục wwwroot

        public SinhVienController(AppDbContext context, IWebHostEnvironment env )
        {
            _context = context;
            _env = env;
        }


        // ==============================
        // GET: api/SinhVien
        // XEM DANH SÁCH
        // ==============================
        
        [HttpGet]

        public async Task<ActionResult<PagedResult<SinhVienDto>>> GetAll([FromQuery] SinhVienQuery query)
        {
            if (query.PageNumber < 1) query.PageNumber = 1;// Vì nếu PageNumber < 1 thì sẽ bị lỗi khi tính toán Skip, nên mặc định là 1
            if (query.PageSize < 1 || query.PageSize > 50) query.PageSize = 5;// Vì nếu PageSize < 1 hoặc > 50 thì sẽ bị lỗi khi tính toán Take, nên mặc định là 5

            // Sử dụng IQueryable để xây dựng câu truy vấn động dưới SQL Server
            var queryable = _context.SinhVien.AsQueryable();

            // 1. Xử lý Tìm kiếm (Filtering)
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var keyword = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(s =>
                    s.HoTen.ToLower().Contains(keyword) ||
                    s.Email.ToLower().Contains(keyword)
                );
            }

            // 2. Xử lý Sắp xếp (Sorting)
            if (!string.IsNullOrWhiteSpace(query.SortBy))
            {
                var sortBy = query.SortBy?.Trim().ToLower();
                queryable = (sortBy, query.IsDescending) switch
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
            }
            else
            {
                queryable = queryable.OrderBy(s => s.Id); // Mặc định xếp theo Id
            }

            // Lấy tổng số dòng khớp điều kiện trước khi phân trang
            var totalCount = await queryable.CountAsync();

            // 3. Xử lý Phân trang (Pagination)
            var items = await queryable
                .Skip((query.PageNumber - 1) * query.PageSize) // Bỏ qua các dòng trang trước
                .Take(query.PageSize)                          // Lấy đúng số dòng quy định
                .Select(s => new SinhVienDto
                {
                    Id = s.Id,
                    HoTen = s.HoTen,
                    Email = s.Email,
                    Tuoi = s.Tuoi,
                    AvatarUrl = s.AvatarUrl
                })
                .ToListAsync();

            var result = new PagedResult<SinhVienDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };

            return Ok(result);
        }


        // ==============================
        // GET: api/SinhVien/1
        // XEM 1 SINH VIÊN
        // ==============================

        [HttpGet("{id}")]
        public async Task<ActionResult<SinhVienDto>> GetById(int id)
        {
            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null) throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!"
 );

            return new SinhVienDto
            {
                Id = sinhVien.Id,
                HoTen = sinhVien.HoTen,
                Email = sinhVien.Email,
                Tuoi = sinhVien.Tuoi,
                AvatarUrl = sinhVien.AvatarUrl
            };
        }


        // ==============================
        // POST: api/SinhVien
        // THÊM SINH VIÊN
        // ==============================

        [HttpPost]
        [Authorize(Roles = "Admin")]// Chỉ Admin mới được thêm sinh viên
        public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
        {
            var emailTonTai = await _context.SinhVien
               .AnyAsync(s => s.Email.ToLower() == sinhVienDto.Email.ToLower());
            if (emailTonTai)
            {
                throw new ConflictException("Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác.");
            }
            var sinhVien = new SinhVien
            {
                HoTen = sinhVienDto.HoTen,
                Email = sinhVienDto.Email,
                Tuoi = sinhVienDto.Tuoi,
                AvatarUrl = sinhVienDto.AvatarUrl
            };

            _context.SinhVien.Add(sinhVien);
            await _context.SaveChangesAsync();

            sinhVienDto.Id = sinhVien.Id;

            return CreatedAtAction(
                nameof(GetById),
                new { id = sinhVien.Id },
                sinhVienDto
            );
        }


        // ==============================
        // PUT: api/SinhVien/1
        // SỬA SINH VIÊN
        // ==============================

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]// Chỉ Admin mới được sửa sinh viên
        public async Task<IActionResult> Update(int id, SinhVienDto sinhVienDto)
        {
            if (id != sinhVienDto.Id)
            {
                throw new BadRequestException("Id trong URL không khớp với Id trong dữ liệu gửi lên!");
            }

            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null)
            {
                throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
            }
            var emailDaDung = await _context.SinhVien
               .AnyAsync(s => s.Email.ToLower() == sinhVienDto.Email.ToLower() && s.Id != id);
            if (emailDaDung)
            {
                throw new ConflictException("Email này đã được sử dụng bởi sinh viên khác!");
            }
            sinhVien.HoTen = sinhVienDto.HoTen;
            sinhVien.Email = sinhVienDto.Email;
            sinhVien.Tuoi = sinhVienDto.Tuoi;

            _context.Entry(sinhVien).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SinhVienExists(id))
                {
                    throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
                }
                throw;// Nếu có lỗi khác, ném ra để xử lý ở tầng trên
            }
           
            return NoContent();
        }


        // ==============================
        // DELETE: api/SinhVien/1
        // XÓA SINH VIÊN
        // ==============================

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // Chỉ Admin mới được xóa sinh viên
        public async Task<IActionResult> Delete(int id)
        {
            var sinhVien =
                await _context.SinhVien.FindAsync(id);

            if (sinhVien == null)
            {
                throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");
            }
            //_context.SinhVien.Remove(sinhVien); //Xoa cung
            sinhVien.IsDeleted = true; // Xoa mem
            _context.Entry(sinhVien).State = EntityState.Modified;


            await _context.SaveChangesAsync();

            return NoContent();
        }


        // Kiểm tra tồn tại
        private async Task<bool> SinhVienExists(int id)
        {
            return await _context.SinhVien.AnyAsync(s => s.Id == id);
        }
        
        [HttpPost("upload-avatar/{id}")]
        [Authorize(Roles = "Admin,GiangVien")]
        public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
        {
            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null)
                throw new NotFoundException($"Không tìm thấy sinh viên có Id = {id}!");

            if (file == null || file.Length == 0) throw new BadRequestException("Vui lòng chọn một file ảnh!");

            // 1. Kiểm tra định dạng đuôi file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new BadRequestException("Định dạng file không hợp lệ! Chỉ chấp nhận các định dạng: .jpg, .jpeg, .png");
            }

            // 2. Kiểm tra dung lượng file (tối đa 2MB)
            if (file.Length > 2 * 1024 * 1024)
            {
                throw new BadRequestException("Dung lượng file quá lớn! Vui lòng chọn file có dung lượng tối đa 2MB.");
            }

            // 3. Tạo thư mục lưu file: wwwroot/avatars
            var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Tạo tên file duy nhất tránh bị đè đè khi upload trùng tên
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // Lưu file vật lý vào thư mục server
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 1. Lưu lại đường dẫn ảnh cũ của sinh viên trước khi đổi
            var oldAvatarUrl = sinhVien.AvatarUrl;

            // 2. Cập nhật đường dẫn file ảnh mới vào CSDL
            sinhVien.AvatarUrl = $"/avatars/{uniqueFileName}";
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Lưu DB thất bại → xóa file mới vừa ghi để không để lại rác
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
                throw; // ném tiếp cho ExceptionMiddleware trả JSON lỗi
            }

            // 3. Xử lý xóa file ảnh cũ vật lý trên ổ cứng server
            if (!string.IsNullOrEmpty(oldAvatarUrl))
            {
                var oldAbsoluteFilePath = Path.Combine(_env.WebRootPath, oldAvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldAbsoluteFilePath))
                {
                    System.IO.File.Delete(oldAbsoluteFilePath);
                }
            }

            return Ok(new { AvatarUrl = sinhVien.AvatarUrl });
        }
    }
}   