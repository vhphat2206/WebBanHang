using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly Cloudinary? _cloudinary;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxFileSize = 5 * 1024 * 1024;

        public UploadController(IConfiguration config, IWebHostEnvironment env)
        {
            _env = env;

            var cloudName = config["Cloudinary:CloudName"];
            var apiKey = config["Cloudinary:ApiKey"];
            var apiSecret = config["Cloudinary:ApiSecret"];

            if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
            {
                _cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
            }
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

            // Ưu tiên Cloudinary (vĩnh viễn). Nếu chưa config → fallback local (mất khi redeploy).
            if (_cloudinary != null)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "adlv-store",
                    Transformation = new Transformation().Width(800).Height(800).Crop("limit").Quality("auto").FetchFormat("auto")
                };
                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                    return BadRequest(new { message = "Cloudinary upload failed: " + result.Error?.Message });

                return Ok(new
                {
                    url = result.SecureUrl.ToString(),
                    absoluteUrl = result.SecureUrl.ToString(),
                    publicId = result.PublicId,
                    size = file.Length,
                    storage = "cloudinary"
                });
            }
            else
            {
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

                return Ok(new { url, absoluteUrl, fileName, size = file.Length, storage = "local" });
            }
        }
    }
}
