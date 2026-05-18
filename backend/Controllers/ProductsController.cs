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

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _context.Products.Include(p => p.Category).ToListAsync());
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
            if (string.IsNullOrWhiteSpace(query))
            {
                return await _context.Products.Include(p => p.Category).ToListAsync();
            }

            var keywords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = _context.Products.Include(p => p.Category).AsQueryable();

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
            product.ImageUrl = input.ImageUrl;
            product.ImageUrls = input.ImageUrls;
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
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
