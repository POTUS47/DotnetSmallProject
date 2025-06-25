using DataAccessLib.Models;
using DataAccessLib.Services;
using WTEMaui.Views;
using Microsoft.Extensions.Logging;

namespace WTEMaui
{
    [QueryProperty(nameof(Username), "user")]
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private User? _currentUser;
        private readonly UserService _userService;
        private readonly ILogger<MainPage> _logger;
        
        public string Username { get; set; } = string.Empty;

        public MainPage(UserService userService, ILogger<MainPage> logger = null)
        {
            InitializeComponent();
            _userService = userService;
            _logger = logger;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            try
            {
                _logger?.LogInformation("MainPage OnAppearing, Username: {Username}", Username);
                
                if (!string.IsNullOrEmpty(Username))
                {
                    _currentUser = await _userService.GetByUsernameAsync(Username);
                    _logger?.LogInformation("加载用户信息成功: {Username}", Username);
                }
                else
                {
                    _logger?.LogInformation("Username为空，显示默认信息");
                }
                
                UpdateUserInfo();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载用户信息时发生错误: {Message}", ex.Message);
                UpdateUserInfo(); // 显示默认信息
            }
        }

        private void UpdateUserInfo()
        {
            try
            {
                if (_currentUser != null)
                {
                    WelcomeLabel.Text = $"欢迎回来，{_currentUser.Username}！";
                    UserInfoLabel.Text = $"邮箱: {_currentUser.Email ?? "未设置"}\n用户ID: {_currentUser.UserId}";
                    _logger?.LogInformation("用户信息更新完成: {Username}", _currentUser.Username);
                }
                else
                {
                    WelcomeLabel.Text = "欢迎使用！";
                    UserInfoLabel.Text = $"当前用户名参数: {Username ?? "无"}";
                    _logger?.LogInformation("显示默认用户信息");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新用户信息时发生错误: {Message}", ex.Message);
                WelcomeLabel.Text = "加载用户信息出错";
                UserInfoLabel.Text = ex.Message;
            }
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            var result = await DisplayAlert("确认退出", "确定要退出登录吗？", "确定", "取消");

            if (result)
            {
                // 清除当前用户信息
                App.CurrentUser = null;
                
                // 返回登录页面
                await Shell.Current.GoToAsync($"{nameof(LoginPage)}");
            }
        }

        private async void OnUserManagementClicked(object sender, EventArgs e)
        {
            // 跳转到用户管理页面
            await Shell.Current.GoToAsync(nameof(UserManagementPage));
        }
    }
}