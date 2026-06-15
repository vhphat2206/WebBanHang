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
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public CartController(ApplicationDbContext context) { _context = context; }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub") ?? "0");

        public record AddItemDto(int ProductId, int Quantity, string? Size, string? Color);
        public record UpdateQuantityDto(int Quantity);

        // GET /api/cart — Xem giỏ hàng
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var items = await _context.CartItems
                .Where(c => c.UserId == CurrentUserId)
                .Include(c => c.Product)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.ProductId,
                    productName = c.Product!.Name,
                    productImage = c.Product.ImageUrl,
                    productBrand = c.Product.Brand,
                    unitPrice = c.Product.SalePrice ?? c.Product.Price,
                    originalPrice = c.Product.Price,
                    salePrice = c.Product.SalePrice,
                    stock = c.Product.Stock,
                    c.Quantity,
                    c.Size,
                    c.Color,
                    lineTotal = (c.Product.SalePrice ?? c.Product.Price) * c.Quantity,
                    c.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                items,
                totalItems = items.Sum(i => i.Quantity),
                totalProducts = items.Count,
                subTotal = items.Sum(i => i.lineTotal)
            });
        }

        // POST /api/cart/items — Thêm SP vào giỏ
        [HttpPost("items")]
        public async Task<IActionResult> AddItem([FromBody] AddItemDto dto)
        {
            if (dto.Quantity <= 0)
                return BadRequest(new { message = "Số lượng phải lớn hơn 0" });

            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null || product.IsDeleted)
                return NotFound(new { message = "Sản phẩm không tồn tại" });

            if (product.Stock < dto.Quantity)
                return BadRequest(new { message = $"Chỉ còn {product.Stock} sản phẩm trong kho" });

            var size = (dto.Size ?? "").Trim();
            var color = (dto.Color ?? "").Trim();

            var existing = await _context.CartItems.FirstOrDefaultAsync(c =>
                c.UserId == CurrentUserId &&
                c.ProductId == dto.ProductId &&
                c.Size == size && c.Color == color);

            if (existing != null)
            {
                existing.Quantity += dto.Quantity;
                if (existing.Quantity > product.Stock)
                    return BadRequest(new { message = $"Tổng số lượng vượt tồn kho (còn {product.Stock})" });
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = CurrentUserId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    Size = size,
                    Color = color
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã thêm vào giỏ hàng" });
        }

        // PUT /api/cart/items/{id} — Cập nhật số lượng
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateQuantity(int id, [FromBody] UpdateQuantityDto dto)
        {
            if (dto.Quantity <= 0)
                return BadRequest(new { message = "Số lượng phải lớn hơn 0" });

            var item = await _context.CartItems.Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
            if (item == null)
                return NotFound(new { message = "Mục giỏ hàng không tồn tại" });

            if (item.Product!.Stock < dto.Quantity)
                return BadRequest(new { message = $"Chỉ còn {item.Product.Stock} trong kho" });

            item.Quantity = dto.Quantity;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật số lượng", item.Id, item.Quantity });
        }

        // DELETE /api/cart/items/{id} — Xóa 1 SP khỏi giỏ
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
            if (item == null)
                return NotFound(new { message = "Mục giỏ hàng không tồn tại" });

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa khỏi giỏ" });
        }

        // DELETE /api/cart — Xóa toàn bộ giỏ
        [HttpDelete]
        public async Task<IActionResult> Clear()
        {
            var items = await _context.CartItems
                .Where(c => c.UserId == CurrentUserId).ToListAsync();
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã xóa {items.Count} mục khỏi giỏ" });
        }

        // GET /api/cart/total — Tính tổng tiền
        [HttpGet("total")]
        public async Task<IActionResult> Total()
        {
            var items = await _context.CartItems
                .Where(c => c.UserId == CurrentUserId)
                .Include(c => c.Product)
                .ToListAsync();

            var subTotal = items.Sum(c => (c.Product!.SalePrice ?? c.Product.Price) * c.Quantity);
            var totalItems = items.Sum(c => c.Quantity);
            return Ok(new { totalItems, totalProducts = items.Count, subTotal });
        }
    }
}
