using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Smart_Home.Models;
using Smart_Home.Service;

namespace Smart_Home.ViewModels
{
    public partial class AccessLogsViewModel : ObservableObject
    {
        private readonly IAccessLogService _accessLogService;

        [ObservableProperty]
        private ObservableCollection<AccessLog> _logsList = new();

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        public AccessLogsViewModel(IAccessLogService accessLogService)
        {
            _accessLogService = accessLogService;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                var logs = await _accessLogService.GetLogsAsync(StartDate, EndDate);
                LogsList = new ObservableCollection<AccessLog>(logs);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải lịch sử truy cập: " + ex.Message);
            }
        }
    }
}
