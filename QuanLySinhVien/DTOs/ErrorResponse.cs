using System.Text.Json.Serialization;

namespace QuanLySinhVien.DTOs
{
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; } // Chỉ hiển thị ở môi trường phát triển (Dev)

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Chỉ serialize khi không null
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}