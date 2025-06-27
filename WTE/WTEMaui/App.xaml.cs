using DataAccessLib.Models;

namespace WTEMaui
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }
        
        // 添加当前登录用户的静态属性
        public static User CurrentUser { get; set; }

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            Services = serviceProvider;
            MainPage = new AppShell();
        }


        // 保留无参构造函数以兼容XAML
        public App() : this(null) { }
    }
}
