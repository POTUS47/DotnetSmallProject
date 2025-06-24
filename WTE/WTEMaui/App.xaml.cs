namespace WTEMaui
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }

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
