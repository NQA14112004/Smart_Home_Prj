using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("full_name")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Column("username")]
        [MaxLength(50)]
        public string? Username { get; set; }

        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        [Column("phone")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Column("email")]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        [Column("role_id")]
        public int RoleId { get; set; }
        
        [ForeignKey("RoleId")]
        public Role? Role { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "active";

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public ICollection<RfidCard> RfidCards { get; set; } = new List<RfidCard>();
        public ICollection<PinCode> PinCodes { get; set; } = new List<PinCode>();
    }
}
