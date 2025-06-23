using WTEMaui.Models;
using WTEMaui.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace WTEMaui.Views
{
    public partial class UserManagementPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        public ObservableCollection<User> Users { get; set; }
        public ICommand DeleteUserCommand { get; set; }

        public UserManagementPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
            Users = new ObservableCollection<User>();
            DeleteUserCommand = new Command<int>(async (userId) => await DeleteUser(userId));
            
            BindingContext = this;
            LoadUsers();
        }

        private async void LoadUsers()
        {
            LoadingIndicator.IsVisible = true;
            UserCollectionView.IsVisible = false;

            try
            {
                var users = await _databaseService.GetAllUsersAsync();
                Users.Clear();
                foreach (var user in users)
                {
                    Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"加载用户列表失败: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                UserCollectionView.IsVisible = true;
            }
        }

        private async Task DeleteUser(int userId)
        {
            var result = await DisplayAlert("确认删除", "确定要删除这个用户吗？", "确定", "取消");
            
            if (result)
            {
                try
                {
                    var success = await _databaseService.DeleteUserAsync(userId);
                    if (success)
                    {
                        // 从列表中移除
                        var userToRemove = Users.FirstOrDefault(u => u.Id == userId);
                        if (userToRemove != null)
                        {
                            Users.Remove(userToRemove);
                        }
                        await DisplayAlert("成功", "用户已删除", "确定");
                    }
                    else
                    {
                        await DisplayAlert("错误", "删除用户失败", "确定");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("错误", $"删除用户失败: {ex.Message}", "确定");
                }
            }
        }
    }
} 