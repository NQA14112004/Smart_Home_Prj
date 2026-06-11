using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("access_logs")]
    public class AccessLog
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("node_id")]
        public int NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Column("user_id")]
        public long? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Column("card_uid")]
        [MaxLength(50)]
        public string? CardUid { get; set; }

        [Required]
        [Column("method")]
        [MaxLength(50)]
        public string Method { get; set; } = string.Empty;

        [Required]
        [Column("result")]
        [MaxLength(50)]
        public string Result { get; set; } = string.Empty;

        [Column("door_status")]
        [MaxLength(30)]
        public string? DoorStatus { get; set; }

        [Column("lock_status")]
        [MaxLength(30)]
        public string? LockStatus { get; set; }

        [Column("message")]
        public string? Message { get; set; }

        [Column("raw_payload", TypeName = "jsonb")]
        public string? RawPayload { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
