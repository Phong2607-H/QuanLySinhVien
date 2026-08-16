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
        public async Task<ActionResult<IEnumerable<SinhVienDto>>> GetAll()
        {
            return await _context.SinhVien
                .Select(s => new SinhVienDto
                {
                    Id = s.Id,
                    HoTen = s.HoTen,
                    Email = s.Email,
                    Tuoi = s.Tuoi
                })
                .ToListAsync();
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
                Tuoi = sinhVien.Tuoi
            };
        }


        // ==============================
        // POST: api/SinhVien
        // THÊM SINH VIÊN
        // ==============================

        [HttpPost]
        public async Task<ActionResult<SinhVienDto>> Create(SinhVienDto sinhVienDto)
        {
            var sinhVien = new SinhVien
            {
                HoTen = sinhVienDto.HoTen,
                Email = sinhVienDto.Email,
                Tuoi = sinhVienDto.Tuoi
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
            _context.SinhVien.Remove(sinhVien);

            await _context.SaveChangesAsync();

            return NoContent();
        }


        // Kiểm tra tồn tại
        private bool SinhVienExists(int id)
        {
            return _context.SinhVien
                .Any(s => s.Id == id);
        }
    }
}