using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("mqtt_topics")]
    public class MqttTopic
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("topic_code")]
        [MaxLength(100)]
        public string TopicCode { get; set; } = string.Empty;

        [Required]
        [Column("topic_name")]
        [MaxLength(150)]
        public string TopicName { get; set; } = string.Empty;

        [Required]
        [Column("topic")]
        [MaxLength(200)]
        public string Topic { get; set; } = string.Empty;

        [Required]
        [Column("direction")]
        [MaxLength(20)]
        public string Direction { get; set; } = string.Empty;

        [Column("node_id")]
        public int? NodeId { get; set; }

        [ForeignKey("NodeId")]
        public EspNode? Node { get; set; }

        [Column("description")]
        public string? Description { get; set; }
    }
}
