using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("device_status_logs")]
    public class DeviceStatusLog
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("device_id")]
        public long DeviceId { get; set; }

        [ForeignKey("DeviceId")]
        public Device? Device { get; set; }

        [Column("old_status")]
        [MaxLength(50)]
        public string? OldStatus { get; set; }

        [Required]
        [Column("new_status")]
        [MaxLength(50)]
        public string NewStatus { get; set; } = string.Empty;

        [Column("value")]
        [MaxLength(120)]
        public string? Value { get; set; }

        [Column("source")]
        [MaxLength(50)]
        public string? Source { get; set; }

        [Column("raw_payload", TypeName = "jsonb")]
        public string? RawPayload { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
