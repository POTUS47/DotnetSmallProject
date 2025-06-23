using WTEMaui.Services;
using WTEMaui.Models;

namespace WTEMaui.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly DatabaseService _databaseService;

        public LoginPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            var username = UsernameEntry.Text?.Trim();
            var password = PasswordEntry.Text?.Trim();

            // 验证输入
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("请输入用户名和密码", true);
                return;
            }

            // 显示加载状态
            ShowStatus("登录中...", false);

            try
            {
                // 尝试登录
                var user = await _databaseService.LoginAsync(username, password);

                if (user != null)
                {
                    ShowStatus("登录成功！", false);
                    
                    // 延迟一下再跳转，让用户看到成功消息
                    await Task.Delay(1000);
                    
                    // 跳转到主页面，传递用户信息
                    await Shell.Current.GoToAsync($"{nameof(MainPage)}?user={user.Username}");
                }
                else
                {
                    ShowStatus("用户名或密码错误", true);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"登录失败: {ex.Message}", true);
            }
        }

        private async void OnRegisterTapped(object sender, EventArgs e)
        {
            // 跳转到注册页面
            await Shell.Current.GoToAsync(nameof(RegisterPage));
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusLabel.Text = message;
            StatusLabel.TextColor = isError ? Colors.Red : Colors.Green;
            StatusLabel.IsVisible = true;
        }
    }
} 