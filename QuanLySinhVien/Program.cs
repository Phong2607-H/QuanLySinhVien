using Microsoft.EntityFrameworkCore;
using QuanLySinhVien.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();


// Kết nối SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


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


app.UseHttpsRedirection();


app.UseCors("AllowAngular");


app.UseAuthorization();


app.MapControllers();


app.Run();