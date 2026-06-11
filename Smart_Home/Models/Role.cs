using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Smart_Home.Models
{
    [Table("roles")]
    public class Role
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("role_name")]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
