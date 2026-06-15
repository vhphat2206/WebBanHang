using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var cats = await _context.Categories
                .Select(c => new { c.Id, c.Name, ProductCount = c.Products.Count(p => !p.IsDeleted) })
                .ToListAsync();
            return Ok(cats);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await _context.Categories
                .Where(c => c.Id == id)
                .Select(c => new { c.Id, c.Name, ProductCount = c.Products.Count(p => !p.IsDeleted) })
                .FirstOrDefaultAsync();
            if (cat == null) return NotFound();
            return Ok(cat);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Category cat)
        {
            if (string.IsNullOrWhiteSpace(cat.Name))
                return BadRequest(new { message = "Tên danh mục không được rỗng" });
            _context.Categories.Add(cat);
            await _context.SaveChangesAsync();
            return Ok(cat);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Category input)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return BadRequest(new { message = "Tên danh mục không được rỗng" });
            var cat = await _context.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            cat.Name = input.Name;
            await _context.SaveChangesAsync();
            return Ok(cat);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _context.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            // Chặn xóa nếu còn SP (kể cả soft-deleted) thuộc danh mục
            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
                return BadRequest(new { message = "Không thể xóa danh mục vì còn sản phẩm. Hãy chuyển SP sang danh mục khác hoặc xóa SP trước." });

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
