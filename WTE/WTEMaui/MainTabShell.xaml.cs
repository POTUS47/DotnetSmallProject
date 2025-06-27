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
            
            // 设置健康主题强调色
            SetHealthyThemeColors();
        }
        
        private void SetHealthyThemeColors()
        {
            // 设置选中项的强调色为健康绿
            this.Items.OfType<TabBar>().FirstOrDefault()?.Items.ToList().ForEach(item =>
            {
                // 为每个标签设置健康主题色调
                item.SetValue(Shell.TabBarTitleColorProperty, Color.FromArgb("#2D5016"));
            });
        }
    }
}