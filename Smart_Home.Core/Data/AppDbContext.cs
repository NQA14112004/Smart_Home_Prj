using Microsoft.EntityFrameworkCore;
using Smart_Home.Models;

namespace Smart_Home.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<EspNode> EspNodes { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<RfidCard> RfidCards { get; set; }
        public DbSet<PinCode> PinCodes { get; set; }
        public DbSet<AccessLog> AccessLogs { get; set; }
        public DbSet<SensorReading> SensorReadings { get; set; }
        public DbSet<DeviceCommand> DeviceCommands { get; set; }
        public DbSet<DeviceStatusLog> DeviceStatusLogs { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<MqttTopic> MqttTopics { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>()
                .HasIndex(r => r.RoleName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<EspNode>()
                .HasIndex(e => e.NodeCode)
                .IsUnique();

            modelBuilder.Entity<Device>()
                .HasIndex(d => d.DeviceCode)
                .IsUnique();

            modelBuilder.Entity<RfidCard>()
                .HasIndex(r => r.CardUid)
                .IsUnique();

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(s => s.SettingKey)
                .IsUnique();

            modelBuilder.Entity<MqttTopic>()
                .HasIndex(m => m.TopicCode)
                .IsUnique();

            modelBuilder.Entity<MqttTopic>()
                .HasIndex(m => m.Topic)
                .IsUnique();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                return await Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }
    }
}
