using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ADLV CDN dùng path /resize/{WxH}/ — tự nâng thumbnail size lên 914x1200
        // để ảnh hiển thị nét trên trang chi tiết.
        private static string UpscaleAdlvUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (!url.Contains("dytbw3ui6vsu6.cloudfront.net")) return url;
            foreach (var small in new[] { "/resize/100x100/", "/resize/200x200/", "/resize/360x480/", "/resize/750x750/" })
            {
                if (url.Contains(small))
                    url = url.Replace(small, "/resize/914x1200/");
            }
            return url;
        }

        private static string UpscaleAdlvUrls(string urls)
        {
            if (string.IsNullOrWhiteSpace(urls)) return urls;
            var parts = urls.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(",", parts.Select(u => UpscaleAdlvUrl(u.Trim())));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool includeDeleted = false,
            [FromQuery] int? categoryId = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sort = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var query = _context.Products.Include(p => p.Category).AsQueryable();
            if (!includeDeleted) query = query.Where(p => !p.IsDeleted);

            // Lọc theo danh mục
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            // Lọc theo khoảng giá (so sánh trên giá hiệu lực = SalePrice ?? Price)
            if (minPrice.HasValue)
                query = query.Where(p => (p.SalePrice ?? p.Price) >= minPrice.Value);
            if (maxPrice.HasValue)
                query = query.Where(p => (p.SalePrice ?? p.Price) <= maxPrice.Value);

            // Sort theo giá: price_asc / price_desc / newest (mặc định)
            query = sort?.ToLower() switch
            {
                "price_asc" => query.OrderBy(p => p.SalePrice ?? p.Price),
                "price_desc" => query.OrderByDescending(p => p.SalePrice ?? p.Price),
                "name_asc" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            // Pagination — nếu có page+pageSize trả wrapper, không thì list thẳng (backward compat)
            if (page.HasValue && pageSize.HasValue && pageSize.Value > 0)
            {
                var total = await query.CountAsync();
                var data = await query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync();
                return Ok(new
                {
                    page = page.Value,
                    pageSize = pageSize.Value,
                    total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize.Value),
                    data
                });
            }

            return Ok(await query.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();
            return Ok(product);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string? query)
        {
            var result = _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            if (string.IsNullOrWhiteSpace(query))
                return Ok(await result.ToListAsync());

            var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyword in keywords)
            {
                result = result.Where(p =>
                    p.Name.ToLower().Contains(keyword) ||
                    p.Description.ToLower().Contains(keyword) ||
                    p.Brand.ToLower().Contains(keyword) ||
                    p.Sku.ToLower().Contains(keyword) ||
                    (p.Category != null && p.Category.Name.ToLower().Contains(keyword)));
            }

            return Ok(await result.ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            product.ImageUrl = UpscaleAdlvUrl(product.ImageUrl);
            product.ImageUrls = UpscaleAdlvUrls(product.ImageUrls);
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var created = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == product.Id);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Product input)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = input.Name;
            product.Sku = input.Sku;
            product.Price = input.Price;
            product.SalePrice = input.SalePrice;
            product.Description = input.Description;
            product.ImageUrl = UpscaleAdlvUrl(input.ImageUrl);
            product.ImageUrls = UpscaleAdlvUrls(input.ImageUrls);
            product.Brand = input.Brand;
            product.Material = input.Material;
            product.Fit = input.Fit;
            product.CareInstructions = input.CareInstructions;
            product.Notes = input.Notes;
            product.Sizes = input.Sizes;
            product.Colors = input.Colors;
            product.Stock = input.Stock;
            product.IsNew = input.IsNew;
            product.IsBestseller = input.IsBestseller;
            product.CategoryId = input.CategoryId;
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id, [FromQuery] bool permanent = false)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            if (permanent)
            {
                // Xóa hẳn khỏi DB — không thể hoàn tác
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã xóa vĩnh viễn sản phẩm", permanent = true });
            }

            // Soft delete: đánh dấu IsDeleted=true → khách hàng không thấy, admin có thể hoàn tác
            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã chuyển vào thùng rác", permanent = false, id = product.Id });
        }

        [HttpPut("{id}/restore")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Restore(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            product.IsDeleted = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã hiện lại sản phẩm" });
        }
    }
}
