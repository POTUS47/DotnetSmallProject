//using DataAccessLib.Services;

//{
//    public partial class MainPage : ContentPage
//    {
//        int count = 0;

//        public MainPage()
//        {
//            InitializeComponent();
//        }

//        private void OnCounterClicked(object sender, EventArgs e)
//        {
//            count++;

//            if (count == 1)
//                CounterBtn.Text = $"Clicked {count} time";
//            else
//                CounterBtn.Text = $"Clicked {count} times";

//            SemanticScreenReader.Announce(CounterBtn.Text);
//        }
//    }

//}

using DataAccessLib.Services;
using Microsoft.EntityFrameworkCore;
namespace WTEMaui;
public partial class MainPage : ContentPage
{
    private readonly TestService _testService;

    public MainPage(TestService testService)
    {
        InitializeComponent();
        _testService = testService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var canConnect = await _testService.CanConnectAsync();
            await DisplayAlert("数据库连接测试",
                canConnect ? "成功!" : "失败!", "确定");

            if (canConnect)
            {
                var newUser = await _testService.AddTestUserAsync();
                var users = await _testService.GetAllUsersAsync();

                await DisplayAlert("测试结果",
                    $"新增用户: {newUser.Username}\n总用户数: {users.Count}", "确定");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("发生错误",
                $"类型: {ex.GetType().Name}\n消息: {ex.Message}", "确定");
        }
    }
}
