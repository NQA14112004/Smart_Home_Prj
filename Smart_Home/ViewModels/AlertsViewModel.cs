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
    public partial class AlertsViewModel : ObservableObject
    {
        private readonly IAlertService _alertService;

        [ObservableProperty]
        private ObservableCollection<Alert> _alertsList = new();

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        public AlertsViewModel(IAlertService alertService)
        {
            _alertService = alertService;
            _ = LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                var alerts = await _alertService.GetAlertsAsync(StartDate, EndDate);
                AlertsList = new ObservableCollection<Alert>(alerts);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải lịch sử cảnh báo: " + ex.Message);
            }
        }
    }
}
