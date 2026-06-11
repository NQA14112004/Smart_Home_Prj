using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Smart_Home.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        private readonly IServiceProvider _serviceProvider;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            CurrentViewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
        }

        [RelayCommand]
        private void Navigate(string viewName)
        {
            var nextViewModel = viewName switch
            {
                "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
                "Users" => _serviceProvider.GetRequiredService<UsersViewModel>(),
                "RFIDCards" => _serviceProvider.GetRequiredService<RFIDCardsViewModel>(),
                "PinCodes" => _serviceProvider.GetRequiredService<PinCodesViewModel>(),
                "AccessLogs" => _serviceProvider.GetRequiredService<AccessLogsViewModel>(),
                "DeviceControl" => _serviceProvider.GetRequiredService<DeviceControlViewModel>(),
                "Alerts" => _serviceProvider.GetRequiredService<AlertsViewModel>(),
                _ => CurrentViewModel
            };

            if (!ReferenceEquals(CurrentViewModel, nextViewModel))
            {
                (CurrentViewModel as IDisposable)?.Dispose();
                CurrentViewModel = nextViewModel;
            }
        }
    }
}
