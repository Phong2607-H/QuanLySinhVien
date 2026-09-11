using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.Models;
using QuanLySinhVien.DTOs;
using Microsoft.AspNetCore.Authorization;
namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SinhVienController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SinhVienController(AppDbContext context)
        {
            _context = context;
        }


        // ==============================
        // GET: api/SinhVien
        // XEM DANH SÁCH
        // ==============================
        
        [HttpGet]

        public async Task<ActionResult<PagedResult<SinhVienDto>>> GetAll([FromQuery] SinhVienQuery query)
        {
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
                if (query.SortBy.Equals("HoTen", StringComparison.OrdinalIgnoreCase))
                {
                    queryable = query.IsDescending
                        ? queryable.OrderByDescending(s => s.HoTen)
                        : queryable.OrderBy(s => s.HoTen);
                }
                else if (query.SortBy.Equals("Tuoi", StringComparison.OrdinalIgnoreCase))
                {
                    queryable = query.IsDescending
                        ? queryable.OrderByDescending(s => s.Tuoi)
                        : queryable.OrderBy(s => s.Tuoi);
                }
                else
                {
                    queryable = queryable.OrderBy(s => s.Id);
                }
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
            if (sinhVien == null) return NotFound();

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
        public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
        {
            var emailTonTai = await _context.SinhVien
               .AnyAsync(s => s.Email.ToLower() == sinhVienDto.Email.ToLower());
            if (emailTonTai)
            {
                return BadRequest(new { message = "Email này đã tồn tại trong hệ thống! Vui lòng dùng email khác." });
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
        public async Task<IActionResult> Update(int id, SinhVienDto sinhVienDto)
        {
            if (id != sinhVienDto.Id)
            {
                return BadRequest();
            }

            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null)
            {
                return NotFound();
            }
            var emailDaDung = await _context.SinhVien
               .AnyAsync(s => s.Email.ToLower() == sinhVienDto.Email.ToLower() && s.Id != id);
            if (emailDaDung)
            {
                return BadRequest(new { message = "Email này đã được sử dụng bởi sinh viên khác!" });
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
                if (!SinhVienExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }


        // ==============================
        // DELETE: api/SinhVien/1
        // XÓA SINH VIÊN
        // ==============================

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var sinhVien =
                await _context.SinhVien.FindAsync(id);

            if (sinhVien == null)
            {
                return NotFound();
            }
            _context.SinhVien.Remove(sinhVien); //Xoa cung
            //sinhVien.IsDeleted = true; // Xoa mem
            //_context.Entry(sinhVien).State = EntityState.Modified;


            await _context.SaveChangesAsync();

            return NoContent();
        }


        // Kiểm tra tồn tại
        private bool SinhVienExists(int id)
        {
            return _context.SinhVien
                .Any(s => s.Id == id);
        }
        [HttpPost("upload-avatar/{id}")]
        public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
        {
            var sinhVien = await _context.SinhVien.FindAsync(id);
            if (sinhVien == null) return NotFound("Không tìm thấy sinh viên!");

            if (file == null || file.Length == 0) return BadRequest("Vui lòng chọn một file ảnh!");

            // 1. Kiểm tra định dạng đuôi file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Định dạng file không hợp lệ! Chỉ chấp nhận .jpg, .jpeg, .png");
            }

            // 2. Kiểm tra dung lượng file (tối đa 2MB)
            if (file.Length > 2 * 1024 * 1024)
            {
                return BadRequest("Dung lượng file quá lớn! Tối đa là 2MB.");
            }

            // 3. Tạo thư mục lưu file: wwwroot/avatars
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatars");
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
            _context.Entry(sinhVien).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // 3. Xử lý xóa file ảnh cũ vật lý trên ổ cứng server
            if (!string.IsNullOrEmpty(oldAvatarUrl))
            {
                var oldAbsoluteFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldAvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldAbsoluteFilePath))
                {
                    System.IO.File.Delete(oldAbsoluteFilePath);
                }
            }

            return Ok(new { AvatarUrl = sinhVien.AvatarUrl });
        }
    }
}   