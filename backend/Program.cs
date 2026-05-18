// ĐƯỢC ĐẶT TRÊN CÙNG ĐẦU FILE (Đúng chuẩn quy tắc)
using Microsoft.EntityFrameworkCore;
using backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Thêm cấu hình kết nối Database SQLite
builder.Services.AddDbContext<backend.Data.ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=fashionshop.db"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();