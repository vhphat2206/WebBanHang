using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search = null)
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(s) ||
                    u.Email.ToLower().Contains(s) ||
                    u.FullName.ToLower().Contains(s));
            }

            // Load users + tất cả orders trong memory rồi compute (SQLite-friendly)
            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var userIds = users.Select(u => u.Id).ToHashSet();
            var orders = await _context.Orders
                .Where(o => userIds.Contains(o.UserId))
                .Select(o => new { o.UserId, o.Status, o.Total })
                .ToListAsync();

            var statsMap = orders
                .GroupBy(o => o.UserId)
                .ToDictionary(g => g.Key, g => new
                {
                    OrderCount = g.Count(),
                    TotalSpent = g.Where(o => o.Status != "Cancelled").Sum(o => o.Total)
                });

            var result = users.Select(u =>
            {
                statsMap.TryGetValue(u.Id, out var s);
                return new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.Phone,
                    u.Role,
                    u.IsLocked,
                    u.EmailVerified,
                    u.AvatarUrl,
                    u.CreatedAt,
                    OrderCount = s?.OrderCount ?? 0,
                    TotalSpent = s?.TotalSpent ?? 0m
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return Ok(new
            {
                user.Id, user.Username, user.FullName, user.Email, user.Phone,
                user.Role, user.IsLocked, user.EmailVerified, user.AvatarUrl,
                user.Address, user.CreatedAt
            });
        }

        [HttpPut("{id}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Role == "Admin") return BadRequest(new { message = "Không thể khóa tài khoản Admin" });

            user.IsLocked = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã khóa tài khoản {user.Username}", user.Id, user.IsLocked });
        }

        [HttpPut("{id}/unlock")]
        public async Task<IActionResult> Unlock(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsLocked = false;
            user.FailedLoginCount = 0;
            user.LastFailedLoginAt = null;
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã mở khóa tài khoản {user.Username}", user.Id, user.IsLocked });
        }

        [HttpPut("{id}/role")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleDto dto)
        {
            if (dto.Role != "Admin" && dto.Role != "Customer")
                return BadRequest(new { message = "Role phải là 'Admin' hoặc 'Customer'" });

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub") ?? "0");
            if (id == currentUserId)
                return BadRequest(new { message = "Không thể thay đổi role của chính mình" });

            user.Role = dto.Role;
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã cập nhật role thành {dto.Role}", user.Id, user.Role });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Role == "Admin") return BadRequest(new { message = "Không thể xóa tài khoản Admin" });

            var hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);
            if (hasOrders)
                return BadRequest(new { message = "Không thể xóa user đã có đơn hàng. Hãy khóa tài khoản thay vì xóa." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã xóa tài khoản {user.Username}" });
        }

        public record UpdateRoleDto(string Role);
    }
}
