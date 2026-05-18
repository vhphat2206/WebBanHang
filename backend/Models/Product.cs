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
        public string Name { get; set; } = string.Empty; // Ví dụ: Áo Hoodie Jordan Black

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Giá bán

        public string Description { get; set; } = string.Empty; // Mô tả sản phẩm (Thương hiệu, chất liệu)

        public string ImageUrl { get; set; } = string.Empty; // Đường dẫn ảnh sản phẩm

        // Khóa ngoại liên kết danh mục
        public int CategoryId { get; set; }
        
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
    }
}