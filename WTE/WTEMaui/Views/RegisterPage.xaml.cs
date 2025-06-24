using DataAccessLib.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace WTEMaui.Views
{
    public partial class RegisterPage : ContentPage
    {
        private readonly UserService _userService;
        private readonly ILogger<RegisterPage> _logger;

        public RegisterPage(UserService userService, ILogger<RegisterPage> logger = null)
        {
            InitializeComponent();
            _userService = userService;
            _logger = logger;
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim();
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text?.Trim();
            var confirmPassword = ConfirmPasswordEntry.Text?.Trim();

            _logger?.LogInformation("注册按钮被点击，用户名: {Username}, 邮箱: {Email}", username, email);

            // 验证输入
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ShowStatus("请填写所有字段", true);
                return;
            }

            // 验证用户名长度
            if (username.Length < 3 || username.Length > 20)
            {
                ShowStatus("用户名长度应在3-20个字符之间", true);
                return;
            }

            // 验证邮箱格式
            if (!IsValidEmail(email))
            {
                ShowStatus("请输入有效的邮箱地址", true);
                return;
            }

            // 验证密码长度
            if (password.Length < 6)
            {
                ShowStatus("密码长度至少6个字符", true);
                return;
            }

            // 验证密码确认
            if (password != confirmPassword)
            {
                ShowStatus("两次输入的密码不一致", true);
                return;
            }

            // 显示加载状态
            ShowStatus("注册中...", false);

            try
            {
                _logger?.LogInformation("开始调用RegisterAsync方法");
                
                // 尝试注册
                var user = await _userService.RegisterAsync(username, email, password);

                if (user != null)
                {
                    _logger?.LogInformation("注册成功，用户ID: {UserId}", user.UserId);
                    ShowStatus("注册成功！正在跳转到登录页面...", false);
                    
                    // 延迟一下再跳转
                    await Task.Delay(2000);
                    
                    // 返回登录页面
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    ShowStatus("注册失败，请重试", true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "注册过程中发生异常，用户名: {Username}, 邮箱: {Email}, 异常类型: {ExceptionType}, 消息: {Message}", 
                    username, email, ex.GetType().Name, ex.Message);
                ShowStatus($"注册失败: {ex.Message}", true);
            }
        }

        private async void OnBackToLoginTapped(object sender, EventArgs e)
        {
            // 返回登录页面
            await Shell.Current.GoToAsync("..");
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusLabel.Text = message;
            StatusLabel.TextColor = isError ? Colors.Red : Colors.Green;
            StatusLabel.IsVisible = true;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }
    }
}