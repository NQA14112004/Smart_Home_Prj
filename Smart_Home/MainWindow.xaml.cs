using System.Windows;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.ViewModels;

namespace Smart_Home
{
    public partial class MainWindow : Window
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public MainWindow(MainViewModel viewModel, IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
            InitializeComponent();
            DataContext = viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            bool isConnected = await context.TestConnectionAsync();

            if (!isConnected)
            {
                MessageBox.Show("Kết nối Database thất bại. Vui lòng kiểm tra lại cấu hình (mật khẩu, cổng, host).", "Lỗi kết nối", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
