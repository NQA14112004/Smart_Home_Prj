using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Smart_Home.Models;
using Smart_Home.Service;

namespace Smart_Home.ViewModels
{
    public partial class PinCodesViewModel : ObservableObject
    {
        private readonly IPinCodeService _pinCodeService;

        [ObservableProperty]
        private ObservableCollection<PinCode> _pinsList = new();

        [ObservableProperty]
        private ObservableCollection<User> _usersList = new();

        [ObservableProperty]
        private PinCode? _selectedPin;

        [ObservableProperty]
        private User? _selectedUser;

        [ObservableProperty]
        private string _pinInput = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        public PinCodesViewModel(IPinCodeService pinCodeService)
        {
            _pinCodeService = pinCodeService;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                UsersList = new ObservableCollection<User>(await _pinCodeService.GetActiveUsersAsync());
                SelectedUser = UsersList.FirstOrDefault();

                await RefreshPinsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        private async Task RefreshPinsAsync()
        {
            PinsList = new ObservableCollection<PinCode>(await _pinCodeService.GetPinsAsync());
        }

        partial void OnSelectedPinChanged(PinCode? value)
        {
            if (value != null)
            {
                PinInput = string.Empty;
                IsActive = value.IsActive;
                SelectedUser = UsersList.FirstOrDefault(u => u.Id == value.UserId);
            }
            else
            {
                PinInput = string.Empty;
                IsActive = true;
                SelectedUser = UsersList.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task AddPinAsync()
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn người dùng.");
                return;
            }

            var result = await _pinCodeService.CreatePinAsync(SelectedUser.Id, PinInput, IsActive);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshPinsAsync();
            MessageBox.Show("Thêm mã PIN thành công!");
            SelectedPin = null;
        }

        [RelayCommand]
        private async Task UpdatePinAsync()
        {
            if (SelectedPin == null || SelectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn mã PIN và người dùng để cập nhật.");
                return;
            }

            var result = await _pinCodeService.UpdatePinAsync(SelectedPin.Id, SelectedUser.Id, PinInput, IsActive);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshPinsAsync();
            MessageBox.Show("Cập nhật thành công!");
        }

        [RelayCommand]
        private async Task DeletePinAsync()
        {
            if (SelectedPin == null)
            {
                MessageBox.Show("Vui lòng chọn mã PIN để khóa/xóa.");
                return;
            }

            var confirm = MessageBox.Show("Bạn có chắc muốn khóa/xóa mã PIN này?", "Xác nhận", MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await _pinCodeService.DeactivatePinAsync(SelectedPin.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshPinsAsync();
            MessageBox.Show("Đã khóa mã PIN!");
            SelectedPin = null;
        }
    }
}
