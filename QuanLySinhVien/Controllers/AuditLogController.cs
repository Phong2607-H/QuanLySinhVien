using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;
using System.Threading.Tasks;
using QuanLySinhVien.DTOs;

namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")] //  Bắt buộc: Chỉ tài khoản Admin mới gọi được API này
    public class AuditLogController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditLogController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var logs = await _context.AuditLogs
                .AsNoTracking()// Không theo dõi các thay đổi của các bản ghi này trong DbContext
                .OrderByDescending(l => l.Timestamp) // Bản ghi mới nhất lên đầu
                .Select(l => new AuditLogDto
                {
                    Id = l.Id,
                    Username = l.Username,
                    Action = l.Action,
                    TableName = l.TableName,
                    OldValues = l.OldValues,
                    NewValues = l.NewValues,
                    Timestamp = l.Timestamp
                })// Chọn các trường cần thiết để trả về cho client
                .ToListAsync();
            return Ok(logs);
        }
    }
}