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

        // GET: api/products/search?query=quần jordan
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Product>>> SearchProducts([FromQuery] string? query)
        {
            // Tự động thêm dữ liệu mẫu nếu chưa có gì trong DB
            await SeedDataAsync();

            // Nếu không gõ từ khóa nào, trả về toàn bộ sản phẩm
            if (string.IsNullOrWhiteSpace(query))
            {
                return await _context.Products.Include(p => p.Category).ToListAsync();
            }

            // Tách chuỗi thành các từ khóa riêng biệt (ví dụ: "quần jordan" -> ["quần", "jordan"])
            var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var result = _context.Products.Include(p => p.Category).AsQueryable();

            // Duyệt qua từng từ khóa để lọc (thỏa mãn CẢ 2 hoặc NHIỀU từ khóa)
            foreach (var keyword in keywords)
            {
                result = result.Where(p => p.Name.ToLower().Contains(keyword) 
                                        || p.Description.ToLower().Contains(keyword)
                                        || (p.Category != null && p.Category.Name.ToLower().Contains(keyword)));
            }

            return Ok(await result.ToListAsync());
        }

        // Hàm hỗ trợ tự động bơm dữ liệu (Đã sửa lỗi vòng lặp Object)
        private async Task SeedDataAsync()
        {
            if (!await _context.Categories.AnyAsync())
            {
                // 1. Tạo và lưu Danh mục trước để lấy ID thực tế từ DB
                var ao = new Category { Name = "Áo" };
                var quan = new Category { Name = "Quần" };
                var giay = new Category { Name = "Giày" };
                var non = new Category { Name = "Nón" };

                await _context.Categories.AddRangeAsync(ao, quan, giay, non);
                await _context.SaveChangesAsync();

                // 2. Dùng đúng ID đã sinh ra để gán cho sản phẩm
                var products = new List<Product>
                {
                    new Product { Name = "Áo Thun Nike Jordan T-Shirt", Price = 550000, Description = "Áo thun cotton chính hãng Jordan", CategoryId = ao.Id, ImageUrl = "ao_jordan.jpg" },
                    new Product { Name = "Quần Short Thể Thao Jordan Pro", Price = 650000, Description = "Quần short thoáng khí thương hiệu Jordan Air", CategoryId = quan.Id, ImageUrl = "quan_jordan.jpg" },
                    new Product { Name = "Quần Dài Jean Levi's Slim Fit", Price = 1200000, Description = "Quần jean chất bò cao cấp", CategoryId = quan.Id, ImageUrl = "quan_jean.jpg" },
                    new Product { Name = "Giày Sneaker Air Jordan 1 Low", Price = 3500000, Description = "Giày cổ thấp phối màu đen trắng hot trend", CategoryId = giay.Id, ImageUrl = "giay_jordan.jpg" },
                    new Product { Name = "Nón Lưỡi Trai Adidas Originals", Price = 350000, Description = "Nón kết thể thao Adidas", CategoryId = non.Id, ImageUrl = "non_adidas.jpg" }
                };

                await _context.Products.AddRangeAsync(products);
                await _context.SaveChangesAsync();
            }
        }
    }
}