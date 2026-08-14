namespace QuanLySinhVien.Models
{
    public class SinhVien
    {
        public int Id { get; set; }

        public string HoTen { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int Tuoi { get; set; }
    }
}