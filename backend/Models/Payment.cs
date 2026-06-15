using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        [StringLength(20)]
        public string Method { get; set; } = "COD";

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";

        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? TransactionId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }
}
