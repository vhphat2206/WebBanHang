using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string Sku { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SalePrice { get; set; }

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string ImageUrls { get; set; } = string.Empty;

        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;

        [StringLength(100)]
        public string Material { get; set; } = string.Empty;

        [StringLength(100)]
        public string Fit { get; set; } = string.Empty;

        public string CareInstructions { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public string Sizes { get; set; } = string.Empty;

        public string Colors { get; set; } = string.Empty;

        public int Stock { get; set; }

        public bool IsNew { get; set; }

        public bool IsBestseller { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
    }
}
