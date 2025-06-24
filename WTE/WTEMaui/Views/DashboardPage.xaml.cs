using LLMLib;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using DataAccessLib.Services;

namespace WTEMaui.Views
{
    public partial class DashboardPage : ContentPage
    {
        private readonly ImageRecognitionService _imageService;
        private readonly OssService _ossService;
        private byte[] _capturedImageData;
        private string _capturedImagePath; // 存储拍摄的图片路径
        private string _ossImagePath; // 存储OSS中的图片路径
        private readonly ILogger<DashboardPage> _logger;

        public DashboardPage(
            ImageRecognitionService imageService,
            OssService ossService,
            ILogger<DashboardPage> logger)
        {
            InitializeComponent();
            _imageService = imageService;
            _ossService = ossService;
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
                LoadingIndicator.IsRunning = true;

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

                if (photo == null) 
                {
                    LoadingIndicator.IsRunning = false;
                    return;
                }

                // 记录照片信息
                _capturedImagePath = photo.FullPath;
                _logger?.LogInformation("照片完整路径: {FullPath}", photo.FullPath);
                _logger?.LogInformation("文件是否存在: {Exists}", File.Exists(photo.FullPath));

                // 显示预览
                using (var stream = await photo.OpenReadAsync())
                {
                    _capturedImageData = await ReadFully(stream);
                    _logger?.LogInformation("图片数据大小: {Size} bytes", _capturedImageData.Length);
                    
                    PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_capturedImageData));
                    PreviewImage.IsVisible = true;
                }

                // 上传到OSS
                await UploadImageToOssAsync(photo.FullPath, photo.FileName);

                // 显示识别按钮
                RecognizeButton.IsVisible = true;
                LoadingIndicator.IsRunning = false;
            }
            catch (Exception ex)
            {
                LoadingIndicator.IsRunning = false;
                _logger?.LogError(ex, "拍照失败");
                await DisplayAlert("错误", $"拍照失败: {ex.Message}", "确定");
            }
        }

        /// <summary>
        /// 上传图片到OSS
        /// </summary>
        private async Task UploadImageToOssAsync(string localImagePath, string fileName)
        {
            try
            {
                // 生成OSS对象名称
                var fileExtension = Path.GetExtension(fileName);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var guid = Guid.NewGuid().ToString("N")[..8]; // 取前8位
                var ossObjectName = $"food-photos/{DateTime.Now:yyyy/MM/dd}/{timestamp}_{guid}{fileExtension}";

                _logger?.LogInformation("开始上传图片到OSS: {LocalPath} -> {OssPath}", localImagePath, ossObjectName);

                // 在后台线程执行上传
                await Task.Run(() =>
                {
                    _ossService.UploadFile(ossObjectName, localImagePath);
                });

                _ossImagePath = ossObjectName;
                _logger?.LogInformation("图片上传成功: {OssPath}", ossObjectName);

                // 在主线程显示成功消息
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("上传成功", $"图片已保存到云端\n路径: {ossObjectName}", "确定");
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "上传图片到OSS失败: {LocalPath}", localImagePath);
                
                // 在主线程显示错误消息
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("上传失败", $"图片上传失败: {ex.Message}", "确定");
                });
                
                throw; // 重新抛出异常以便上层处理
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

                // 显示结果（包含OSS信息）
                var displayResult = result;
                if (!string.IsNullOrEmpty(_ossImagePath))
                {
                    var imageUrl = _ossService.GetFileUrl(_ossImagePath);
                    displayResult += $"\n\n📸 图片已保存到云端\n🔗 访问链接: {imageUrl}";
                }

                ResultLabel.Text = displayResult;
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
            
            // 可选：清理本地缓存文件
            CleanupLocalImage();
        }

        /// <summary>
        /// 清理本地缓存图片
        /// </summary>
        private void CleanupLocalImage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_capturedImagePath) && File.Exists(_capturedImagePath))
                {
                    File.Delete(_capturedImagePath);
                    _logger?.LogInformation("已清理本地缓存图片: {Path}", _capturedImagePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "清理本地缓存图片失败: {Path}", _capturedImagePath);
            }
            finally
            {
                _capturedImagePath = null;
                _ossImagePath = null;
                _capturedImageData = null;
            }
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