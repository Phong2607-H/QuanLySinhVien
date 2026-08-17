namespace QuanLySinhVien.DTOs
{
    public class SinhVienQuery
    {
        public int PageNumber { get; set; } = 1; // Số trang hiện tại (mặc định trang 1)
        public int PageSize { get; set; } = 5;  // Số dòng mỗi trang (mặc định 5 dòng)
        public string? Keyword { get; set; }     // Từ khóa tìm kiếm
        public string? SortBy { get; set; }      // Tên cột sắp xếp (ví dụ: HoTen, Tuoi)
        public bool IsDescending { get; set; } = false; // Sắp xếp giảm dần?
    }
}