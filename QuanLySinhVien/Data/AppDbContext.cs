using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Models;


namespace QuanLySinhVien.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<SinhVien> SinhVien { get; set; }
    }
}