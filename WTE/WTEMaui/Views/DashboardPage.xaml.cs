using LLMLib;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;

namespace WTEMaui.Views
{
    public partial class DashboardPage : ContentPage
    {
        private readonly ImageRecognitionService _imageService;
        private byte[] _capturedImageData;
        private readonly ILogger<DashboardPage> _logger;

        public DashboardPage(
            ImageRecognitionService imageService,
            ILogger<DashboardPage> logger)
        {
            InitializeComponent();
            _imageService = imageService;
            _logger = logger;

            // 初始化UI状态
            LoadingIndicator.IsRunning = false;
            ResultFrame.IsVisible = false;
            RecognizeButton.IsVisible = false;
            PreviewImage.IsVisible = false;
        }

        private async void OnTakePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                // 重置UI状态
                ResultFrame.IsVisible = false;
                RecognizeButton.IsVisible = false;

                // 检查权限
                if (!await CheckAndRequestPermissions())
                {
                    await DisplayAlert("权限不足", "需要相机权限才能使用此功能", "确定");
                    return;
                }

                // 拍照
                var photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "请拍摄清晰的食物照片"
                });

                if (photo == null) return;

                // 显示预览
                using (var stream = await photo.OpenReadAsync())
                {
                    _capturedImageData = await ReadFully(stream);
                    PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_capturedImageData));
                    PreviewImage.IsVisible = true;
                    RecognizeButton.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "拍照失败");
                await DisplayAlert("错误", $"拍照失败: {ex.Message}", "确定");
            }
        }

        private async void OnRecognizeClicked(object sender, EventArgs e)
        {
            if (_capturedImageData == null || _capturedImageData.Length == 0)
            {
                await DisplayAlert("提示", "请先拍摄食物照片", "确定");
                return;
            }

            try
            {
                // 更新UI状态
                LoadingIndicator.IsRunning = true;
                RecognizeButton.IsEnabled = false;
                ResultFrame.IsVisible = false;

                // 调用识别服务
                var result = await _imageService.RecognizeFoodFromImageAsync(_capturedImageData);

                // 显示结果
                ResultLabel.Text = result;
                ResultFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("识别失败", $"识别时出错: {ex.Message}", "确定");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                RecognizeButton.IsEnabled = true;
            }
        }

        private void OnDismissResultClicked(object sender, EventArgs e)
        {
            ResultFrame.IsVisible = false;
        }

        private async Task<bool> CheckAndRequestPermissions()
        {
            try
            {
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }
                return cameraStatus == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "权限检查失败");
                return false;
            }
        }

        private async Task<byte[]> ReadFully(Stream input)
        {
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}