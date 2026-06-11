using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("devices")]
    public class Device
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("device_code")]
        [MaxLength(100)]
        public string DeviceCode { get; set; } = string.Empty;

        [Required]
        [Column("device_name")]
        [MaxLength(120)]
        public string DeviceName { get; set; } = string.Empty;

        [Required]
        [Column("device_type")]
        [MaxLength(50)]
        public string DeviceType { get; set; } = string.Empty;

        [Required]
        [Column("node_id")]
        public int NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Column("location")]
        [MaxLength(100)]
        public string? Location { get; set; }

        [Column("gpio_pin")]
        [MaxLength(80)]
        public string? GpioPin { get; set; }

        [Column("voltage")]
        [MaxLength(20)]
        public string? Voltage { get; set; }

        [Column("mqtt_status_topic")]
        [MaxLength(150)]
        public string? MqttStatusTopic { get; set; }

        [Column("mqtt_command_topic")]
        [MaxLength(150)]
        public string? MqttCommandTopic { get; set; }

        [Required]
        [Column("is_controllable")]
        public bool IsControllable { get; set; } = false;

        [Required]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Required]
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "unknown";

        [Column("last_value")]
        [MaxLength(120)]
        public string? LastValue { get; set; }

        [Column("last_payload", TypeName = "jsonb")]
        public string? LastPayload { get; set; }

        [Column("last_updated_at")]
        public DateTime? LastUpdatedAt { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
