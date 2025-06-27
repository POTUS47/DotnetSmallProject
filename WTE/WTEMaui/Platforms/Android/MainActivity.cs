using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;

namespace WTEMaui
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // 确保在基类OnCreate之后设置
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                // 设置状态栏颜色（替换为你想要的颜色）
                Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#6200EE")); // 默认紫色

                // 设置状态栏图标颜色（浅色或深色）
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)
                    (SystemUiFlags.LightStatusBar); // 黑色图标
                                                    // | SystemUiFlags.LightNavigationBar); // 如果需要也可以设置导航栏

                // 边缘到边缘设置
                Google.Android.Material.Internal.EdgeToEdgeUtils.ApplyEdgeToEdge(Window, true);

                if (OperatingSystem.IsAndroidVersionAtLeast((int)BuildVersionCodes.Q))
                {
                    Window.StatusBarContrastEnforced = false;
                    Window.NavigationBarContrastEnforced = false;
                }
            }
        }
    }
}