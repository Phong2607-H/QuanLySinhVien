using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLySinhVien.DTOs;
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
                // Ghi log lỗi vào file/console của server
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
            switch (exception)
            {
                case UnauthorizedAccessException:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized; // 401
                    response.StatusCode = 401;
                    response.Message = "Phiên đăng nhập đã hết hạn hoặc không hợp lệ!";
                    break;

                case ArgumentException or BadHttpRequestException:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest; // 400
                    response.StatusCode = 400;
                    response.Message = exception.Message;
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500
                    response.StatusCode = 500;
                    break;
            }

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            await context.Response.WriteAsync(json);
        }
    }
}