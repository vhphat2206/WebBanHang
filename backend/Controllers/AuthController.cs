using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwt;

        public AuthController(ApplicationDbContext context, JwtService jwt)
        {
            _context = context;
            _jwt = jwt;
        }

        public record LoginDto(string Username, string Password);
        public record RegisterDto(string Username, string Password, string FullName, string Email);
        public record UpdateProfileDto(string FullName, string Email, string? Phone, string? Gender, DateTime? DateOfBirth, string? Address, string? AvatarUrl);
        public record ChangePasswordDto(string OldPassword, string NewPassword);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });
            }

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
            {
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại" });
            }

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Email = dto.Email,
                Role = "Customer"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role }
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            return Ok(new
            {
                user.Id, user.Username, user.FullName, user.Email, user.Role,
                user.Phone, user.Gender, user.DateOfBirth, user.Address, user.AvatarUrl,
                user.CreatedAt
            });
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email))    user.Email = dto.Email.Trim();
            if (dto.Phone != null)        user.Phone = dto.Phone.Trim();
            if (dto.Gender != null)       user.Gender = dto.Gender.Trim();
            if (dto.DateOfBirth.HasValue) user.DateOfBirth = dto.DateOfBirth.Value;
            if (dto.Address != null)      user.Address = dto.Address.Trim();
            if (dto.AvatarUrl != null)    user.AvatarUrl = dto.AvatarUrl.Trim();

            await _context.SaveChangesAsync();
            return Ok(new
            {
                user.Id, user.Username, user.FullName, user.Email, user.Role,
                user.Phone, user.Gender, user.DateOfBirth, user.Address, user.AvatarUrl
            });
        }

        [HttpGet("stats")]
        [Authorize]
        public async Task<IActionResult> GetStats()
        {
            var userId = CurrentUserId;
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .ToListAsync();

            var totalOrders = orders.Count;
            var completedOrders = orders.Count(o => o.Status == "Completed");
            var totalSpent = orders
                .Where(o => o.Status != "Cancelled")
                .Sum(o => o.Total);
            var activeVouchers = await _context.UserVouchers
                .CountAsync(v => v.UserId == userId && !v.IsUsed && v.ExpiresAt > DateTime.UtcNow);
            var memberDays = (DateTime.UtcNow - user.CreatedAt).Days;

            return Ok(new
            {
                totalOrders,
                completedOrders,
                pendingOrders = orders.Count(o => o.Status == "Pending" || o.Status == "Confirmed" || o.Status == "Shipping"),
                totalSpent,
                activeVouchers,
                memberDays,
                memberSince = user.CreatedAt
            });
        }

        [HttpPut("password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
                return BadRequest(new { message = "Mật khẩu cũ không đúng" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã đổi mật khẩu thành công" });
        }
    }
}
