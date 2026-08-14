using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using QuanLySinhVien.Models;

namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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
        public async Task<ActionResult<IEnumerable<SinhVien>>> GetAll()
        {
            return await _context.SinhVien.ToListAsync();
        }


        // ==============================
        // GET: api/SinhVien/1
        // XEM 1 SINH VIÊN
        // ==============================

        [HttpGet("{id}")]
        public async Task<ActionResult<SinhVien>> GetById(int id)
        {
            var sinhVien =
                await _context.SinhVien.FindAsync(id);

            if (sinhVien == null)
            {
                return NotFound();
            }

            return sinhVien;
        }


        // ==============================
        // POST: api/SinhVien
        // THÊM SINH VIÊN
        // ==============================

        [HttpPost]
        public async Task<ActionResult<SinhVien>> Create(
            SinhVien sinhVien)
        {
            _context.SinhVien.Add(sinhVien);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = sinhVien.Id },
                sinhVien
            );
        }


        // ==============================
        // PUT: api/SinhVien/1
        // SỬA SINH VIÊN
        // ==============================

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            SinhVien sinhVien)
        {
            if (id != sinhVien.Id)
            {
                return BadRequest();
            }

            _context.Entry(sinhVien).State =
                EntityState.Modified;

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