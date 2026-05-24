using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");

        private static string StatusLabel(string s) => s switch
        {
            "Pending" => "Chờ xác nhận",
            "Confirmed" => "Đã xác nhận",
            "Shipping" => "Đang được giao",
            "Completed" => "Đã giao thành công",
            "Cancelled" => "Đã hủy",
            _ => s
        };

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] string? wishlist)
        {
            var userId = CurrentUserId;
            var now = DateTime.UtcNow;
            var notifs = new List<object>();

            // 1. Wishlist items đang được giảm giá
            if (!string.IsNullOrEmpty(wishlist))
            {
                var ids = wishlist.Split(',')
                    .Select(s => int.TryParse(s, out var n) ? n : 0)
                    .Where(n => n > 0)
                    .ToArray();
                if (ids.Length > 0)
                {
                    var sales = await _context.Products
                        .Where(p => ids.Contains(p.Id) && p.SalePrice != null && p.SalePrice < p.Price)
                        .Select(p => new { p.Id, p.Name, p.Price, p.SalePrice, p.ImageUrl })
                        .ToListAsync();

                    foreach (var p in sales)
                    {
                        var off = Math.Round((1 - (decimal)p.SalePrice! / p.Price) * 100);
                        notifs.Add(new
                        {
                            id = $"sale_{p.Id}",
                            type = "sale",
                            icon = "🔥",
                            title = $"Giảm {off}% — sản phẩm bạn yêu thích",
                            message = $"{p.Name} đang giảm còn {p.SalePrice:N0}đ",
                            timestamp = now,
                            link = $"product.html?id={p.Id}",
                            image = p.ImageUrl
                        });
                    }
                }
            }

            // 2. Voucher mới — chỉ tính chưa dùng + chưa hết hạn, gộp theo DisplayName
            var activeVouchers = await _context.UserVouchers
                .Where(v => v.UserId == userId
                            && !v.IsUsed
                            && v.ExpiresAt > now
                            && v.CreatedAt > now.AddDays(-30))
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            var grouped = activeVouchers
                .GroupBy(v => v.DisplayName)
                .Select(g => new
                {
                    Latest = g.OrderByDescending(v => v.CreatedAt).First(),
                    Count = g.Count(),
                    Name = g.Key
                });

            foreach (var grp in grouped)
            {
                notifs.Add(new
                {
                    id = $"voucher_group_{grp.Latest.Id}",
                    type = "voucher",
                    icon = "🎁",
                    title = grp.Count > 1
                        ? $"Bạn đang có {grp.Count} voucher đang chờ"
                        : "Bạn vừa nhận được voucher",
                    message = grp.Name,
                    timestamp = grp.Latest.CreatedAt,
                    link = "account.html",
                    image = (string?)null
                });
            }

            // 3. Đơn hàng đổi trạng thái (≤14 ngày)
            var recentOrders = await _context.Orders
                .Where(o => o.UserId == userId
                            && o.UpdatedAt > now.AddDays(-14)
                            && o.Status != "Pending")
                .OrderByDescending(o => o.UpdatedAt)
                .Take(10)
                .ToListAsync();
            foreach (var o in recentOrders)
            {
                var emoji = o.Status switch
                {
                    "Confirmed" => "✅",
                    "Shipping" => "🚚",
                    "Completed" => "📦",
                    "Cancelled" => "❌",
                    _ => "📋"
                };
                notifs.Add(new
                {
                    id = $"order_{o.Id}_{o.Status}",
                    type = "order",
                    icon = emoji,
                    title = $"Đơn {o.OrderCode}",
                    message = StatusLabel(o.Status),
                    timestamp = o.UpdatedAt,
                    link = "orders.html",
                    image = (string?)null
                });
            }

            // 4. System welcome (user mới ≤7 ngày)
            var user = await _context.Users.FindAsync(userId);
            if (user != null && (now - user.CreatedAt).TotalDays < 7)
            {
                notifs.Add(new
                {
                    id = "welcome",
                    type = "system",
                    icon = "👋",
                    title = $"Chào mừng đến với ADLV, {user.FullName}!",
                    message = "Khám phá ngay bộ sưu tập 26SS và dùng mã NEWTOADLV để giảm 10%.",
                    timestamp = user.CreatedAt,
                    link = "lookbook.html",
                    image = (string?)null
                });
            }

            return Ok(notifs.OrderByDescending(n => ((dynamic)n).timestamp).ToList());
        }
    }
}
