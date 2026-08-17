using System.Collections.Generic;

namespace QuanLySinhVien.DTOs
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new(); // Danh sách dữ liệu trang hiện tại
        public int TotalCount { get; set; }        // Tổng số dòng trong CSDL
        public int PageNumber { get; set; }        // Trang hiện tại
        public int PageSize { get; set; }          // Kích thước trang
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize); // Tính tổng số trang
    }
}