using System;
using System.ComponentModel.DataAnnotations;

namespace FIt3d.DAL.Entities
{
    public class Otp : BaseEntity
    {
        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        public DateTime ExpirationTime { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}
