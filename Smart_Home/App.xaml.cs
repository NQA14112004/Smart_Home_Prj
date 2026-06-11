using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smart_Home.Data;
using Smart_Home.Models;
using Smart_Home.Service;
using Smart_Home.ViewModels;

namespace Smart_Home
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Populate process environment from a .env file (if present) before configuration is built,
            // so .AddEnvironmentVariables() and the connection-string lookups below can see those values.
            EnvFileLoader.Load();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<App>()
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Start node-presence tracking on the UI thread (its DispatcherTimer requires the UI thread).
            ServiceProvider.GetRequiredService<NodePresenceService>().Start();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            _ = ConnectMqttAsync();
        }

        private async Task ConnectMqttAsync()
        {
            try
            {
                var mqttService = ServiceProvider.GetRequiredService<IMqttService>();
                await mqttService.ConnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể kết nối MQTT Broker: " + ex.Message, "Cảnh báo MQTT", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.Configure<MqttOptions>(options =>
            {
                options.Host = Configuration["MqttOptions:Host"] ?? "localhost";
                options.Port = int.TryParse(Configuration["MqttOptions:Port"], out var port) ? port : 1883;
                options.TlsPort = int.TryParse(Configuration["MqttOptions:TlsPort"], out var tlsPort) ? tlsPort : 8883;
                options.ClientId = Configuration["MqttOptions:ClientId"] ?? "WpfSmartHomeClient";
                options.ClientNode = Configuration["MqttOptions:ClientNode"] ?? "wpf";
                // Credentials: environment variables (MQTT_USERNAME/MQTT_PASSWORD) take precedence over the secretless appsettings.json.
                options.Username = Environment.GetEnvironmentVariable("MQTT_USERNAME")
                    ?? Configuration["MqttOptions:Username"];
                options.Password = Environment.GetEnvironmentVariable("MQTT_PASSWORD")
                    ?? Configuration["MqttOptions:Password"];
                options.UseTls = bool.TryParse(Configuration["MqttOptions:UseTls"], out var tls) && tls;
                options.CaCertPath = Configuration["MqttOptions:CaCertPath"];
                options.AllowUntrustedCertificates =
                    bool.TryParse(Configuration["MqttOptions:AllowUntrustedCertificates"], out var allow) && allow;
            });

            services.Configure<NodePresenceOptions>(options =>
            {
                options.HeartbeatIntervalSeconds =
                    int.TryParse(Configuration["NodePresenceOptions:HeartbeatIntervalSeconds"], out var hb) ? hb : 30;
                options.MissedHeartbeatsBeforeOffline =
                    int.TryParse(Configuration["NodePresenceOptions:MissedHeartbeatsBeforeOffline"], out var miss) ? miss : 3;
                options.TimerTickSeconds =
                    int.TryParse(Configuration["NodePresenceOptions:TimerTickSeconds"], out var tick) ? tick : 5;
                options.PresenceTopicPattern =
                    Configuration["NodePresenceOptions:PresenceTopicPattern"] ?? "smarthome/status/{node}/online";
            });

            var connectionString =
                Environment.GetEnvironmentVariable("SMART_HOME_CONNECTION_STRING") ??
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
                Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection in appsettings.json or environment variables.");
            }

            services.AddDbContextFactory<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddSingleton<IMqttService, MqttClientService>();
            services.AddSingleton<NodePresenceService>();

            // Data-access service layer (extracted from ViewModels for testability — PHAN_TICH 1.4 #4).
            services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IPinCodeService, PinCodeService>();
            services.AddTransient<IRfidCardService, RfidCardService>();
            services.AddTransient<IAccessLogService, AccessLogService>();
            services.AddTransient<IAlertService, AlertService>();
            services.AddTransient<IDeviceService, DeviceService>();
            services.AddTransient<IDashboardService, DashboardService>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<UsersViewModel>();
            services.AddTransient<RFIDCardsViewModel>();
            services.AddTransient<PinCodesViewModel>();
            services.AddTransient<AccessLogsViewModel>();
            services.AddTransient<DeviceControlViewModel>();
            services.AddTransient<AlertsViewModel>();

            services.AddTransient<MainWindow>();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider != null)
            {
                var mqttService = ServiceProvider.GetService<IMqttService>();
                if (mqttService != null)
                {
                    await mqttService.DisconnectAsync();
                }

                if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            base.OnExit(e);
        }
    }
}
