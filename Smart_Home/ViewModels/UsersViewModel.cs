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
    public partial class UsersViewModel : ObservableObject
    {
        private readonly IUserService _userService;

        [ObservableProperty]
        private ObservableCollection<User> _usersList = new();

        [ObservableProperty]
        private ObservableCollection<Role> _rolesList = new();

        [ObservableProperty]
        private User? _selectedUser;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private Role? _selectedRole;

        public UsersViewModel(IUserService userService)
        {
            _userService = userService;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                RolesList = new ObservableCollection<Role>(await _userService.GetRolesAsync());
                if (RolesList.Any())
                {
                    SelectedRole = RolesList.First();
                }

                await RefreshUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải dữ liệu: " + ex.Message);
            }
        }

        private async Task RefreshUsersAsync()
        {
            UsersList = new ObservableCollection<User>(await _userService.GetUsersAsync());
        }

        partial void OnSelectedUserChanged(User? value)
        {
            if (value != null)
            {
                FullName = value.FullName;
                Username = value.Username ?? string.Empty;
                SelectedRole = RolesList.FirstOrDefault(r => r.Id == value.RoleId);
            }
            else
            {
                FullName = string.Empty;
                Username = string.Empty;
                SelectedRole = RolesList.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task AddUserAsync()
        {
            if (SelectedRole == null)
            {
                MessageBox.Show("Vui lòng chọn phân quyền.");
                return;
            }

            var result = await _userService.CreateUserAsync(FullName, Username, SelectedRole.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshUsersAsync();
            MessageBox.Show("Thêm người dùng thành công!");
            SelectedUser = null;
        }

        [RelayCommand]
        private async Task UpdateUserAsync()
        {
            if (SelectedUser == null || SelectedRole == null)
            {
                MessageBox.Show("Vui lòng chọn người dùng và phân quyền để cập nhật.");
                return;
            }

            var result = await _userService.UpdateUserAsync(SelectedUser.Id, FullName, Username, SelectedRole.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshUsersAsync();
            MessageBox.Show("Cập nhật thành công!");
        }

        [RelayCommand]
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null)
            {
                MessageBox.Show("Vui lòng chọn người dùng để khóa/xóa.");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có chắc muốn khóa/xóa user {SelectedUser.Username}?", "Xác nhận", MessageBoxButton.YesNo);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await _userService.SoftDeleteUserAsync(SelectedUser.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage);
                return;
            }

            await RefreshUsersAsync();
            MessageBox.Show("Đã khóa/xóa người dùng!");
            SelectedUser = null;
        }
    }
}
