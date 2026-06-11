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
    public partial class RFIDCardsViewModel : ObservableObject
    {
        private readonly IRfidCardService _rfidCardService;

        [ObservableProperty]
        private ObservableCollection<RfidCard> _cardsList = new();

        [ObservableProperty]
        private ObservableCollection<User> _usersList = new();

        [ObservableProperty]
        private RfidCard? _selectedCard;

        [ObservableProperty]
        private User? _selectedUser;

        [ObservableProperty]
        private string _cardUid = string.Empty;

        [ObservableProperty]
        private string _cardLabel = string.Empty;

        [ObservableProperty]
        private bool _isActive = true;

        public RFIDCardsViewModel(IRfidCardService rfidCardService)
        {
            _rfidCardService = rfidCardService;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                UsersList = new ObservableCollection<User>(await _rfidCardService.GetActiveUsersAsync());
                SelectedUser = UsersList.FirstOrDefault();

                await RefreshCardsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        private async Task RefreshCardsAsync()
        {
            CardsList = new ObservableCollection<RfidCard>(await _rfidCardService.GetCardsAsync());
        }

        partial void OnSelectedCardChanged(RfidCard? value)
        {
            if (value != null)
            {
                CardUid = value.CardUid;
                CardLabel = value.CardLabel ?? string.Empty;
                IsActive = value.IsActive;
                SelectedUser = UsersList.FirstOrDefault(u => u.Id == value.UserId);
            }
            else
            {
                CardUid = string.Empty;
                CardLabel = string.Empty;
                IsActive = true;
                SelectedUser = UsersList.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task AddCardAsync()
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn người dùng.");
                return;
            }

            var result = await _rfidCardService.CreateCardAsync(CardUid, CardLabel, SelectedUser.Id, IsActive);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshCardsAsync();
            MessageBox.Show("Thêm thẻ RFID thành công!");
            SelectedCard = null;
        }

        [RelayCommand]
        private async Task UpdateCardAsync()
        {
            if (SelectedCard == null || SelectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn thẻ và người dùng để cập nhật.");
                return;
            }

            var result = await _rfidCardService.UpdateCardAsync(SelectedCard.Id, CardUid, CardLabel, SelectedUser.Id, IsActive);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshCardsAsync();
            MessageBox.Show("Cập nhật thành công!");
        }

        [RelayCommand]
        private async Task DeleteCardAsync()
        {
            if (SelectedCard == null)
            {
                MessageBox.Show("Vui lòng chọn thẻ để khóa/xóa.");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có chắc muốn khóa thẻ {SelectedCard.CardUid}?", "Xác nhận", MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await _rfidCardService.DeactivateCardAsync(SelectedCard.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshCardsAsync();
            MessageBox.Show("Đã khóa thẻ RFID!");
            SelectedCard = null;
        }
    }
}
