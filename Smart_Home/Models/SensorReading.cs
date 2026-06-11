using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("sensor_readings")]
    public class SensorReading
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("node_id")]
        public int NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Column("temperature")]
        public double? Temperature { get; set; }

        [Column("humidity")]
        public double? Humidity { get; set; }

        [Column("gas_value")]
        public int? GasValue { get; set; }

        [Column("light_value")]
        public int? LightValue { get; set; }

        [Column("door_status")]
        [MaxLength(30)]
        public string? DoorStatus { get; set; }

        [Column("lock_status")]
        [MaxLength(30)]
        public string? LockStatus { get; set; }

        [Column("motion_detected")]
        public bool? MotionDetected { get; set; }

        [Column("raw_payload", TypeName = "jsonb")]
        public string? RawPayload { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
