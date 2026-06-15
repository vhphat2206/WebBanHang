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
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PaymentsController(ApplicationDbContext context) { _context = context; }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub") ?? "0");
        private bool IsAdmin => User.IsInRole("Admin");

        public record CreatePaymentDto(int OrderId, string Method, string? Note);
        public record UpdateStatusDto(string Status, string? TransactionId);

        private static readonly string[] ValidMethods = { "COD", "Card", "Bank", "Momo", "Wallet" };
        private static readonly string[] ValidStatuses = { "Pending", "Paid", "Failed", "Refunded" };

        // POST /api/payments — Tạo thanh toán cho đơn hàng
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
        {
            if (!ValidMethods.Contains(dto.Method))
                return BadRequest(new { message = $"Phương thức không hợp lệ. Chấp nhận: {string.Join(", ", ValidMethods)}" });

            var order = await _context.Orders.FindAsync(dto.OrderId);
            if (order == null)
                return NotFound(new { message = "Đơn hàng không tồn tại" });

            if (!IsAdmin && order.UserId != CurrentUserId)
                return Forbid();

            // Không cho thanh toán lại đơn đã Paid
            var existingPaid = await _context.Payments
                .AnyAsync(p => p.OrderId == dto.OrderId && p.Status == "Paid");
            if (existingPaid)
                return BadRequest(new { message = "Đơn hàng đã được thanh toán" });

            var payment = new Payment
            {
                OrderId = dto.OrderId,
                Method = dto.Method,
                Amount = order.Total,
                Status = dto.Method == "COD" ? "Pending" : "Paid", // COD chờ giao, online tạm coi như paid (mô phỏng)
                Note = dto.Note,
                PaidAt = dto.Method == "COD" ? null : DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                payment.Id, payment.OrderId, payment.Method, payment.Status,
                payment.Amount, payment.CreatedAt, payment.PaidAt,
                message = "Tạo thanh toán thành công"
            });
        }

        // GET /api/payments — List (user thấy của mình, admin thấy tất)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var query = _context.Payments.Include(p => p.Order).AsQueryable();
            if (!IsAdmin)
                query = query.Where(p => p.Order!.UserId == CurrentUserId);

            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.OrderId,
                    orderCode = p.Order!.OrderCode,
                    p.Method, p.Status, p.Amount,
                    p.TransactionId, p.Note,
                    p.CreatedAt, p.PaidAt
                })
                .ToListAsync();

            return Ok(payments);
        }

        // GET /api/payments/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _context.Payments.Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            if (!IsAdmin && p.Order!.UserId != CurrentUserId)
                return Forbid();

            return Ok(new
            {
                p.Id, p.OrderId, orderCode = p.Order!.OrderCode,
                p.Method, p.Status, p.Amount,
                p.TransactionId, p.Note,
                p.CreatedAt, p.PaidAt
            });
        }

        // PUT /api/payments/{id}/status — Cập nhật trạng thái (Admin)
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            if (!ValidStatuses.Contains(dto.Status))
                return BadRequest(new { message = $"Trạng thái không hợp lệ. Chấp nhận: {string.Join(", ", ValidStatuses)}" });

            var p = await _context.Payments.FindAsync(id);
            if (p == null) return NotFound();

            if (p.Status == "Paid" && dto.Status == "Paid")
                return BadRequest(new { message = "Đơn hàng đã thanh toán, không cần update lại" });

            p.Status = dto.Status;
            if (!string.IsNullOrEmpty(dto.TransactionId))
                p.TransactionId = dto.TransactionId;
            if (dto.Status == "Paid")
                p.PaidAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật trạng thái", p.Id, p.Status, p.PaidAt });
        }
    }
}
