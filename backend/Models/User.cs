using System.ComponentModel.DataAnnotations;

namespace backend.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Customer";

        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(10)]
        public string Gender { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string Address { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsLocked { get; set; } = false;

        public bool EmailVerified { get; set; } = false;

        [StringLength(100)]
        public string? ResetToken { get; set; }

        public DateTime? ResetTokenExpiry { get; set; }

        [StringLength(100)]
        public string? EmailVerifyToken { get; set; }
    }
}
