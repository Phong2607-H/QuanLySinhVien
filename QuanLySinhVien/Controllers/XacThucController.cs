using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuanLySinhVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class XacThucController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public XacThucController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("dangky")]
        public async Task<IActionResult> DangKy(DangKyDto dto)
        {
            // Kiểm tra rỗng
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Tài khoản và mật khẩu không được để trống!");
            }
            if (await _context.NguoiDung.AnyAsync(u => u.Username == dto.Username))
            {
                return BadRequest("Tài khoản đã tồn tại!");
            }

            var nguoiDung = new NguoiDung
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName
            };

            _context.NguoiDung.Add(nguoiDung);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng ký thành công!" });
        }

        [HttpPost("dangnhap")]
        public async Task<IActionResult> DangNhap(DangNhapDto dto)
        {
            // Kiểm tra rỗng
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest("Tài khoản và mật khẩu không được để trống!");
            }

            var nguoiDung = await _context.NguoiDung.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (nguoiDung == null) return BadRequest("Tài khoản hoặc mật khẩu không chính xác!");

            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(dto.Password, nguoiDung.PasswordHash);
            if (!isPasswordCorrect) return BadRequest("Tài khoản hoặc mật khẩu không chính xác!");

            var token = GenerateJwtToken(nguoiDung);
            return Ok(new { Token = token, FullName = nguoiDung.FullName, Role = nguoiDung.Role });
        }

        private string GenerateJwtToken(NguoiDung nguoiDung)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, nguoiDung.Id.ToString()),
                new Claim(ClaimTypes.Name, nguoiDung.Username),
                new Claim(ClaimTypes.Role, nguoiDung.Role), //Backend phan quyen API
                new Claim("FullName", nguoiDung.FullName)
            };

            var key = new SymmetricSecurityKey(secretKey);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
