using WTEMaui.Models;
using WTEMaui.Services;
using WTEMaui.Views;

namespace WTEMaui
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private User? _currentUser;
        private readonly DatabaseService _databaseService;

        public MainPage()
        {
            InitializeComponent();
            _databaseService = new DatabaseService();
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            // 从Shell参数中获取用户名
            var parameters = Shell.Current.CurrentState.Location.ToString();
            if (parameters.Contains("user="))
            {
                var username = parameters.Split("user=")[1].Split("&")[0];
                _currentUser = await _databaseService.GetUserByUsernameAsync(username);
            }

            UpdateUserInfo();
        }

        private void UpdateUserInfo()
        {
            if (_currentUser != null)
            {
                WelcomeLabel.Text = $"欢迎回来，{_currentUser.Username}！";
                UserInfoLabel.Text = $"邮箱: {_currentUser.Email}\n注册时间: {_currentUser.CreatedAt:yyyy-MM-dd HH:mm}\n最后登录: {_currentUser.LastLoginAt:yyyy-MM-dd HH:mm}";
            }
            else
            {
                WelcomeLabel.Text = "欢迎使用！";
                UserInfoLabel.Text = "未登录用户";
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
                // 返回登录页面
                await Shell.Current.GoToAsync("//LoginPage");
            }
        }

        private async void OnUserManagementClicked(object sender, EventArgs e)
        {
            // 跳转到用户管理页面
            await Shell.Current.GoToAsync(nameof(UserManagementPage));
        }
    }
}