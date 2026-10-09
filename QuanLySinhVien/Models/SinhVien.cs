using System.ComponentModel.DataAnnotations;

namespace QuanLySinhVien.Models
{
    public class SinhVien
    {
        public int Id { get; set; }

        public string HoTen { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int Tuoi { get; set; }
        public string? AvatarUrl { get; set; } // Cho phép Null nếu sinh viên chưa có ảnh
        public bool IsDeleted { get; set; } = false;

        [Timestamp]                              // ← báo EF: đây là cột concurrency
        public byte[]? RowVersion { get; set; }
    }
}