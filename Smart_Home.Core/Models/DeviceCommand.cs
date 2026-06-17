using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("device_commands")]
    public class DeviceCommand
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("device_id")]
        public long? DeviceId { get; set; }

        [ForeignKey("DeviceId")]
        public Device? Device { get; set; }

        [Required]
        [Column("node_id")]
        public int NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Required]
        [Column("command")]
        [MaxLength(50)]
        public string Command { get; set; } = string.Empty;

        [Column("payload", TypeName = "jsonb")]
        public string? Payload { get; set; }

        [Required]
        [Column("source")]
        [MaxLength(50)]
        public string Source { get; set; } = "wpf";

        [Column("requested_by_user_id")]
        public long? RequestedByUserId { get; set; }

        [ForeignKey("RequestedByUserId")]
        public User? RequestedByUser { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = "pending";

        [Required]
        [Column("requested_at")]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("acknowledged_at")]
        public DateTime? AcknowledgedAt { get; set; }

        [Column("response_message")]
        public string? ResponseMessage { get; set; }
    }
}
