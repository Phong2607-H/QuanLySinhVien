using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Exceptions;
using QuanLySinhVien.Models;
using QuanLySinhVien.Services;
namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SinhVienController : ControllerBase
    {
        private readonly AppDbContext _context;         // vẫn cần cho UploadAvatar
        private readonly IWebHostEnvironment _env;
        private readonly SinhVienService _service;

        public SinhVienController(AppDbContext context, IWebHostEnvironment env, SinhVienService service)
        {
            _context = context;
            _env = env;
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<SinhVienDto>>> GetAll([FromQuery] SinhVienQuery query)
            => Ok(await _service.GetAllAsync(query));

        [HttpGet("{id}")]
        public async Task<ActionResult<SinhVienDto>> GetById(int id)
            => await _service.GetByIdAsync(id);

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
        {
            var created = await _service.CreateAsync(sinhVienDto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, SinhVienDto sinhVienDto)
        {
            await _service.UpdateAsync(id, sinhVienDto);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }

        // SinhVienExists: xóa (không còn dùng)
        // UploadAvatar: giữ nguyên

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