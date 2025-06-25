using Microsoft.Maui.Controls;
using WTEMaui.Views;

namespace WTEMaui
{
    public partial class MainTabShell : Shell
    {
        public MainTabShell()
        {
            InitializeComponent();
            
            // 注册路由
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        }
    }
}