using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Exceptions;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuanLySinhVien.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context); // Cho request đi tiếp qua các bộ lọc khác
            }
            catch (Exception ex)
            {
                // Response đã bắt đầu gửi về client thì không ghi đè được nữa
                if (context.Response.HasStarted)
                {
                    _logger.LogError(ex, "Lỗi xảy ra sau khi response đã bắt đầu gửi");
                    throw;
                }

                // Lỗi do người dùng (4xx) chỉ là cảnh báo, lỗi hệ thống (5xx) mới là Error
                if (ex is AppException)
                    _logger.LogWarning("Lỗi nghiệp vụ: {Message}", ex.Message);
                else
                    _logger.LogError(ex, "Một sự cố hệ thống đã xảy ra: {Message}", ex.Message);

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // Mặc định lỗi 500

            var response = new ErrorResponse
            {
                StatusCode = context.Response.StatusCode,
                Message = "Đã xảy ra sự cố hệ thống! Vui lòng liên hệ Admin hoặc thử lại sau.",
                Details = _env.IsDevelopment() ? exception.StackTrace?.ToString() : null
            };

            // Phân loại mã lỗi HTTP dựa trên Kiểu dữ liệu của Exception (Exception Type Pattern Matching)
            var (statusCode, message) = exception switch
            {
                AppException appEx => (appEx.StatusCode, appEx.Message),
                UnauthorizedAccessException => (401, "Phiên đăng nhập đã hết hạn hoặc không hợp lệ!"),
                ArgumentException or BadHttpRequestException => (400, exception.Message),
                DbUpdateConcurrencyException => (409, "Dữ liệu đã bị thay đổi bởi người khác, vui lòng tải lại!"),
                DbUpdateException => (409, "Dữ liệu bị trùng hoặc vi phạm ràng buộc CSDL!"),
                _ => (500, "Đã xảy ra sự cố hệ thống! Vui lòng liên hệ Admin hoặc thử lại sau.")
            };
            context.Response.StatusCode = statusCode;
            response.StatusCode = statusCode;
            response.Message = message;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}