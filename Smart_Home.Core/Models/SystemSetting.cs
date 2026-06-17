using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("system_settings")]
    public class SystemSetting
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("setting_key")]
        [MaxLength(100)]
        public string SettingKey { get; set; } = string.Empty;

        [Required]
        [Column("setting_value")]
        [MaxLength(255)]
        public string SettingValue { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
