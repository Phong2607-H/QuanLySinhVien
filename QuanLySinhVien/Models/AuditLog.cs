using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLySinhVien.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Username { get; set; } = "Anonymous"; // Người thực hiện

        [MaxLength(20)]
        public string Action { get; set; } = string.Empty;   // Thêm / Sửa / Xóa

        [MaxLength(50)]
        public string TableName { get; set; } = string.Empty; // Bảng bị tác động (ví dụ: SinhVien)

        public string? OldValues { get; set; }               // Giá trị trước khi sửa (JSON)
        public string? NewValues { get; set; }               // Giá trị sau khi sửa (JSON)

        public DateTime Timestamp { get; set; } = DateTime.Now; // Thời gian ghi log
    }
}