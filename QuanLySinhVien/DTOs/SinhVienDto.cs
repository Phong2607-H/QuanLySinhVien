using System.ComponentModel.DataAnnotations;

namespace QuanLySinhVien.DTOs
{
    public class SinhVienDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Họ và tên không được để trống!")]
        public string HoTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống!")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng!")]
        public string Email { get; set; } = string.Empty;

        [Range(18, 99, ErrorMessage = "Tuổi phải là số dương từ 18 đến 99!")]
        public int Tuoi { get; set; }

        public string? AvatarUrl { get; set; }
    }
}