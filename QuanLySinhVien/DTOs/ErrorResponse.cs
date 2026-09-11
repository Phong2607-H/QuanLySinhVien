namespace QuanLySinhVien.DTOs
{
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; } // Chỉ hiển thị ở môi trường phát triển (Dev)
    }
}