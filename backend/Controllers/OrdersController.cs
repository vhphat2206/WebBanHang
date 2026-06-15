using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] ValidStatuses =
            { "Pending", "Confirmed", "Shipping", "Completed", "Cancelled" };

        // Voucher hardcode đơn giản — production nên có bảng Voucher riêng
        // FREESHIP đã bỏ vì đơn ≥ 1tr tự động miễn ship
        private static readonly Dictionary<string, decimal> Vouchers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "NEWTOADLV", 0.10m },   // -10%
            { "WELCOME20", 0.20m }    // -20%
        };

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "0");

        private bool IsAdmin =>
            User.IsInRole("Admin");

        public record CartItemDto(
            int ProductId,
            string Size,
            string Color,
            int Quantity);

        public record CreateOrderDto(
            List<CartItemDto> Items,
            string CustomerName,
            string Phone,
            string Email,
            string ShippingAddress,
            string? PaymentMethod,
            string? VoucherCode,
            string? Notes,
            string? GiftBear);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            if (dto.Items == null || !dto.Items.Any())
                return BadRequest(new { message = "Giỏ hàng trống" });
            if (string.IsNullOrWhiteSpace(dto.CustomerName))
                return BadRequest(new { message = "Vui lòng nhập họ tên" });
            if (string.IsNullOrWhiteSpace(dto.Phone))
                return BadRequest(new { message = "Vui lòng nhập số điện thoại" });
            if (string.IsNullOrWhiteSpace(dto.ShippingAddress))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng" });

            // Load tất cả sản phẩm 1 lượt
            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // Kiểm tra tồn tại + stock
            foreach (var item in dto.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var p))
                    return BadRequest(new { message = $"Sản phẩm #{item.ProductId} không tồn tại" });
                if (item.Quantity <= 0)
                    return BadRequest(new { message = $"Số lượng không hợp lệ cho {p.Name}" });
                if (p.Stock < item.Quantity)
                    return BadRequest(new { message = $"{p.Name} chỉ còn {p.Stock} sản phẩm" });
            }

            // Tính tiền
            var orderItems = dto.Items.Select(item =>
            {
                var p = products[item.ProductId];
                var unitPrice = p.SalePrice ?? p.Price;
                return new OrderItem
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    ProductImage = p.ImageUrl,
                    Size = item.Size ?? "",
                    Color = item.Color ?? "",
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    Subtotal = unitPrice * item.Quantity
                };
            }).ToList();

            // Validate + thêm gấu quà tặng (Mua 2 áo tặng 1 gấu)
            var giftChoice = (dto.GiftBear ?? "").Trim();
            if (!string.IsNullOrEmpty(giftChoice))
            {
                if (giftChoice != "Nam" && giftChoice != "Nữ")
                    return BadRequest(new { message = "Quà tặng chỉ chấp nhận 'Nam' hoặc 'Nữ'" });

                var aoCount = orderItems
                    .Where(i => i.ProductId.HasValue
                                && products.TryGetValue(i.ProductId.Value, out var p)
                                && p.CategoryId == 1)  // Category Id 1 = Áo
                    .Sum(i => i.Quantity);
                if (aoCount < 2)
                    return BadRequest(new { message = "Cần mua từ 2 áo trở lên mới nhận quà gấu ADLV" });

                orderItems.Add(new OrderItem
                {
                    ProductId = null,
                    ProductName = $"🎁 Gấu ADLV {giftChoice} — Quà tặng",
                    // Path relative (không leading slash) — frontend dùng làm asset path
                    ProductImage = giftChoice == "Nam" ? "assets/bear-male.png" : "assets/bear-female.png",
                    Size = "",
                    Color = giftChoice,
                    Quantity = 1,
                    UnitPrice = 0,
                    Subtotal = 0
                });
            }

            var subtotal = orderItems.Sum(i => i.Subtotal);
            // Free ship cho đơn từ 1 triệu trở lên
            var shippingFee = subtotal >= 1000000 ? 0m : 30000m;
            decimal discount = 0m;
            var voucherCode = (dto.VoucherCode ?? "").Trim();

            UserVoucher? personalVoucher = null;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                if (Vouchers.TryGetValue(voucherCode, out var rate))
                {
                    // Voucher công khai (NEWTOADLV, WELCOME20)
                    // Mỗi user chỉ dùng được 1 lần — check Orders cũ
                    var alreadyUsed = await _context.Orders.AnyAsync(o =>
                        o.UserId == CurrentUserId
                        && o.VoucherCode.ToUpper() == voucherCode.ToUpper()
                        && o.Status != "Cancelled");
                    if (alreadyUsed)
                        return BadRequest(new { message = $"Bạn đã sử dụng mã '{voucherCode}' rồi, mỗi tài khoản chỉ dùng 1 lần" });

                    discount = Math.Round(subtotal * rate, 0);
                }
                else
                {
                    // Tìm voucher cá nhân của user
                    personalVoucher = await _context.UserVouchers
                        .FirstOrDefaultAsync(v => v.Code == voucherCode && v.UserId == CurrentUserId);
                    if (personalVoucher == null)
                        return BadRequest(new { message = $"Mã '{voucherCode}' không hợp lệ" });
                    if (personalVoucher.IsUsed)
                        return BadRequest(new { message = "Voucher này đã được sử dụng" });
                    if (personalVoucher.ExpiresAt < DateTime.UtcNow)
                        return BadRequest(new { message = "Voucher đã hết hạn" });
                    if (subtotal < personalVoucher.MinSubtotal)
                        return BadRequest(new { message = $"Đơn tối thiểu {personalVoucher.MinSubtotal:N0}đ để dùng voucher này" });

                    if (personalVoucher.Type == "FreeProduct")
                    {
                        // Tặng 1 SP bất kỳ → trừ giá SP có UnitPrice cao nhất trong đơn (chỉ 1 cái)
                        discount = orderItems.Max(i => i.UnitPrice);
                    }
                    else if (personalVoucher.Type == "Percent")
                    {
                        discount = Math.Round(subtotal * personalVoucher.Value, 0);
                    }
                    else
                    {
                        discount = personalVoucher.Value;
                    }
                    discount = Math.Min(discount, subtotal); // không cho discount > subtotal
                }
            }

            var total = subtotal + shippingFee - discount;

            var order = new Order
            {
                UserId = CurrentUserId,
                OrderCode = $"ADLV{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(10, 99)}",
                Status = "Pending",
                CustomerName = dto.CustomerName.Trim(),
                Phone = dto.Phone.Trim(),
                Email = (dto.Email ?? "").Trim(),
                ShippingAddress = dto.ShippingAddress.Trim(),
                PaymentMethod = dto.PaymentMethod ?? "COD",
                Notes = dto.Notes ?? "",
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                VoucherCode = voucherCode,
                DiscountAmount = discount,
                Total = total,
                Items = orderItems
            };

            // Trừ stock
            foreach (var item in orderItems)
            {
                if (item.ProductId.HasValue)
                    products[item.ProductId.Value].Stock -= item.Quantity;
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Đánh dấu voucher đã dùng
            if (personalVoucher != null)
            {
                personalVoucher.IsUsed = true;
                personalVoucher.UsedAt = DateTime.UtcNow;
                personalVoucher.UsedInOrderId = order.Id;
                await _context.SaveChangesAsync();
            }

            // Sinh voucher thưởng theo tier (chỉ 1 voucher, lấy tier cao nhất đạt được)
            UserVoucher? newReward = null;
            string suffix = order.OrderCode.Length >= 6 ? order.OrderCode.Substring(order.OrderCode.Length - 6) : order.OrderCode;
            if (subtotal >= 5000000)
            {
                newReward = new UserVoucher
                {
                    UserId = CurrentUserId,
                    Code = $"ADLV-VIP-1FREE-{suffix}",
                    DisplayName = "Tặng 1 sản phẩm bất kỳ (miễn phí SP có giá cao nhất)",
                    Type = "FreeProduct",
                    Value = 0m, // không dùng — discount = max(unit price) ở thời điểm apply
                    MinSubtotal = 0m,
                    ExpiresAt = DateTime.UtcNow.AddDays(60),
                    EarnedFromOrderId = order.Id
                };
            }
            else if (subtotal >= 3000000)
            {
                newReward = new UserVoucher
                {
                    UserId = CurrentUserId,
                    Code = $"ADLV-400K-{suffix}",
                    DisplayName = "Giảm 400.000₫ cho đơn tiếp theo",
                    Type = "FixedAmount",
                    Value = 400000m,
                    MinSubtotal = 500000m,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    EarnedFromOrderId = order.Id
                };
            }
            else if (subtotal >= 2000000)
            {
                newReward = new UserVoucher
                {
                    UserId = CurrentUserId,
                    Code = $"ADLV-200K-{suffix}",
                    DisplayName = "Giảm 200.000₫ cho đơn tiếp theo",
                    Type = "FixedAmount",
                    Value = 200000m,
                    MinSubtotal = 500000m,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    EarnedFromOrderId = order.Id
                };
            }
            if (newReward != null)
            {
                _context.UserVouchers.Add(newReward);
                await _context.SaveChangesAsync();
            }

            // Clear server-side cart sau khi đặt hàng thành công (PDF mục 5)
            var userCart = await _context.CartItems.Where(c => c.UserId == CurrentUserId).ToListAsync();
            if (userCart.Any())
            {
                _context.CartItems.RemoveRange(userCart);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                order.Id,
                order.OrderCode,
                order.Status,
                order.Total,
                order.CreatedAt,
                itemCount = order.Items.Count
            });
        }

        public record FromCartDto(
            string CustomerName,
            string Phone,
            string ShippingAddress,
            string? Email,
            string? PaymentMethod,
            string? VoucherCode,
            string? Notes,
            string? GiftBear);

        // POST /api/orders/from-cart — Tạo đơn từ giỏ hàng server-side (PDF mục 5)
        [HttpPost("from-cart")]
        public async Task<IActionResult> CreateFromCart([FromBody] FromCartDto info)
        {
            var cartItems = await _context.CartItems
                .Where(c => c.UserId == CurrentUserId)
                .Select(c => new CartItemDto(c.ProductId, c.Size, c.Color, c.Quantity))
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "Giỏ hàng trống — không thể đặt hàng" });

            var dto = new CreateOrderDto(
                cartItems,
                info.CustomerName,
                info.Phone,
                info.Email ?? "",
                info.ShippingAddress,
                info.PaymentMethod,
                info.VoucherCode,
                info.Notes,
                info.GiftBear);

            return await Create(dto);
        }

        [HttpGet("my-vouchers")]
        public async Task<IActionResult> GetMyVouchers()
        {
            var now = DateTime.UtcNow;
            var personal = await _context.UserVouchers
                .Where(v => v.UserId == CurrentUserId && !v.IsUsed && v.ExpiresAt > now)
                .OrderBy(v => v.ExpiresAt)
                .Select(v => new
                {
                    code = v.Code,
                    description = v.DisplayName,
                    discount = v.Type == "FreeProduct"
                        ? "1 SP miễn phí"
                        : (v.Type == "Percent" ? $"{v.Value * 100:0}%" : $"{v.Value:N0}đ"),
                    type = v.Type,
                    expiresAt = v.ExpiresAt,
                    minSubtotal = v.MinSubtotal,
                    isPersonal = true
                })
                .ToListAsync();
            return Ok(personal);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyOrders()
        {
            var orders = await _context.Orders
                .Where(o => o.UserId == CurrentUserId)
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return Ok(orders);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            if (order.UserId != CurrentUserId && !IsAdmin)
                return Forbid();

            return Ok(order);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return Ok(orders);
        }

        public record UpdateStatusDto(string Status);

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            if (!ValidStatuses.Contains(dto.Status))
                return BadRequest(new { message = $"Status không hợp lệ. Phải là: {string.Join(", ", ValidStatuses)}" });

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();

            // Nếu hủy đơn → hoàn lại stock (chỉ với SP còn tồn tại)
            if (dto.Status == "Cancelled" && order.Status != "Cancelled")
            {
                foreach (var item in order.Items)
                {
                    if (!item.ProductId.HasValue) continue;
                    var product = await _context.Products.FindAsync(item.ProductId.Value);
                    if (product != null) product.Stock += item.Quantity;
                }
            }

            order.Status = dto.Status;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { order.Id, order.Status, order.UpdatedAt });
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelMyOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound();
            if (order.UserId != CurrentUserId)
                return Forbid();
            if (order.Status != "Pending")
                return BadRequest(new { message = "Chỉ hủy được đơn ở trạng thái Pending" });

            foreach (var item in order.Items)
            {
                if (!item.ProductId.HasValue) continue;
                var product = await _context.Products.FindAsync(item.ProductId.Value);
                if (product != null) product.Stock += item.Quantity;
            }

            order.Status = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { order.Id, order.Status });
        }

        [HttpGet("vouchers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVouchers()
        {
            var all = new[]
            {
                new { code = "NEWTOADLV", description = "Giảm 10% cho đơn hàng đầu tiên", discount = "10%" },
                new { code = "WELCOME20", description = "Giảm 20% cho khách hàng mới", discount = "20%" }
            };

            // Nếu user đã đăng nhập → lọc bỏ mã đã dùng (mỗi tài khoản 1 lần)
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = CurrentUserId;
                var usedCodes = (await _context.Orders
                    .Where(o => o.UserId == userId
                                && o.VoucherCode != ""
                                && o.Status != "Cancelled")
                    .Select(o => o.VoucherCode)
                    .Distinct()
                    .ToListAsync())
                    .Select(c => c.ToUpper())
                    .ToHashSet();

                return Ok(all.Where(v => !usedCodes.Contains(v.code.ToUpper())).ToList());
            }
            return Ok(all);
        }
    }
}
