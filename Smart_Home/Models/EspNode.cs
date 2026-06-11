using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("esp_nodes")]
    public class EspNode
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("node_code")]
        [MaxLength(50)]
        public string NodeCode { get; set; } = string.Empty;

        [Required]
        [Column("node_name")]
        [MaxLength(100)]
        public string NodeName { get; set; } = string.Empty;

        [Required]
        [Column("node_type")]
        [MaxLength(50)]
        public string NodeType { get; set; } = "esp32";

        [Column("location")]
        [MaxLength(100)]
        public string? Location { get; set; }

        [Column("ip_address")]
        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [Column("mac_address")]
        [MaxLength(50)]
        public string? MacAddress { get; set; }

        [Column("firmware_version")]
        [MaxLength(50)]
        public string? FirmwareVersion { get; set; }

        [Column("mqtt_client_id")]
        [MaxLength(100)]
        public string? MqttClientId { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "offline";

        [Column("last_seen_at")]
        public DateTime? LastSeenAt { get; set; }

        [Required]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
