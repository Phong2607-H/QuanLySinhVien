using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuanLySinhVien.Data;
using QuanLySinhVien.DTOs;
using QuanLySinhVien.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new ErrorResponse
            {
                StatusCode = 400,
                Message = errors.Values.SelectMany(v => v).FirstOrDefault(m => !string.IsNullOrEmpty(m))
                          ?? "Dữ liệu gửi lên không hợp lệ!",
                Errors = errors
            });
        };
        options.SuppressMapClientErrors = true;
    });


// Cấu hình JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

builder.Services.AddHttpContextAccessor();// Cho phép truy cập HttpContext trong các lớp khác (ví dụ: AuditSaveChangesInterceptor)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});


// Kết nối SQL Server và cấu hình Audit Interceptor
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
    var auditInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>();
    options.AddInterceptors(auditInterceptor);
});


// Cho phép Angular gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    var status = http.Response.StatusCode;
    var message = status switch
    {
        401 => "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn!",
        403 => "Bạn không có quyền thực hiện chức năng này!",
        404 => "Không tìm thấy tài nguyên yêu cầu!",
        _ => "Đã xảy ra lỗi khi xử lý yêu cầu!"
    };
    await http.Response.WriteAsJsonAsync(new ErrorResponse { StatusCode = status, Message = message });
});

app.UseHttpsRedirection();


app.UseCors("AllowAngular");

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();


app.Run();
public partial class Program { }