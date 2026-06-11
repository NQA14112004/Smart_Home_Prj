using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("alerts")]
    public class Alert
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("alert_type")]
        [MaxLength(50)]
        public string AlertType { get; set; } = string.Empty;

        [Required]
        [Column("level")]
        [MaxLength(20)]
        public string Level { get; set; } = string.Empty;

        [Column("node_id")]
        public int? NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Column("device_id")]
        public long? DeviceId { get; set; }

        [ForeignKey("DeviceId")]
        public Device? Device { get; set; }

        [Required]
        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("value")]
        [MaxLength(120)]
        public string? Value { get; set; }

        [Column("threshold")]
        [MaxLength(120)]
        public string? Threshold { get; set; }

        [Column("raw_payload", TypeName = "jsonb")]
        public string? RawPayload { get; set; }

        [Required]
        [Column("is_resolved")]
        public bool IsResolved { get; set; } = false;

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [Column("resolved_by_user_id")]
        public long? ResolvedByUserId { get; set; }

        [ForeignKey("ResolvedByUserId")]
        public User? ResolvedByUser { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
