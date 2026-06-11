using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("rfid_cards")]
    public class RfidCard
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("user_id")]
        public long UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [Column("card_uid")]
        [MaxLength(50)]
        public string CardUid { get; set; } = string.Empty;

        [Column("card_label")]
        [MaxLength(100)]
        public string? CardLabel { get; set; }

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("deactivated_at")]
        public DateTime? DeactivatedAt { get; set; }
    }
}
