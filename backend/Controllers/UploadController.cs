using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file" });

            if (file.Length > MaxFileSize)
                return BadRequest(new { message = "File quá lớn (tối đa 5MB)" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new { message = "Định dạng không hỗ trợ. Chỉ chấp nhận: " + string.Join(", ", AllowedExtensions) });

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsDir = Path.Combine(webRoot, "uploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/{fileName}";
            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{url}";

            return Ok(new { url, absoluteUrl, fileName, size = file.Length });
        }
    }
}
