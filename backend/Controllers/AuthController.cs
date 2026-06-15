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
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, JwtService jwt, EmailService email, IConfiguration config)
        {
            _context = context;
            _jwt = jwt;
            _email = email;
            _config = config;
        }

        private string GetBaseUrl()
        {
            var publicUrl = _config["App:PublicUrl"];
            if (!string.IsNullOrEmpty(publicUrl)) return publicUrl.TrimEnd('/');
            return $"{Request.Scheme}://{Request.Host}";
        }

        public record LoginDto(string Username, string Password, bool RememberMe = false);
        public record RegisterDto(string Username, string Password, string FullName, string Email);
        public record UpdateProfileDto(string FullName, string Email, string? Phone, string? Gender, DateTime? DateOfBirth, string? Address, string? AvatarUrl);
        public record ChangePasswordDto(string OldPassword, string NewPassword);
        public record ForgotPasswordDto(string Email);
        public record ResetPasswordDto(string Token, string NewPassword);
        public record VerifyEmailDto(string Token);
        public record RefreshTokenDto(string RefreshToken);

        private async Task<string> IssueRefreshTokenAsync(int userId, bool rememberMe)
        {
            var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
                .Replace("/", "_").Replace("+", "-").Replace("=", "");
            var days = rememberMe ? 90 : 7;
            _context.RefreshTokens.Add(new Models.RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(days)
            });
            await _context.SaveChangesAsync();
            return token;
        }

        private const int MaxFailedLogins = 5;

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });

            if (user.IsLocked)
                return Unauthorized(new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                // Đếm lần login sai, khóa nếu >= MaxFailedLogins (chỉ với Customer)
                user.FailedLoginCount++;
                user.LastFailedLoginAt = DateTime.UtcNow;
                var remaining = MaxFailedLogins - user.FailedLoginCount;

                if (user.FailedLoginCount >= MaxFailedLogins && user.Role != "Admin")
                {
                    user.IsLocked = true;
                    await _context.SaveChangesAsync();
                    return Unauthorized(new { message = $"Sai mật khẩu {MaxFailedLogins} lần liên tiếp. Tài khoản đã bị khóa." });
                }

                await _context.SaveChangesAsync();
                return Unauthorized(new
                {
                    message = remaining > 0
                        ? $"Sai tài khoản hoặc mật khẩu (còn {remaining} lần thử)"
                        : "Sai tài khoản hoặc mật khẩu"
                });
            }

            // Login thành công — reset counter
            user.FailedLoginCount = 0;
            user.LastFailedLoginAt = null;
            await _context.SaveChangesAsync();

            var token = _jwt.GenerateToken(user, dto.RememberMe);
            var refreshToken = await IssueRefreshTokenAsync(user.Id, dto.RememberMe);
            return Ok(new
            {
                token,
                refreshToken,
                rememberMe = dto.RememberMe,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.EmailVerified, user.AvatarUrl }
            });
        }

        // POST /api/auth/refresh — Đổi access token mới bằng refresh token
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
                return BadRequest(new { message = "Thiếu refresh token" });

            var stored = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (stored == null || !stored.IsActive)
                return Unauthorized(new { message = "Refresh token không hợp lệ hoặc đã hết hạn" });

            if (stored.User == null || stored.User.IsLocked)
                return Unauthorized(new { message = "Tài khoản đã bị khóa" });

            // Token rotation: revoke cái cũ, cấp cái mới
            stored.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var rememberMe = (stored.ExpiresAt - stored.CreatedAt).TotalDays > 30;
            var newAccessToken = _jwt.GenerateToken(stored.User, rememberMe);
            var newRefreshToken = await IssueRefreshTokenAsync(stored.User.Id, rememberMe);

            return Ok(new
            {
                token = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        // POST /api/auth/logout — Revoke refresh token
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.RefreshToken))
            {
                var stored = await _context.RefreshTokens
                    .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);
                if (stored != null && stored.RevokedAt == null)
                {
                    stored.RevokedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            return Ok(new { message = "Đã đăng xuất" });
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

            // Gửi email xác thực nếu có config SMTP, nếu không thì trả link trong response (demo)
            var verifyLink = $"{GetBaseUrl()}/verify-email.html?token={verifyToken}";
            var emailSent = false;
            if (!string.IsNullOrEmpty(user.Email))
            {
                emailSent = await _email.SendAsync(
                    user.Email,
                    "Xác thực tài khoản ADLV Store",
                    EmailService.BuildVerifyEmailHtml(user.FullName, verifyLink));
            }

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new { user.Id, user.Username, user.FullName, user.Email, user.Role, user.EmailVerified },
                verifyToken = emailSent ? null : verifyToken,
                verifyUrl = emailSent ? null : $"/verify-email.html?token={verifyToken}",
                emailSent,
                message = emailSent
                    ? $"Đăng ký thành công. Link xác thực đã gửi tới {user.Email}."
                    : "Đăng ký thành công. Vui lòng xác thực email (demo: dùng link bên dưới)."
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Vui lòng nhập email" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            // Demo trả lỗi rõ ràng nếu email không tồn tại để UX dễ debug.
            // Production nên trả OK generic để tránh leak email enumeration.
            if (user == null)
            {
                return NotFound(new { message = "Email này chưa được đăng ký trong hệ thống" });
            }

            user.ResetToken = Guid.NewGuid().ToString("N");
            user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var resetLink = $"{GetBaseUrl()}/reset-password.html?token={user.ResetToken}";
            var emailSent = await _email.SendAsync(
                user.Email,
                "Đặt lại mật khẩu — ADLV Store",
                EmailService.BuildResetPasswordHtml(user.FullName, resetLink));

            return Ok(new
            {
                message = emailSent
                    ? $"Link đặt lại mật khẩu đã được gửi tới {user.Email}. Kiểm tra hộp thư của bạn (kể cả Spam)."
                    : $"Link đặt lại mật khẩu đã được tạo cho {user.Username}. Có hiệu lực 1 giờ.",
                resetToken = emailSent ? null : user.ResetToken,
                resetUrl = emailSent ? null : $"/reset-password.html?token={user.ResetToken}",
                emailSent
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

            var verifyLink = $"{GetBaseUrl()}/verify-email.html?token={user.EmailVerifyToken}";
            var emailSent = await _email.SendAsync(
                user.Email,
                "Xác thực tài khoản ADLV Store",
                EmailService.BuildVerifyEmailHtml(user.FullName, verifyLink));

            return Ok(new
            {
                message = emailSent
                    ? $"Link xác thực đã gửi tới {user.Email}. Kiểm tra hộp thư."
                    : "Đã tạo lại link xác thực.",
                verifyToken = emailSent ? null : user.EmailVerifyToken,
                verifyUrl = emailSent ? null : $"/verify-email.html?token={user.EmailVerifyToken}",
                emailSent
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (user.IsLocked) return Unauthorized(new { message = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên." });
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
