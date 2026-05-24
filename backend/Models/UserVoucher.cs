using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class UserVoucher
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [StringLength(100)]
        public string DisplayName { get; set; } = string.Empty;

        // "FixedAmount" (200000 = trừ 200K) hoặc "Percent" (0.10 = giảm 10%)
        [Required]
        [StringLength(20)]
        public string Type { get; set; } = "FixedAmount";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }

        // Đơn tối thiểu để dùng được voucher này
        [Column(TypeName = "decimal(18,2)")]
        public decimal MinSubtotal { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime? UsedAt { get; set; }

        public int? UsedInOrderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? EarnedFromOrderId { get; set; }
    }
}
