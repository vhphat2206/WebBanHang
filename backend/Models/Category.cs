using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // Ví dụ: Quần, Áo, Giày, Nón

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}