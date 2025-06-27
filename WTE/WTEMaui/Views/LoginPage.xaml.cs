using DataAccessLib.Services;
using DataAccessLib.Models;
using WTEMaui.Views;
using Microsoft.Extensions.Logging;

namespace WTEMaui.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly UserService _userService;
        private readonly ILogger<LoginPage> _logger;

        public LoginPage(UserService userService, ILogger<LoginPage> logger = null)
        {
            InitializeComponent();
            _userService = userService;
            _logger = logger;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim();
            var password = PasswordEntry.Text?.Trim();

            _logger?.LogInformation("登录按钮被点击，用户名: {Username}", username);

            // 验证输入
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("请输入用户名和密码", StatusType.Error);
                return;
            }

            // 显示加载状态
            ShowStatus("正在登录，请稍候...", StatusType.Loading);
            LoginButton.IsEnabled = false;

            try
            {
                _logger?.LogInformation("开始调用LoginAsync方法");
                
                // 尝试登录
                var user = await _userService.LoginAsync(username, password);

                if (user != null)
                {
                    _logger?.LogInformation("登录成功，准备跳转页面");
                    ShowStatus("登录成功！正在跳转...", StatusType.Success);
                    
                    // 存储当前登录用户信息
                    App.CurrentUser = user;
                    
                    // 延迟一下再跳转，让用户看到成功消息
                    await Task.Delay(1500);
                    HideStatus();

                    // 设置新的主页面为带底部 Tab 的 Shell 页面
                    Application.Current.MainPage = new MainTabShell();
                }
                else
                {
                    ShowStatus("用户名或密码错误", StatusType.Error);
                    LoginButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "登录过程中发生异常，用户名: {Username}, 异常类型: {ExceptionType}, 消息: {Message}", 
                    username, ex.GetType().Name, ex.Message);
                ShowStatus($"登录失败: {ex.Message}", StatusType.Error);
                LoginButton.IsEnabled = true;
            }
        }

        private async void OnRegisterTapped(object sender, EventArgs e)
        {
            // 跳转到注册页面
            await Shell.Current.GoToAsync(nameof(RegisterPage));
        }

        private void ShowStatus(string message, StatusType type)
        {
            StatusLabel.Text = message;
            LoadingIndicator.IsVisible = type == StatusType.Loading;
            LoadingIndicator.IsRunning = type == StatusType.Loading;
            
            switch (type)
            {
                case StatusType.Error:
                    StatusLabel.TextColor = Colors.White;
                    StatusFrame.BackgroundColor = Color.FromArgb("#FFE57373"); // 浅红色
                    break;
                case StatusType.Success:
                    StatusLabel.TextColor = Colors.White;
                    StatusFrame.BackgroundColor = Color.FromArgb("#FF81C784"); // 浅绿色
                    break;
                case StatusType.Loading:
                    StatusLabel.TextColor = Color.FromArgb("#FF666666");
                    StatusFrame.BackgroundColor = Color.FromArgb("#FFF0F0F0"); // 浅灰色
                    break;
            }
            
            StatusContainer.IsVisible = true;
        }

        private void HideStatus()
        {
            StatusContainer.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    public enum StatusType
    {
        Error,
        Success,
        Loading
    }
}