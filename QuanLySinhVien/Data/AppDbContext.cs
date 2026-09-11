using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using QuanLySinhVien.Models;
using System.Security.Claims;
using System.Text.Json;

namespace QuanLySinhVien.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<SinhVien> SinhVien { get; set; }
        public DbSet<NguoiDung> NguoiDung { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
    }
}