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

// DB: ưu tiên Postgres (Neon) nếu có connection string, fallback SQLite cho dev/local
var dbConnStr = builder.Configuration["Database:ConnectionString"];
if (!string.IsNullOrWhiteSpace(dbConnStr))
{
    // Hỗ trợ cả URL format (postgresql://user:pass@host/db?sslmode=require) lẫn key=value
    string npgsqlConnStr = dbConnStr;
    if (dbConnStr.StartsWith("postgres://") || dbConnStr.StartsWith("postgresql://"))
    {
        var uri = new Uri(dbConnStr);
        var userInfo = uri.UserInfo.Split(':');
        npgsqlConnStr = $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};" +
                        $"Database={uri.AbsolutePath.TrimStart('/')};" +
                        $"Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])};" +
                        $"SSL Mode=Require;Trust Server Certificate=true";
    }
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(npgsqlConnStr));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=fashionshop.db"));
}

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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ADLV Store API",
        Version = "v1",
        Description = "RESTful API cho ADLV Store — 60+ endpoints (Auth, Products, Categories, Cart, Orders, Payments, Users, Upload, Notifications).\n\n**Tài khoản test:**\n- Admin: `admin` / `admin123`\n- Customer: `elonmusk` / `musk2026`",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact { Name = "ADLV Store", Email = "vhphat2206@gmail.com" }
    });

    // Bearer auth UI để Authorize nhanh
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste JWT token (lấy từ POST /api/auth/login). Format: chỉ dán token, KHÔNG cần thêm 'Bearer'."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();

    if (db.Database.IsSqlite())
    {
        foreach (var sql in new[]
        {
            "ALTER TABLE Users ADD COLUMN IsLocked INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Users ADD COLUMN EmailVerified INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Users ADD COLUMN ResetToken TEXT NULL",
            "ALTER TABLE Users ADD COLUMN ResetTokenExpiry TEXT NULL",
            "ALTER TABLE Users ADD COLUMN EmailVerifyToken TEXT NULL",
            "ALTER TABLE Users ADD COLUMN FailedLoginCount INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Users ADD COLUMN LastFailedLoginAt TEXT NULL"
        })
        {
            try { using var cmd = conn.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
            catch { }
        }
    }
    else
    {
        // Postgres — idempotent ADD COLUMN + CREATE TABLE cho bảng/cột mới sau EnsureCreated
        foreach (var sql in new[]
        {
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"FailedLoginCount\" INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"LastFailedLoginAt\" TIMESTAMP NULL",
            @"CREATE TABLE IF NOT EXISTS ""CartItems"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                ""ProductId"" INTEGER NOT NULL REFERENCES ""Products""(""Id"") ON DELETE CASCADE,
                ""Quantity"" INTEGER NOT NULL DEFAULT 1,
                ""Size"" VARCHAR(20) NOT NULL DEFAULT '',
                ""Color"" VARCHAR(30) NOT NULL DEFAULT '',
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW())",
            @"CREATE TABLE IF NOT EXISTS ""Payments"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""OrderId"" INTEGER NOT NULL REFERENCES ""Orders""(""Id"") ON DELETE CASCADE,
                ""Method"" VARCHAR(20) NOT NULL DEFAULT 'COD',
                ""Status"" VARCHAR(20) NOT NULL DEFAULT 'Pending',
                ""Amount"" DECIMAL(18,2) NOT NULL DEFAULT 0,
                ""TransactionId"" VARCHAR(100) NULL,
                ""Note"" VARCHAR(500) NULL,
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                ""PaidAt"" TIMESTAMP NULL)",
            @"CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
                ""Token"" VARCHAR(200) NOT NULL,
                ""ExpiresAt"" TIMESTAMP NOT NULL,
                ""RevokedAt"" TIMESTAMP NULL,
                ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW())",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RefreshTokens_Token"" ON ""RefreshTokens""(""Token"")"
        })
        {
            try { using var cmd = conn.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
            catch { }
        }
    }

    await conn.CloseAsync();
    await SeedData.InitializeAsync(db);
}

var webRoot = app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(webRoot, "uploads");
Directory.CreateDirectory(uploadsPath);

// Swagger UI luôn bật cho cả production để demo cho thầy
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADLV Store API v1");
    c.RoutePrefix = "swagger"; // → /swagger
    c.DocumentTitle = "ADLV Store API Docs";
});

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
