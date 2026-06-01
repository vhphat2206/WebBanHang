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

        public record LoginDto(string Username, string Password, bool RememberMe = false);
        public record RegisterDto(string Username, string Password, string FullName, string Email);
        public record UpdateProfileDto(string FullName, string Email, string? Phone, string? Gender, DateTime? DateOfBirth, string? Address, string? AvatarUrl);
        public record ChangePasswordDto(string OldPassword, string NewPassword);
        public record ForgotPasswordDto(string Email);
        public record ResetPasswordDto(string Token, string NewPassword);
        public record VerifyEmailDto(string Token);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });
            }

            if (user.IsLocked)
            {
                return Unauthorized(new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });
            }

            var token = _jwt.GenerateToken(user, dto.RememberMe);
            return Ok(new
            {
                token,
                rememberMe = dto.RememberMe,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.EmailVerified, user.AvatarUrl }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
            {
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại" });
            }
            if (!string.IsNullOrWhiteSpace(dto.Email) && await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "Email đã được sử dụng" });
            }

            var verifyToken = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FullName = dto.FullName,
                Email = dto.Email,
                Role = "Customer",
                EmailVerifyToken = verifyToken,
                EmailVerified = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.EmailVerified },
                verifyToken,
                verifyUrl = $"/verify-email.html?token={verifyToken}",
                message = "Đăng ký thành công. Vui lòng xác thực email."
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Vui lòng nhập email" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return Ok(new { message = "Nếu email tồn tại, link đặt lại mật khẩu đã được gửi." });
            }

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            // Demo không có SMTP — trả link reset ngay trong response để user copy.
            return Ok(new
            {
                message = "Link đặt lại mật khẩu đã được tạo. Có hiệu lực 1 giờ.",
                resetToken = user.ResetToken,
                resetUrl = $"/reset-password.html?token={user.ResetToken}"
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == dto.Token);
            if (user == null || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
                return BadRequest(new { message = "Token không hợp lệ hoặc đã hết hạn" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Token))
                return BadRequest(new { message = "Token không hợp lệ" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailVerifyToken == dto.Token);
            if (user == null)
                return BadRequest(new { message = "Token không hợp lệ hoặc đã được sử dụng" });

            user.EmailVerified = true;
            user.EmailVerifyToken = null;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xác thực email thành công!", user = new { user.Id, user.Username, user.Email } });
        }

        [HttpPost("resend-verify")]
        [Authorize]
        public async Task<IActionResult> ResendVerify()
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (user.EmailVerified) return BadRequest(new { message = "Email đã được xác thực" });

            user.EmailVerifyToken = Guid.NewGuid().ToString("N");
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Đã tạo lại link xác thực.",
                verifyToken = user.EmailVerifyToken,
                verifyUrl = $"/verify-email.html?token={user.EmailVerifyToken}"
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
                user.CreatedAt, user.EmailVerified, user.IsLocked
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
