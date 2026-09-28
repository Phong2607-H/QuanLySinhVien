using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Middleware;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace QuanLySinhVien.Tests
{
    public class ExceptionMiddlewareTests
    {
        private readonly Mock<ILogger<ExceptionMiddleware>> _mockLogger;
        private readonly Mock<IHostEnvironment> _mockEnv;

        public ExceptionMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<ExceptionMiddleware>>();
            _mockEnv = new Mock<IHostEnvironment>();
        }

        [Fact]
        public async Task InvokeAsync_KhiCoLoiBatNgo_TraVeLoi500VaDungFormatJson()
        {
            // 1. ARRANGE (Chuẩn bị dữ liệu giả lập)
            _mockEnv.Setup(m => m.EnvironmentName).Returns("Development");

            // Giả lập luồng request quăng ra Exception bất ngờ
            RequestDelegate next = (HttpContext ctx) => throw new Exception("Lỗi sập hệ thống giả lập!");

            var middleware = new ExceptionMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            var context = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // 2. ACT (Thực thi gọi hàm Middleware)
            await middleware.InvokeAsync(context);

            // 3. ASSERT (Kiểm tra kết quả)
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var result = JsonSerializer.Deserialize<ErrorResponse>(responseBody, jsonOptions);

            // Kiểm tra format JSON chuẩn { statusCode, message, details }
            Assert.NotNull(result);
            Assert.Equal(500, result.StatusCode);
            Assert.Equal("Đã xảy ra sự cố hệ thống! Vui lòng liên hệ Admin hoặc thử lại sau.", result.Message);
            Assert.NotNull(result.Details); // Môi trường Development phải có Details
        }

        [Fact]
        public async Task InvokeAsync_KhiBiLoiUnauthorized_TraVe401VaThongBaoHetHan()
        {
            // 1. ARRANGE
            _mockEnv.Setup(m => m.EnvironmentName).Returns("Development");
            RequestDelegate next = (HttpContext ctx) => throw new UnauthorizedAccessException();

            var middleware = new ExceptionMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            var context = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // 2. ACT
            await middleware.InvokeAsync(context);

            // 3. ASSERT
            Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var result = JsonSerializer.Deserialize<ErrorResponse>(responseBody, jsonOptions);

            Assert.NotNull(result);
            Assert.Equal(401, result.StatusCode);
            Assert.Equal("Phiên đăng nhập đã hết hạn hoặc không hợp lệ!", result.Message);
        }

        [Fact]
        public async Task InvokeAsync_KhiCoLoiDuLieuDauVao_TraVeLoi400VaDungMessage()
        {
            // 1. ARRANGE
            _mockEnv.Setup(m => m.EnvironmentName).Returns("Development");
            // Giả lập ngoại lệ dữ liệu không hợp lệ (ArgumentException)
            RequestDelegate next = (HttpContext ctx) => throw new ArgumentException("Dữ liệu đầu vào không hợp lệ!");

            var middleware = new ExceptionMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            var context = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // 2. ACT
            await middleware.InvokeAsync(context);

            // 3. ASSERT
            Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var result = JsonSerializer.Deserialize<ErrorResponse>(responseBody, jsonOptions);

            Assert.NotNull(result);
            Assert.Equal(400, result.StatusCode);
            Assert.Equal("Dữ liệu đầu vào không hợp lệ!", result.Message);
        }

        [Fact]
        public async Task InvokeAsync_KhiOEnvironmentProduction_TruongDetailsPhaiBangNull()
        {
            // 1. ARRANGE
            // Giả lập môi trường Production (triển khai thật tế)
            _mockEnv.Setup(m => m.EnvironmentName).Returns("Production");
            RequestDelegate next = (HttpContext ctx) => throw new Exception("Lỗi hệ thống nghiêm trọng");

            var middleware = new ExceptionMiddleware(next, _mockLogger.Object, _mockEnv.Object);

            var context = new DefaultHttpContext();
            var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            // 2. ACT
            await middleware.InvokeAsync(context);

            // 3. ASSERT
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var result = JsonSerializer.Deserialize<ErrorResponse>(responseBody, jsonOptions);

            Assert.NotNull(result);
            // Ở Production, details phải là null để bảo mật thông tin hệ thống
            Assert.Null(result.Details);
        }
    }
}
