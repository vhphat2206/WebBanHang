using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using backend.Data;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Load appsettings.Local.json nếu tồn tại (cho local dev, gitignored)
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=fashionshop.db"));

builder.Services.AddSingleton<JwtService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<EmailService>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    // ALTER TABLE để thêm columns mới cho DB đã exist (không mất data)
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    foreach (var sql in new[]
    {
        "ALTER TABLE Users ADD COLUMN IsLocked INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Users ADD COLUMN EmailVerified INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Users ADD COLUMN ResetToken TEXT NULL",
        "ALTER TABLE Users ADD COLUMN ResetTokenExpiry TEXT NULL",
        "ALTER TABLE Users ADD COLUMN EmailVerifyToken TEXT NULL"
    })
    {
        try { using var cmd = conn.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
        catch { /* column đã tồn tại — bỏ qua */ }
    }
    await conn.CloseAsync();

    await SeedData.InitializeAsync(db);
}

var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRoot, "uploads");
Directory.CreateDirectory(uploadsPath);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseStaticFiles();

// Explicit static handler cho /uploads — Render không có wwwroot mặc định
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Phục vụ luôn frontend từ folder ../frontend → 1 lệnh dotnet run là chạy cả web
// Truy cập: http://localhost:5083/ → index.html
var frontendPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend"));
if (Directory.Exists(frontendPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        DefaultFileNames = new List<string> { "index.html" }
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath)
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
